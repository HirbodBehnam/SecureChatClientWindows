using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Web;
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
    }
}
