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
        private long _lastMessageIndex = 100;
        /// <summary>
        /// Check to see if the user has reached the end of it's messages
        /// </summary>
        private bool _reachedEnd = false;
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
                ("SELECT * FROM Messages WHERE Username == ? ORDER BY Id DESC LIMIT 100",Username); //TODO: Write a function with offset and... to fetch data from db
            if (messages.Count < 100) // prevent additional load on db
                _reachedEnd = true;
//            foreach (var message in messages)
//            {
//                MessagesList.Insert(0,new ChatMessagesNotify
//                {
//                    MyMessage = message.MyMessage,
//                    Message = message.Payload,
//                    FullDate = message.Date
//                });
//            }
            for (int i = 0; i < 30;i++)
            {
                MessagesList.Insert(0,new ChatMessagesNotify
                {
                    MyMessage = i % 2 == 0,
                    Message = i.ToString(),
                    FullDate = DateTime.Now
                });
            }

            MainScrollViewer.ScrollToBottom(); // Go to the last of scroll view that is actually the first of it
        }

        public void AddMessage()
        {

        }
        /// <summary>
        /// Use this method to get older messages when VerticalOffset reaches 0 (scrolled to top)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_reachedEnd && Math.Abs(e.VerticalOffset) < 1)
            {

            }
        }
    }
}
