using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using org.whispersystems.curve25519;
using SQLite;
using WebSocketSharp;

namespace Chat
{
    /// <summary>
    /// Some variables that can be used in whole application
    /// </summary>
    internal static class SharedStuff
    {
        /// <summary>
        /// A dictionary that contains all messages that are not delivered yet, by they GUID
        /// </summary>
        public static readonly Dictionary<string,ChatMessagesNotify> PendingMessages = new Dictionary<string, ChatMessagesNotify>();
        /// <summary>
        /// An http client to use it in from all of the application https://stackoverflow.com/a/4015346/4213397
        /// </summary>
        public static readonly HttpClient Client = new HttpClient();
        /// <summary>
        /// Main websocket that all of the windows use it
        /// </summary>
        public static WebSocket Websocket;
        /// <summary>
        /// The SQLite database of messages
        /// </summary>
        public static SQLiteAsyncConnection Database;
        /// <summary>
        /// This value holds the RSA public key of the server
        /// </summary>
        public static AsymmetricKeyParameter ServerPublicKey = null;
        /// <summary>
        /// Use this curve for key exchange
        /// </summary>
        public static readonly Curve25519 Curve = Curve25519.getInstance(Curve25519.BEST);
        /// <summary>
        /// Secure random number generator
        /// </summary>
        public static RNGCryptoServiceProvider SecureRandom = new RNGCryptoServiceProvider();
        /// <summary>
        /// Create URL quarry from https://stackoverflow.com/a/829138/4213397
        /// </summary>
        /// <param name="nvc">The quarries</param>
        /// <returns>The encoded query</returns>
        public static string ToQueryString(NameValueCollection nvc)
        {
            var array = (
                from key in nvc.AllKeys
                from value in nvc.GetValues(key)
                select $"{HttpUtility.UrlEncode(key)}={HttpUtility.UrlEncode(value)}"
            ).ToArray();
            return "?" + string.Join("&", array);
        }
        /// <summary>
        /// Generates a url with query
        /// </summary>
        /// <param name="url">The base URL</param>
        /// <param name="nvc">Parameters</param>
        /// <returns>The url with parameters</returns>
        public static string CreateUrlWithQuery(string url, NameValueCollection nvc) => url + ToQueryString(nvc);
        /// <summary>
        /// A small function to get the user data of a user
        /// </summary>
        /// <param name="username">Username of the user</param>
        /// <returns></returns>
        public static async Task<JsonTypes.UserDataStruct> GetUserData(string username)
        {
            NameValueCollection queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString.Add("username", username);
            string url = CreateUrlWithQuery(
                "https://" + Properties.Settings.Default.ServerAddress + "/users/getData", queryString);
            string result;
            using (WebClient wc = new WebClient()) // request other user's public key
                result = await wc.DownloadStringTaskAsync(url);
            return JsonConvert.DeserializeObject<JsonTypes.UserDataStruct>(result);
        }
        /// <summary>
        /// Converts an <see cref="int"/> to byte array (big-endian)
        /// </summary>
        /// <param name="i">The number to convert</param>
        /// <returns>A array of 4 bytes</returns>
        public static byte[] IntToBytes(int i) => BitConverter.GetBytes(IPAddress.HostToNetworkOrder(i));
        /// <summary>
        /// Converts a byte array of size 4 (big-endian) to <see cref="int"/>
        /// </summary>
        /// <param name="b">The byte array</param>
        /// <returns>The number</returns>
        public static int BytesToInt(byte[] b) => IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b, 0));

        /// <summary>
        /// Encrypts and uploads a file to server
        /// </summary>
        /// <param name="guid">The ID of message</param>
        public static async void UploadFile(string guid)
        {
            // at first encrypt the file
            var encryptedFile = new FileInfo(Path.GetTempFileName());
            string username = PendingMessages[guid].With;
            var key = Convert.FromBase64String((await Database.Table<DatabaseHelper.Users>()
                .Where(user => user.Username == username).FirstAsync()).Key);
            await Task.Run(() =>
                BouncyCastleHelper.AesGcmEncrypt(new FileInfo(PendingMessages[guid].FilePath),
                    encryptedFile, key));
            // now upload the file
            try
            {
                using (HttpClient localClient = new HttpClient())
                using (var multiForm = new MultipartFormDataContent())
                {
                    localClient.Timeout = TimeSpan.FromMinutes(15); // You may need this if you are uploading a big file

                    var file = new ProgressableStreamContent(
                        new StreamContent(encryptedFile.OpenRead())
                        , (sent, total) => { });
                    multiForm.Add(file, "document", Path.GetFileName(PendingMessages[guid].FilePath)); // Add the file

                    localClient.DefaultRequestHeaders.Add("token",PendingMessages[guid].Token);

                    var response =
                        await localClient.PostAsync("https://" + Properties.Settings.Default.ServerAddress + "/upload",
                            multiForm);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string res = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(res);
                        // parse result
                        var jsonRes = JsonConvert.DeserializeObject<JsonTypes.ServerStatus>(res);
                        if (!jsonRes.Ok)
                            throw new WebException("Server returned an error: " + response.StatusCode);
                        // update UI
                        PendingMessages[guid].Sent = 0;
                        PendingMessages[guid].Progress = 101;
                        // update database
                        await Database.RunInTransactionAsync(tran =>
                        {
                            tran.Insert(new DatabaseHelper.Messages
                            {
                                Date = DateTime.Now,
                                MyMessage = true,
                                Payload = PendingMessages[guid].Token,
                                Type = 1,
                                Username = PendingMessages[guid].With
                            });
                            tran.Insert(new DatabaseHelper.Files
                            {
                                Token = PendingMessages[guid].Token,
                                Location = PendingMessages[guid].FilePath
                            });
                        });
                    }
                    else
                        throw new WebException("HTTP status code is not ok: " + response.StatusCode);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot upload file: " + ex);
                PendingMessages[guid].Sent = 2; // show the user that the upload has been failed
                PendingMessages[guid].Progress = 101; // show the user that the upload has been failed
            }
            finally
            {
                encryptedFile.Delete();
                PendingMessages.Remove(guid);
            }
        }
    }
    /// <summary>
    /// Small enum for global file types
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Text message
        /// </summary>
        Text = 0,
        /// <summary>
        /// File
        /// </summary>
        File = 1,
        /// <summary>
        /// Client sends this type to server to request a token
        /// </summary>
        FileToken = 2
    }
}
