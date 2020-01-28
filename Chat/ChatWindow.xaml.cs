using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SQLite;

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
        private bool _reachedEnd = false;
        /// <summary>
        /// A small variable to control when to load messages from database
        /// </summary>
        private bool _stopLoading = true;
        /// <summary>
        /// 
        /// </summary>
        private int _timesDbLoaded = 1;
        /// <summary>
        /// The username of the user that the client is chatting with
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// Messages of this user
        /// </summary>
        public ObservableCollection<ChatMessagesNotify> MessagesList { get; set; } = new ObservableCollection<ChatMessagesNotify>();
        public ChatWindow()
        {
            // TODO: REMOVE
            {
                Username = "dw";
                SharedStuff.Database = new SQLiteAsyncConnection("database.db");
            }
            InitializeComponent();
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
                    Type = message.Type
                });
            }

            MainScrollViewer.ScrollToBottom(); // Go to the last of scroll view that is actually the first of it
            MainScrollViewer.UpdateLayout();
            _stopLoading = false;
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
            MessagesList.Add(new ChatMessagesNotify
            {
                MyMessage = myMessage,
                Message = data,
                FullDate = date,
                Type = type
            });
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
                //TODO: FIND BETTER WAY
                MainScrollViewer.UpdateLayout();
                MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.ScrollableHeight / ++_timesDbLoaded);
                MainScrollViewer.UpdateLayout();
                _stopLoading = false;
            }
            
        }
    }
}
