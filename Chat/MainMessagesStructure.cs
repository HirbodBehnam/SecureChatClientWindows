using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Chat
{
    /// <summary>
    /// Some shit I find here https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit/blob/master/MainDemo.Wpf/Domain/SelectableViewModel.cs
    /// </summary>
    public class MainMessagesNotify : INotifyPropertyChanged
    {
        private string _name,_message;
        private int _messagesCount;
        private bool _lastMessageForUser;
        private DateTime _date;
        /// <summary>
        /// Name of the one who messaged them
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The last message of the user
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                if (_message == value)
                    return;
                _message = value.Replace(System.Environment.NewLine, ""); //remove new lines
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// What must be shown in the small circle; If there is new message, return the first letter of the Name
        /// If new messages count is less than 10, return them, otherwise return "+9"
        /// </summary>
        public string SmallCircle
        {
            get
            {
                if (_messagesCount == 0)
                    return Name[0].ToString().ToUpper();
                return _messagesCount < 10 ? _messagesCount.ToString() : "+9";
            }
        }

        /// <summary>
        /// Gets the last sender name based on <see cref="IsLastMessageForUser"/>
        /// </summary>
        // there is a space before You to just match the paddings of the Run element
        public string LastSender => _lastMessageForUser ? " You:" : "";
        /// <summary>
        /// Get or set the date of the message in <see cref="DateTime"/> type
        /// </summary>
        public DateTime FullDate
        {
            get => _date;
            set
            {
                if (_date == value)
                    return;
                _date = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Get the time that message is received in string. If it's older than one day, show the dd/mm/yy; otherwise show time
        /// </summary>
        public string Date => _date.ToString((DateTime.Now - _date).Days >= 1 ? "d" : "t");

        /// <summary>
        /// The number of new messages
        /// </summary>
        public int NewMessages {
            get => _messagesCount;
            set
            {
                if (_messagesCount == value)
                    return;
                _messagesCount = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Sets if the last message is for user
        /// </summary>
        public bool IsLastMessageForUser
        {
            get => _lastMessageForUser;
            set
            {
                if (_lastMessageForUser == value)
                    return;
                _lastMessageForUser = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The username of the user; This is used for selections
        /// </summary>
        public string Username { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    /// <summary>
    /// This class is used in main chat window, bind to a list
    /// </summary>
    public class ChatMessagesNotify : INotifyPropertyChanged
    {
        private bool _myMessage;
        private string _message;
        private DateTime _date;
        /// <summary>
        /// True if the user sent this message; If it's an incoming message it's false
        /// </summary>
        public bool MyMessage
        {
            get => _myMessage;
            set
            {
                if(_myMessage == value)
                    return;
                _myMessage = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The main message text
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                if(_message == value)
                    return;
                _message = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Get or set the date of the message in <see cref="DateTime"/> type
        /// </summary>
        public DateTime FullDate
        {
            get => _date;
            set
            {
                if (_date == value)
                    return;
                _date = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Get the time that message is received in string
        /// </summary>
        public string Date => _date.ToString("G");
//        public Brush CardColor => new SolidColorBrush(_myMessage ? Color.FromRgb(255,209,128) : Color.FromRgb(66,66,66));
        /// <summary>
        /// Card alignment of the messages according to the message sent
        /// </summary>
        public HorizontalAlignment MessageAlignment => _myMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
