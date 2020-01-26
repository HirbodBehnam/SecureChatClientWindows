using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using SQLite;
using WebSocketSharp;

namespace Chat
{
    /// <summary>
    /// Interaction logic for MainChatsWindow.xaml
    /// </summary>
    public partial class MainChatsWindow : Window
    {
        public ObservableCollection<MainMessagesINotify> MessagesList { get; set; } = new ObservableCollection<MainMessagesINotify>();
        private readonly JsonTypes.SavedData _savedData;
        private readonly SQLiteAsyncConnection _db;
        private readonly SemaphoreSlim _mu = new SemaphoreSlim(1,1);
        public MainChatsWindow()
        {
            InitializeComponent();
            // load saved data
            _savedData = JsonConvert.DeserializeObject<JsonTypes.SavedData>(Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(Properties.Settings.Default.LoginData),null,DataProtectionScope.CurrentUser)));
            HelloTitle.Text = "Hello " + Properties.Settings.Default.Name;
            HelloTitle.Text = "Hello Hirbod";
            // set the ssl policy
            if (Properties.Settings.Default.TrustInvalidSSL)
                ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
            // load the db
            _db = new SQLiteAsyncConnection("database.db");
        }
        private async void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            // create tables
            await _db.CreateTablesAsync<DatabaseHelper.Messages,DatabaseHelper.Files,DatabaseHelper.Users>();
            // get all of the users that have messaged the user and get their last message
            {
                // at first get list of all users
                var users = await _db.Table<DatabaseHelper.Users>().ToArrayAsync();
                var msgs = new MainMessagesINotify[users.Length];
                // then select last message of the users from Messages table
                for (int i = 0;i<users.Length;i++)
                {
                    var lastMsgQ =
                        await _db.QueryAsync<DatabaseHelper.Messages>("SELECT * FROM Messages WHERE Username == ? ORDER BY ID DESC LIMIT 1",users[i].Username);
                    if (lastMsgQ.Count > 0)
                    {
                        var lastMsg = lastMsgQ[0];
                        msgs[i] = new MainMessagesINotify
                        {
                            Username = users[i].Username,
                            IsLastMessageForUser = lastMsg.MyMessage,
                            Message = lastMsg.Payload,
                            Name = users[i].Name,
                            FullDate = lastMsg.Date,
                            NewMessages = users[i].UnreadMessages
                        };
                    }
                }
                // sort the messages by the date
                Array.Sort(msgs,(x, y) => y.FullDate.CompareTo(x.FullDate)); // reverse sort; The latest date is at top
                // add them to main messages list
                foreach (var msg in msgs)
                    MessagesList.Add(msg);
            }
            // connect to server and register the 
            return;
            await Task.Run(() =>
            {
                if (Properties.Settings.Default.TrustInvalidSSL)
                    SharedStuff.Websocket.SslConfiguration.ServerCertificateValidationCallback =
                        (a, certificate, chain, sslPolicyErrors) => true;
                while (true)
                {
                    try
                    {
                        SharedStuff.Websocket = new WebSocket("wss://" + Properties.Settings.Default.ServerAddress + "/chat/registerUpdater");
                        //_ws.OnMessage += WS_OnMessage;
                        SharedStuff.Websocket.Connect();
                        SharedStuff.Websocket.Send(JsonConvert.SerializeObject(new JsonTypes.HelloMessage
                        {
                            Username = _savedData.Username,
                            Password = _savedData.Password,
                            Verify = Convert.ToBase64String(SharedStuff.Curve.calculateSignature(Convert.FromBase64String(_savedData.PrivateKey), Encoding.UTF8.GetBytes(_savedData.Password)))
                        }));
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(5000); // wait 5 seconds and retry
                        continue;
                    }
                    break;
                }
                
            });
        }

        private async void WS_OnMessage(object sender,MessageEventArgs e)
        {
            // at first try to parse the message as Server status.
            try
            {
                var status = JsonConvert.DeserializeObject<JsonTypes.ServerStatus>(e.Data);
                if (!status.Ok)
                {
                    // TODO: HANDLE THIS SHIT
                }
            }
            catch (Exception)
            {
                // ignored : the type might be something else
            }
            // try ClientUpdateTypeStruct
            await _mu.WaitAsync(); // get the data in order that server sends them
            try
            {
                var msg = JsonConvert.DeserializeObject<JsonTypes.Updates>(e.Data);
                var keyList = await _db.Table<DatabaseHelper.Users>().Where(k => k.Username == msg.Payload.From)
                    .ToArrayAsync(); // get the key of the user
                string key;
                if (keyList.Length == 0) // Key agreement
                {
                    NameValueCollection queryString = HttpUtility.ParseQueryString(string.Empty);
                    queryString.Add("username", msg.Payload.From);
                    string url = SharedStuff.CreateUrlWithQuery(
                        "https://" + Properties.Settings.Default.ServerAddress + "/users/getData", queryString);
                    string result;
                    using (WebClient wc = new WebClient()) // request other user's public key
                        result = await wc.DownloadStringTaskAsync(url);
                    var data = JsonConvert.DeserializeObject<JsonTypes.UserDataStruct>(result);
                    key = Convert.ToBase64String(SharedStuff.Curve.calculateAgreement(
                        Convert.FromBase64String(_savedData.PrivateKey),
                        Convert.FromBase64String(data.PublicKey)));
                    await _db.InsertAsync(new DatabaseHelper.Users // save the key in database
                    {
                        Username = data.Username,
                        Name = data.Name,
                        Key = key,
                    });
                }
                else // get the key from database; There is only one row because Username is unique
                    key = keyList[0].Key;

                // decrypt the message or assign the file token
                string message = "";
                try
                {
                    message = msg.Type == 0
                        ? BouncyCastleHelper.AesGcmDecrypt(msg.Payload.Message, key)
                        : msg.Payload.Message;
                }
                catch (Exception)
                {
                    // TODO: RE-REQUEST public key
                }

                // save the value
                int index = MessagesList.IndexOf(MessagesList.First(x => x.Username == msg.Payload.From)); // get index of the UI
                await _db.RunInTransactionAsync(tran =>
                {
                    tran.Insert(new DatabaseHelper.Messages()
                    {
                        Username = msg.Payload.From,
                        Type = msg.Type,
                        Date = msg.Payload.Date,
                        Payload = message
                    });
                    tran.Update(new DatabaseHelper.Users()
                    {
                        Username = msg.Payload.From,
                        UnreadMessages = MessagesList[index].NewMessages + 1
                    });
                });
                // Show the user the update
                MessagesList.Move(index,0);
                MessagesList[0].FullDate = msg.Payload.Date;
                MessagesList[0].Message = msg.Payload.Message;
                MessagesList[0].NewMessages++;
            }
            catch (Exception)
            {
                // TODO: HANDLE THIS SHIT
            }
            finally
            {
                _mu.Release();
            }
        }
        private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            
        }
        private async void AddChatButton_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new AddChatDialog();
            Exception error = null;
            await DialogHost.Show(dialog,  async delegate(object sender1, DialogClosingEventArgs args)
            {
                if (!(bool) args.Parameter) // check if the dialog is canceled
                    return;
                string id = dialog.IdTextBox.Text;
                // check if the ID exists in database
                var dbResult = await _db.Table<DatabaseHelper.Users>().Where(user => user.Username == id)
                    .ToArrayAsync();
                if (dbResult.Length == 0) // get the user's key from server
                {
                    NameValueCollection queryString = HttpUtility.ParseQueryString(string.Empty);
                    queryString.Add("username", id);
                    string url = SharedStuff.CreateUrlWithQuery(
                        "https://" + Properties.Settings.Default.ServerAddress + "/users/getData", queryString);
                    string result;
                    try
                    {
                        using (WebClient wc = new WebClient()) // request other user's public key
                            result = await wc.DownloadStringTaskAsync(url);
                    }
                    catch (Exception ex)
                    {
                        error = new Exception("Cannot connect to server",ex);
                        return;
                    }
                    // check if the username exists
                    try
                    {
                        var data = JsonConvert.DeserializeObject<JsonTypes.UserDataStruct>(result);
                        string key = Convert.ToBase64String(SharedStuff.Curve.calculateAgreement(
                            Convert.FromBase64String(_savedData.PrivateKey),
                            Convert.FromBase64String(data.PublicKey)));
                        await _db.InsertAsync(new DatabaseHelper.Users // save the key in database
                        {
                            Username = data.Username,
                            Name = data.Name,
                            Key = key,
                        });
                    }
                    catch (Exception)
                    {
                        var data = JsonConvert.DeserializeObject<JsonTypes.ServerStatus>(result);
                        error = new Exception("Server returned an error",new Exception(data.Message));
                    }
                }
            });
            if (error != null)
            {
                var errDialog = new ErrorDialogSample
                {
                    ErrorTitle = {Text = error.Message},
                    ErrorText = {Text = error.InnerException.Message}
                };
                await DialogHost.Show(errDialog);
            }
            //TODO open the chat page
        }
        private void MainChatsWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
    
}
