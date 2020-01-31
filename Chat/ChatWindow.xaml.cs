﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Chat
{
    public partial class ChatWindow : Window
    {
        /// <summary>
        /// We load each 100 messages at once; Save the last loaded message. Add one to this value on every new message to increase the offset. Also add 100 to it on scrolling up
        /// </summary>
        private int _lastMessageIndex = 100;
        /// <summary>
        /// Check to see if the user has reached the end of it's messages
        /// </summary>
        private bool _reachedEnd;
        /// <summary>
        /// A small variable to control when to load messages from database
        /// </summary>
        private bool _stopLoading = true;
        /// <summary>
        /// The username of the user that the client is chatting with
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// Messages of this user
        /// </summary>
        public ObservableCollection<ChatMessagesNotify> MessagesList { get; set; } = new ObservableCollection<ChatMessagesNotify>();
        public ChatWindow(string username)
        {
            InitializeComponent();
            Username = username;
            WindowDialogHost.Identifier = "ChatDialogHost" + Username; // setup the DialogHost
            Title = Username; // just a temporary value. Get the name of user on Page_OnLoaded
        }

        private async void Page_OnLoaded(object sender, RoutedEventArgs e)
        {
            // load name of the user
            Title = "Chatting with " + (await SharedStuff.Database.Table<DatabaseHelper.Users>().Where(row => row.Username == Username)
                .FirstAsync()).Name;
            // load fist 100 messages
            var messages = await SharedStuff.Database.QueryAsync<DatabaseHelper.Messages>
                (DatabaseHelper.LastRowsQuery(0,100),Username);
            if (messages.Count < 100) // prevent additional load on db
                _reachedEnd = true;
            foreach (var message in messages)
            {
                MessagesList.Insert(0,new ChatMessagesNotify
                {
                    MyMessage = message.MyMessage,
                    Message = message.Payload,
                    FullDate = message.Date,
                    Type = message.Type,
                    Sent = 0
                });
            }

            MainScrollViewer.ScrollToBottom(); // Go to the last of scroll view that is actually the first of it
            MainScrollViewer.UpdateLayout();
            _stopLoading = false;
        }
        private async void SendBtnClicked(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text.Trim();
            string id = Guid.NewGuid().ToString();
            await Task.Run(() => SendMessage(message,id));
            var msgUi = new ChatMessagesNotify
            {
                MyMessage = true,
                Message = message,
                FullDate = DateTime.Now,
                Type = 0,
                Sent = 1
            };
            AddMessage(msgUi); // add it to ui
            SharedStuff.NotSentMessages.Add(id,msgUi); // add message to pending messages
            // finalizing UI
            MessageTextBox.Text = "";
            MessageTextBox.Focus();
            _stopLoading = true;
            MainScrollViewer.ScrollToBottom();
            MainScrollViewer.UpdateLayout();
            _stopLoading = false;
        }
        private async void MessageTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                if (Keyboard.IsKeyDown(Key.Enter))
                {
                    string message = MessageTextBox.Text.Trim().TrimEnd( Environment.NewLine.ToCharArray());
                    string id = Guid.NewGuid().ToString();
                    await Task.Run(() => SendMessage(message,id)); // send message over network
                    var msgUi = new ChatMessagesNotify
                    {
                        MyMessage = true,
                        Message = message,
                        FullDate = DateTime.Now,
                        Type = 0,
                        Sent = 1
                    };
                    AddMessage(msgUi); // add it to ui
                    SharedStuff.NotSentMessages.Add(id,msgUi); // add message to pending messages
                    // finalizing UI
                    MessageTextBox.Text = "";
                    MessageTextBox.Focus();
                    _stopLoading = true;
                    MainScrollViewer.ScrollToBottom();
                    MainScrollViewer.UpdateLayout();
                    _stopLoading = false;
                }
        }
        /// <summary>
        /// Sends the outgoing message
        /// </summary>
        /// <param name="message">The message string</param>
        /// <param name="id">The message string</param>
        public async void SendMessage(string message,string id)
        {
            // do not send the message if it's empty
            if(string.IsNullOrWhiteSpace(message))
                return;
            // encrypt the message with other user's key
            string encryptedMessage;
            {
                string key = (await SharedStuff.Database.Table<DatabaseHelper.Users>().Where(user => user.Username == Username)
                    .FirstAsync()).Key;
                encryptedMessage = BouncyCastleHelper.AesGcmEncrypt(message, key);
            }
            // create json
            string json = JsonConvert.SerializeObject(new JsonTypes.SendMessage
            {
                Type = 0,
                Id = id,
                Payload = new JsonTypes.SendMessagePayload
                {
                    To = Username,
                    Message = encryptedMessage
                }
            });
            // save this value in 
            // send the json
            SharedStuff.Websocket.SendAsync(json, ok =>
            {
                // todo: what is this shit?
            });
            // save it into database
            await SharedStuff.Database.InsertAsync(new DatabaseHelper.Messages{
                Date = DateTime.Now,
                MyMessage = true,
                Payload = message,
                Type = 0,
                Username = Username
            });
            // add to main window
            Application.Current.Dispatcher.Invoke(delegate
            {
                var u = MainChatsWindow.MessagesList.First(x => x.Username == Username);
                u.Message = message;
                u.IsLastMessageForUser = true;
            });
        }
        public void AddMessage(ChatMessagesNotify message)
        {
            _lastMessageIndex++;
            Application.Current.Dispatcher.Invoke(delegate
            {
                MessagesList.Add(message);
            });
            if (!message.MyMessage)
            {
                _stopLoading = true;
                // scroll to bottom if needed
                MainScrollViewer.UpdateLayout();
                if (MainScrollViewer.ScrollableHeight - MainScrollViewer.VerticalOffset < 30)
                {
                    MainScrollViewer.ScrollToBottom();
                    MainScrollViewer.UpdateLayout();
                }
                _stopLoading = false;
            }
        }
        /// <summary>
        /// Adds a message to UI
        /// </summary>
        /// <param name="myMessage">Is this incoming message or not</param>
        /// <param name="data">The data of message (plain string or file token)</param>
        /// <param name="date">The date this message has been received</param>
        /// <param name="type">0 -> String message, 1 -> File token</param>
        public void AddMessage(bool myMessage,string data,DateTime date,byte type)
        {
            _lastMessageIndex++;
            Application.Current.Dispatcher.Invoke(delegate
            {
                MessagesList.Add(new ChatMessagesNotify
                {
                    MyMessage = myMessage,
                    Message = data,
                    FullDate = date,
                    Type = type,
                    Sent = myMessage ? (byte)1 : (byte)0
                });
            });
            if (!myMessage)
            {
                _stopLoading = true;
                // scroll to bottom if needed
                MainScrollViewer.UpdateLayout();
                if (MainScrollViewer.ScrollableHeight - MainScrollViewer.VerticalOffset < 30)
                {
                    MainScrollViewer.ScrollToBottom();
                    MainScrollViewer.UpdateLayout();
                }
                _stopLoading = false;
            }
        }
        /// <summary>
        /// Use this method to get older messages when VerticalOffset reaches 0 (scrolled to top)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MainScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_stopLoading && !_reachedEnd && Math.Abs(e.VerticalOffset) < 1) // load more messages
            {
                _stopLoading = true;
                // get last height
                double lastHeight = MainScrollViewer.ScrollableHeight;
                var messages = await SharedStuff.Database.QueryAsync<DatabaseHelper.Messages>
                    (DatabaseHelper.LastRowsQuery(_lastMessageIndex,100),Username);
                _lastMessageIndex += 100;
                // check if we have reached the end
                if (messages.Count < 100)
                    _reachedEnd = true;
                if (messages.Count == 0)
                    return;
                // insert these messages at the end of the list view (that is first of the collation)
                foreach (var message in messages)
                {
                    MessagesList.Insert(0, new ChatMessagesNotify
                    {
                        MyMessage = message.MyMessage,
                        Message = message.Payload,
                        FullDate = message.Date,
                        Type = message.Type
                    });
                }
                MainScrollViewer.UpdateLayout();
                MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.ScrollableHeight - lastHeight); // tricky part; calculate the delta of height adding and go to that position from top
                MainScrollViewer.UpdateLayout();
                _stopLoading = false;
            }
            
        }
        private void ChatWindow_OnClosed(object sender, EventArgs e)
        {
            MainChatsWindow.OpenWindowsList.Remove(Username);
        }

        private void ChatTextCopyClicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
                if(item.CommandParameter is string msg)
                    Clipboard.SetText(msg);
        }
    }
}
