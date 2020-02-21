using System;
using System.Collections.Generic;
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
using System.Windows.Controls;
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
        /// <summary>
        /// This value is used to control the open chat windows; If a window is open, do not allow reopening
        /// </summary>
        public static Dictionary<string,ChatWindow> OpenWindowsList = new Dictionary<string,ChatWindow>();
        /// <summary>
        /// The list that controls the messages in main list
        /// </summary>
        public static ObservableCollection<MainMessagesNotify> MessagesList { get; set; } = new ObservableCollection<MainMessagesNotify>();
        /// <summary>
        /// Saved data is loaded here
        /// </summary>
        private readonly JsonTypes.SavedData _savedData;
        /// <summary>
        /// A mutex like thing to control message receiving flow
        /// </summary>
        private readonly SemaphoreSlim _mu = new SemaphoreSlim(1,1);
        public MainChatsWindow()
        {
            InitializeComponent();
            // load saved data
            _savedData = JsonConvert.DeserializeObject<JsonTypes.SavedData>(Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(Properties.Settings.Default.LoginData),null,DataProtectionScope.CurrentUser)));
            HelloTitle.Text = "Hello " + Properties.Settings.Default.Name;
            // set the ssl policy
            if (Properties.Settings.Default.TrustInvalidSSL)
                ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
            // load the db
            SharedStuff.Database = new SQLiteAsyncConnection("database.db");
        }
        private async void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            // create tables
            await SharedStuff.Database.CreateTablesAsync<DatabaseHelper.Messages,DatabaseHelper.Files,DatabaseHelper.Users>();
            // get all of the users that have messaged the user and get their last message
            {
                // at first get list of all users
                var users = await SharedStuff.Database.Table<DatabaseHelper.Users>().ToArrayAsync();
                var msgs = new MainMessagesNotify[users.Length];
                // then select last message of the users from Messages table
                for (int i = 0;i<users.Length;i++)
                {
                    var lastMsgQ =
                        await SharedStuff.Database.QueryAsync<DatabaseHelper.Messages>("SELECT * FROM Messages WHERE Username == ? ORDER BY ID DESC LIMIT 1",users[i].Username);
                    if (lastMsgQ.Count > 0)
                    {
                        var lastMsg = lastMsgQ[0];
                        string msg = lastMsg.Type == 0 ? lastMsg.Payload :
                            (await SharedStuff.Database.Table<DatabaseHelper.Files>()
                                    .Where(file => file.Token == lastMsg.Payload).FirstAsync()).Name;
                        msgs[i] = new MainMessagesNotify
                        {
                            Type = (MessageType)lastMsg.Type,
                            Username = users[i].Username,
                            IsLastMessageForUser = lastMsg.MyMessage,
                            Message = msg,
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
            // connect to server and register the websocket
            await Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        SharedStuff.Websocket = new WebSocket("wss://" + Properties.Settings.Default.ServerAddress + "/chat/registerUpdater");
                        SharedStuff.Websocket.OnMessage += WS_OnMessage;
                        if (Properties.Settings.Default.TrustInvalidSSL)
                            SharedStuff.Websocket.SslConfiguration.ServerCertificateValidationCallback =
                                (a, certificate, chain, sslPolicyErrors) => true;
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
                var status = JsonConvert.DeserializeObject<JsonTypes.MessageStatus>(e.Data);
                if (status.Id != null) // a real message status
                {
                    if(!status.Ok)
                        Console.WriteLine("Error on message " + status.Id + ": " + status.Message);
                    if (status.Message != "sent") // this is a file token update
                    {
                        SharedStuff.PendingMessages[status.Id].Token = status.Message;
                        // now start upload
                        SharedStuff.UploadFile(status.Id);
                    }
                    else
                    {
                        SharedStuff.PendingMessages[status.Id].Sent = status.Ok ? (byte) 0 : (byte) 2;
                        // Do not remove files from here; They are removed at "finally" block of SharedStuff.UploadFile
                        if(SharedStuff.PendingMessages[status.Id].Type != 1)
                            SharedStuff.PendingMessages.Remove(status.Id);
                    }
                    return;
                }

                if (status.Message != null && !status.Ok)
                {
                    // TODO: HANDLE THIS SHIT
                    return;
                }
            }
            catch (Exception)
            {
                // ignored : the type might be something else
            }
            // try ClientUpdateTypeStruct
            await _mu.WaitAsync(); // get the data in order that server sends them
            string nameSafe = "";
            try
            {
                var msg = JsonConvert.DeserializeObject<JsonTypes.Updates>(e.Data);
                var keyList = await SharedStuff.Database.Table<DatabaseHelper.Users>().Where(k => k.Username == msg.Payload.From)
                    .ToArrayAsync(); // get the key of the user
                string key;
                if (keyList.Length == 0) // Key agreement
                {
                    var data = await SharedStuff.GetUserData(msg.Payload.From);
                    key = Convert.ToBase64String(SharedStuff.Curve.calculateAgreement(
                        Convert.FromBase64String(_savedData.PrivateKey),
                        Convert.FromBase64String(data.PublicKey)));
                    await SharedStuff.Database.InsertAsync(new DatabaseHelper.Users // save the key in database
                    {
                        Username = data.Username,
                        Name = data.Name,
                        Key = key,
                    });
                    nameSafe = data.Name;
                }
                else // get the key from database; There is only one row because Username is unique
                    key = keyList[0].Key;

                string toShowOnMainMenu = "";
                switch (msg.Type)
                {
                    case 0: // Text message
                        // decrypt the message or assign the file token
                        try
                        {
                            string message = BouncyCastleHelper.AesGcmDecrypt(msg.Payload.Message, key);
                            await SharedStuff.Database.InsertAsync(new DatabaseHelper.Messages
                            {
                                Username = msg.Payload.From,
                                Type = msg.Type,
                                Date = msg.Payload.Date,
                                Payload = message
                            });
                            toShowOnMainMenu = message;
                        }
                        catch (Exception)
                        {
                            // TODO: RE-REQUEST public key
                        }
                        break;
                    case 1: // File message
                        //TODO: Fill here
                        var filePayload = JsonConvert.DeserializeObject<JsonTypes.FilePayload>(msg.Payload.Message);
                        await SharedStuff.Database.RunInTransactionAsync(trans =>
                        {
                            trans.Insert(new DatabaseHelper.Messages
                            {
                                Username = msg.Payload.From,
                                Type = msg.Type,
                                Date = msg.Payload.Date,
                                Payload = filePayload.Token
                            });
                            trans.Insert(new DatabaseHelper.Files
                            {
                                Token = filePayload.Token,
                                Name = filePayload.FileName
                            });
                        });
                        toShowOnMainMenu = filePayload.FileName;
                        break;
                }

                // save the value
                bool inserted = false, open = OpenWindowsList.ContainsKey(msg.Payload.From);
                int index = -1;
                try
                {
                    index = MessagesList.IndexOf(MessagesList.First(x =>
                        x.Username == msg.Payload.From)); // get index of the UI
                    if (index == -1)
                        throw new Exception();
                }
                catch (Exception)
                {
                    string name = nameSafe;
                    if(name == "")
                        name = (await SharedStuff.GetUserData(msg.Payload.From)).Name;
                    Application.Current.Dispatcher.Invoke(delegate 
                    {
                        MessagesList.Insert(0,new MainMessagesNotify
                        {
                            FullDate = msg.Payload.Date,
                            Message = toShowOnMainMenu,
                            IsLastMessageForUser = false,
                            Name = name,
                            NewMessages = 1,
                            Username = msg.Payload.From
                        });
                    });
                    inserted = true;
                }
                if (index == -1)
                    return;

                if (!open) // increase unread messages
                    await SharedStuff.Database.ExecuteAsync("UPDATE Users SET UnreadMessages = ? WHERE Username = ?",
                        MessagesList[index].NewMessages + 1, msg.Payload.From);
                if (!inserted)
                {
                    Application.Current.Dispatcher.Invoke(delegate
                    {
                        // Show the user the update
                        MessagesList.Move(index, 0);
                        MessagesList[0].FullDate = msg.Payload.Date;
                        MessagesList[0].Message = toShowOnMainMenu;
                        MessagesList[0].IsLastMessageForUser = false;
                        if (!open)
                            MessagesList[0].NewMessages++;
                    });
                }

                // update the open windows
                if (open)
                    OpenWindowsList[msg.Payload.From].AddMessage(false, toShowOnMainMenu, msg.Payload.Date, msg.Type);
            }
            catch (Exception ex)
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
            string username = "",name = "";
            byte delegateResult = 0; // 0 is running, 1 is done, 2 is canceled
            Exception error = null;
            await DialogHost.Show(dialog,"MainWindowDialogHost",async delegate(object sender1, DialogClosingEventArgs args)
            {
                if (!(bool) args.Parameter)
                {
                    delegateResult = 2;
                    // check if the dialog is canceled
                    return;
                }
                username = dialog.IdTextBox.Text;
                // check if the ID exists in database
                var dbResult = await SharedStuff.Database.Table<DatabaseHelper.Users>().Where(user => user.Username == username)
                    .ToArrayAsync();
                if (dbResult.Length == 0) // get the user's key from server
                {
                    NameValueCollection queryString = HttpUtility.ParseQueryString(string.Empty);
                    queryString.Add("username", username);
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
                        delegateResult = 1;
                        return;
                    }
                    // check if the username exists
                    try
                    {
                        var data = JsonConvert.DeserializeObject<JsonTypes.UserDataStruct>(result);
                        if (data.PublicKey == "")
                            throw new Exception("User is not logged in.");
                        string key = Convert.ToBase64String(SharedStuff.Curve.calculateAgreement(
                            Convert.FromBase64String(_savedData.PrivateKey),
                            Convert.FromBase64String(data.PublicKey)));
                        await SharedStuff.Database.InsertAsync(
                            new DatabaseHelper.Users // save the key in database
                            {
                                Username = data.Username,
                                Name = data.Name,
                                Key = key,
                            });
                        name = data.Name;
                    }
                    catch (Exception)
                    {
                        var data = JsonConvert.DeserializeObject<JsonTypes.ServerStatus>(result);
                        error = new Exception("Server returned an error", new Exception(data.Message));
                    }
                    finally
                    {
                        delegateResult = 1;
                    }
                }
            });
            // wait until the results are gathered
            while (delegateResult == 0)
                await Task.Delay(50);
            if (delegateResult == 2)
                return;
            if (error != null)
            {
                var errDialog = new ErrorDialogSample
                {
                    ErrorTitle = {Text = error.Message},
                    ErrorText = {Text = error.InnerException.Message}
                };
                await DialogHost.Show(errDialog,"MainWindowDialogHost");
                return;
            }
            // check if messages list does not contain this user
            if (MessagesList.All(x => x.Username != username))
            {
                MessagesList.Insert(0, new MainMessagesNotify
                {
                    FullDate = DateTime.Now,
                    IsLastMessageForUser = false,
                    Message = "",
                    Name = name,
                    Username = username
                });
            }
            // open the chat page
            OpenWindowsList.Add(username, new ChatWindow(username));
            OpenWindowsList[username].Show();
        }
        private void MainChatsWindow_OnClosing(object sender, CancelEventArgs e)
        {
            SharedStuff.Websocket.Close(1000);
            Application.Current.Shutdown();
        }

        private void MainChatsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainChatsList.SelectedItem != null)
            {
                MainMessagesNotify selectedChat = MainChatsList.SelectedItem as MainMessagesNotify;
                MainChatsList.SelectedItem = null; // select nothing
                if(selectedChat == null)
                    return;
                // open the chat window
                if (!OpenWindowsList.ContainsKey(selectedChat.Username))
                {
                    OpenWindowsList.Add(selectedChat.Username, new ChatWindow(selectedChat.Username));
                    OpenWindowsList[selectedChat.Username].Show();
                    // reset new messages
                    MessagesList.First(x => x == selectedChat).NewMessages = 0;
                    SharedStuff.Database.ExecuteAsync("UPDATE Users SET UnreadMessages = ? WHERE Username = ?",
                        0,selectedChat.Username);
                }
                else
                    OpenWindowsList[selectedChat.Username].Activate();
            }
        }
    }
    
}
