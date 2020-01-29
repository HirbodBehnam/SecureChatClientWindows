using System;
using System.Dynamic;

namespace Chat
{
    /// <summary>
    /// This class contains all json schemes that is used in client
    /// </summary>
    class JsonTypes
    {
        /// <summary>
        /// The login details to be saved
        /// </summary>
        public class SavedData
        {
            public string Username { get; set; }
            public string Password{ get; set; }
            public string PublicKey{ get; set; }
            public string PrivateKey{ get; set; }
        }
        /// <summary>
        /// Most used json class; All of the server responses (except chat updates) are like this
        /// </summary>
        public class ServerStatus
        {
            public bool Ok { get; set; }
            public string Message { get; set; }
        }
        /// <summary>
        /// This is sent with first packet of the websocket connection
        /// </summary>
        public class HelloMessage
        {
            /// <summary>
            /// The username
            /// </summary>
            public string Username { get; set; }
            /// <summary>
            /// Password, plain text
            /// </summary>
            public string Password { get; set; }
            /// <summary>
            /// Password signed with <see cref="curve25519"/>
            /// </summary>
            public string Verify { get; set; }
        }
        public class Updates
        {
            /// <summary>
            /// 0 -&gt; Text message / 1 -&gt; File / 2 -&gt; Handshake
            /// </summary>
            public byte Type { get; set; }
            /// <summary>
            /// Payload of the message
            /// </summary>
            public UpdatePayload Payload { get; set; }
        }
        public class UpdatePayload
        {
            /// <summary>
            /// The username of the sender
            /// </summary>
            public string From { get; set; }
            /// <summary>
            /// The time that this message is sent
            /// </summary>
            public DateTime Date { get; set; }
            /// <summary>
            /// The main message; If text message, AES encrypted of the message; If file the token of it
            /// </summary>
            public string Message { get; set; }
        }
        /// <summary>
        /// This is the user data that server return
        /// </summary>
        public class UserDataStruct
        {
            /// <summary>
            /// The name of the user 
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// Username of it
            /// </summary>
            public string Username { get; set; }
            /// <summary>
            /// X25519 public key in base64
            /// </summary>
            [Newtonsoft.Json.JsonProperty("public_key")]
            public string PublicKey { get; set; }
        }
        /// <summary>
        /// A type that when a message is going to be send, it's used
        /// </summary>
        public class SendMessage
        {
            /// <summary>
            /// 0 -&gt; Text message / 1 -&gt; File / 2 -&gt; Handshake
            /// </summary>
            public byte Type { get; set; }
            /// <summary>
            /// Payload of the message
            /// </summary>
            public SendMessagePayload Payload { get; set; }
        }
        /// <summary>
        /// Data of the message that will be sent
        /// </summary>
        public class SendMessagePayload
        {
            /// <summary>
            /// Username of the receiver
            /// </summary>
            public string To { get; set; }
            /// <summary>
            /// Token of the file, or encrypted message
            /// </summary>
            public string Message { get; set; }
        }
    }
}
