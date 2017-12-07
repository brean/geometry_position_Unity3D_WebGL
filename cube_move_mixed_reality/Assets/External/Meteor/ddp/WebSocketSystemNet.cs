#if !WINDOWS_UWP
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Meteor.ddp
{
    public class WebSocketSystemNet : WebSocketConnection
    {
#if !WINDOWS_UWP
        private ClientWebSocket webSocket;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public WebSocketSystemNet(DdpConnection ddpConnection)
        {
            this.ddpConnection = ddpConnection;
        }

        async public override Task Connect(Uri uri)
        {
            webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(uri, cts.Token);
            await Task.Factory.StartNew(
                async () =>
                {
                    ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                    while (true)
                    {
                        WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, cts.Token);
                        if (webSocket.State != WebSocketState.Open)
                        {
                            break;
                        }
                        string json = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        ddpConnection.HandleMessages(json);
                    }
                }, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            // TODO: ddpConnection.Closed();
        }

        public override void Dispose()
        {
            webSocket.Dispose();
        }

        async public override Task Send(string message)
        {
            Byte[] bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, cts.Token);
        }
#endif
    }
}