using System;
using SQLite;

namespace Chat
{
    class DatabaseHelper
    {
        /// <summary>
        /// AES keys of the users
        /// </summary>
        public class Users
        {
            /// <summary>
            /// ID of the row
            /// </summary>
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            /// <summary>
            /// Username
            /// </summary>
            [Indexed]
            public string Username { get; set; }
            /// <summary>
            /// The number of unread messages; Will reset if user opens the chat
            /// </summary>
            public int UnreadMessages { get; set; }
            /// <summary>
            /// The name of the user
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// AES key in base64
            /// </summary>
            public string Key { get; set; }
        }
        /// <summary>
        /// How messages are saved in database. All users are saved into one database
        /// </summary>
        public class Messages
        {
            /// <summary>
            /// ID of the row
            /// </summary>
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            /// <summary>
            /// The username of the 
            /// </summary>
            [Indexed]
            public string Username { get; set; }
            /// <summary>
            /// True if the user is the sender, false if this is sent by another guy
            /// </summary>
            public bool MyMessage { get; set; }
            /// <summary>
            /// 0 -> Text message, 1 -> File
            /// </summary>
            public byte Type { get; set; }
            /// <summary>
            /// The date that this message is sent
            /// </summary>
            public DateTime Date { get; set; }
            /// <summary>
            /// If the it's text message, it's plain text of the message; Otherwise it's the token of the file
            /// </summary>
            public string Payload { get; set; }
        }
        /// <summary>
        /// This table holds of the downloaded files locations
        /// </summary>
        public class Files
        {
            /// <summary>
            /// ID of the row
            /// </summary>
            [PrimaryKey, AutoIncrement]
            public int Id { get; set; }
            /// <summary>
            /// The token of the file
            /// </summary>
            [Indexed]
            public string Token { get; set; }
            /// <summary>
            /// The location of the file
            /// </summary>
            public string Location { get; set; }
        }
        /// <summary>
        /// Returns a query where selects a number of messages of a username (Username is defined by ?)
        /// </summary>
        /// <param name="from">From what message (0 is last message)</param>
        /// <param name="count">How many messages?</param>
        /// <returns>A query</returns>
        public static string LastRowsQuery(int from, int count)
        {
            return "SELECT * FROM Messages WHERE Username == ? ORDER BY Id DESC LIMIT " + from + "," + count;
        }
    }
}
