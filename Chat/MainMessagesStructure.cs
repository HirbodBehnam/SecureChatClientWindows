﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using MaterialDesignThemes.Wpf;

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
                if (Type == MessageType.Text)
                    _message = value.Replace(Environment.NewLine, ""); //remove new lines
                else
                    _message = "File: " + value;
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
                OnPropertyChanged("Date");
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
                OnPropertyChanged("SmallCircle");
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
                OnPropertyChanged("LastSender");
            }
        }
        /// <summary>
        /// The username of the user; This is used for selections
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// File type
        /// </summary>
        public MessageType Type { get; set; }

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
        private string _message,_token;
        private DateTime _date;
        private byte _type,_sent;
        private double _progress;
        private PackIconKind _downloadButtonIcon = PackIconKind.File;
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
        /// <summary>
        /// The type of message. 0 -> Text message, 1 -> File
        /// </summary>
        public byte Type
        {
            get => _type;
            set
            {
                if(_type == value)
                    return;
                _type = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// Gets if the text message type components must be visible or not
        /// </summary>
        public Visibility IsTextType => _type == 0 ? Visibility.Visible : Visibility.Collapsed;
        /// <summary>
        /// Gets if the file message type components must be visible or not
        /// </summary>
        public Visibility IsFileType => _type == 1 ? Visibility.Visible : Visibility.Collapsed;
        /// <summary>
        /// Sets the visibility of the progress bar for file upload and download
        /// </summary>
        public Visibility FileProgressBarEnabled => _progress < 100 ? Visibility.Visible : Visibility.Collapsed;
        /// <summary>
        /// Card alignment of the messages according to the message sent
        /// </summary>
        public HorizontalAlignment MessageAlignment => _myMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        /// <summary>
        /// 0 -> Sent, 1 -> Sending, 2-> Failed
        /// </summary>
        public byte Sent
        {
            get => _sent;
            set
            {
                if(value == _sent)
                    return;
                _sent = value;
                OnPropertyChanged("SentIconKind");
                OnPropertyChanged("SentIconVisibility");
            }
        }
        /// <summary>
        /// Gets if the sending icon must be visible or not
        /// </summary>
        public Visibility SentIconVisibility => _sent == 0 ? Visibility.Collapsed : Visibility.Visible;
        /// <summary>
        /// Get icon kind type of message status
        /// </summary>
        public PackIconKind SentIconKind => _sent == 1 ? PackIconKind.ProgressClock : PackIconKind.Error;

        /// <summary>
        /// Token of the file if <see cref="Type"/> is 0
        /// </summary>
        public string Token
        {
            get => _token;
            set
            {
                if (value == _token)
                    return;
                _token = value;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// The file location on computer
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// What should the progress bar show
        /// </summary>
        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
                OnPropertyChanged("FileProgressBarEnabled");
            }
        }

        /// <summary>
        /// The icon on the download button to show
        /// </summary>
        public PackIconKind DownloadButtonIcon
        {
            get => _downloadButtonIcon;
            set
            {
                if (value == _downloadButtonIcon)
                    return;
                _downloadButtonIcon = value;
                OnPropertyChanged();
                OnPropertyChanged("DownloadButtonTooltip");
            }
        }
        /// <summary>
        /// The tooltip of the file button according to <see cref="DownloadButtonIcon"/>
        /// </summary>
        public string DownloadButtonTooltip
        {
            get
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (DownloadButtonIcon)
                {
                    case PackIconKind.File:
                        return "Open File";
                    case PackIconKind.Download:
                        return "Download File";
                    case PackIconKind.DownloadOff:
                        return "File Unavailable";
                    default:
                        return "";
                }
            }
        }
        /// <summary>
        /// The username of the who this message is from (Only used in file type)
        /// </summary>
        public string With { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
