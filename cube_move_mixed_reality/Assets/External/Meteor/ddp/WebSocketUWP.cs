#if WINDOWS_UWP
using System;
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

using System.Threading.Tasks;
#endif

namespace Meteor.ddp
{
    public class WebSocketUWP : WebSocketConnection
    {
#if WINDOWS_UWP
        private MessageWebSocket messageWebSocket;
        private DataWriter messageWriter;

        public WebSocketUWP(DdpConnection ddpConnection)
        {
            this.ddpConnection = ddpConnection;
        }

        async public override Task Connect(Uri uri)
        {
            messageWebSocket = new MessageWebSocket();
            messageWebSocket.Control.MessageType = SocketMessageType.Utf8;
            messageWebSocket.MessageReceived += MessageReceived;
            messageWebSocket.Closed += Closed;
            await messageWebSocket.ConnectAsync(uri);
            messageWriter = new DataWriter(messageWebSocket.OutputStream);
        }

        private void MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args) 
        {
            using (DataReader reader = args.GetDataReader()) {
                reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                string read = reader.ReadString(reader.UnconsumedBufferLength);
                this.ddpConnection.HandleMessages(read);
            }
        }

        public void Closed(IWebSocket webSocket,  WebSocketClosedEventArgs args) {
            ddpConnection.Closed();
        }

        public override void Dispose()
        {
            messageWebSocket.Dispose();
        }

        public override async Task Send(string message) 
        {
            messageWriter.WriteString(message);
            await messageWriter.StoreAsync();
        }
#endif
    }
}