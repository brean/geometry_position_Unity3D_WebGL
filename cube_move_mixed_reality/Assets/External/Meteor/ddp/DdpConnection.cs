/*
	The MIT License (MIT)

	Copyright (c) 2016 Vincent Cantin (user "green-coder" on Github.com)
	Copyright (c) 2017 Andreas Bresser

	Permission is hereby granted, free of charge, to any person obtaining a copy of
	this software and associated documentation files (the "Software"), to deal in
	the Software without restriction, including without limitation the rights to
	use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
	of the Software, and to permit persons to whom the Software is furnished to do
	so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

#if WINDOWS_UWP
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine; // for Debug.Log
using Windows.Data.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Threading.Tasks;

namespace Meteor.ddp {
    public class DdpConnection : IDisposable {

        // The possible values for the "msg" field.
        public class MessageType {
            // Client -> server.
            public const string CONNECT = "connect";
            public const string PONG = "pong";
            public const string SUB = "sub";
            public const string UNSUB = "unsub";
            public const string METHOD = "method";

            // Server -> client.
            public const string CONNECTED = "connected";
            public const string FAILED = "failed";
            public const string PING = "ping";
            public const string NOSUB = "nosub";
            public const string ADDED = "added";
            public const string CHANGED = "changed";
            public const string REMOVED = "removed";
            public const string READY = "ready";
            public const string ADDED_BEFORE = "addedBefore";
            public const string MOVED_BEFORE = "movedBefore";
            public const string RESULT = "result";
            public const string UPDATED = "updated";
            public const string ERROR = "error";
        }

        // Field names supported in the DDP protocol.
        public class Field {
            public const string SERVER_ID = "server_id";
            public const string MSG = "msg";
            public const string SESSION = "session";
            public const string VERSION = "version";
            public const string SUPPORT = "support";

            public const string NAME = "name";
            public const string PARAMS = "params";
            public const string SUBS = "subs";
            public const string COLLECTION = "collection";
            public const string FIELDS = "fields";
            public const string CLEARED = "cleared";
            public const string BEFORE = "before";

            public const string ID = "id";
            public const string METHOD = "method";
            public const string METHODS = "methods";
            public const string RANDOM_SEED = "randomSeed"; // unused
            public const string RESULT = "result";

            public const string ERROR = "error";
            public const string REASON = "reason";
            public const string DETAILS = "details";
            public const string MESSAGE = "message";   // undocumented
            public const string ERROR_TYPE = "errorType"; // undocumented
            public const string OFFENDING_MESSAGE = "offendingMessage";
        }

        public enum ConnectionState {
            NOT_CONNECTED,
            CONNECTING,
            CONNECTED,
            DISCONNECTED,
            CLOSING,
            CLOSED
        }


        private Dictionary<string, Subscription> subscriptions = new Dictionary<string, Subscription>();
        private Dictionary<string, MethodCall> methodCalls = new Dictionary<string, MethodCall>();

        private int subscriptionId;
        private int methodCallId;

        public delegate void OnConnectedDelegate(DdpConnection connection);
        public delegate void OnDisconnectedDelegate(DdpConnection connection);
        public delegate void OnConnectionClosedDelegate(DdpConnection connection);
        public delegate void OnAddedDelegate(string collection, string docId, JsonObject fields);
        public delegate void OnChangedDelegate(string collection, string docId, JsonObject fields);
        public delegate void OnRemovedDelegate(string collection, string docId);
        public delegate void OnAddedBeforeDelegate(string collection, string docId, JsonObject fields, string before);
        public delegate void OnMovedBeforeDelegate(string collection, string docId, string before);
        public delegate void OnErrorDelegate(DdpError error);

        public event OnConnectedDelegate OnConnected;
        public event OnDisconnectedDelegate OnDisconnected;
        public event OnConnectionClosedDelegate OnConnectionClosed;
        public event OnAddedDelegate OnAdded;
        public event OnChangedDelegate OnChanged;
        public event OnRemovedDelegate OnRemoved;
        public event OnAddedBeforeDelegate OnAddedBefore;
        public event OnMovedBeforeDelegate OnMovedBefore;
        public event OnErrorDelegate OnError;

        // The DDP protocol version implemented by this library.
        public const string DDP_PROTOCOL_VERSION = "1";

        private ConnectionState ddpConnectionState = ConnectionState.NOT_CONNECTED;

        private string sessionId;

        private bool logMessages = true;

        // WebSocket provided by UWP framework
        private MessageWebSocket messageWebSocket;
        private DataWriter messageWriter;

        private Uri uri;

        public DdpConnection(string url) {
            uri = new Uri(url);
        }

        // run StartConnect async
        public void Connect() {
            Task.Run(() => StartConnect());
        }

        private async void StartConnect() {
            messageWebSocket = new MessageWebSocket();
            messageWebSocket.Control.MessageType = SocketMessageType.Utf8;
            messageWebSocket.MessageReceived += MessageReceived;
            messageWebSocket.Closed += Closed;
            await messageWebSocket.ConnectAsync(uri);
            messageWriter = new DataWriter(messageWebSocket.OutputStream);
            Send(GetConnectMessage());
        }

        private void OnConnectedFirst() {
            //Send("{\"msg\":\"sub\",\"id\":\"0\",\"name\":\"meteor_autoupdate_clientVersions\",\"params\":[]}");
            foreach (Subscription subscription in subscriptions.Values) {
                Send(GetSubscriptionMessage(subscription));
            }
            foreach (MethodCall methodCall in methodCalls.Values) {
                Send(GetMethodCallMessage(methodCall));
            }
        }

        private void HandleMessages(JsonObject message) {
            if (!message.ContainsKey(Field.MSG)) {
                // silently ignore messages that don't have the MSG-key
                return;
            }
            string msg = message.GetNamedValue(Field.MSG).GetString();
            switch (msg) {
                case MessageType.CONNECTED: {
                    sessionId = message.GetNamedValue(Field.SESSION).GetString();
                    ddpConnectionState = ConnectionState.CONNECTED;
                    OnConnectedFirst();
                    if (OnConnected != null) {
                        OnConnected(this);
                    }
                    break;
                }
                case MessageType.FAILED: {
                    if (OnError != null) {
                        OnError(new DdpError() {
                            errorCode = "Connection refused",
                            reason = "The server is using an unsupported DDP protocol version: " +
                                message.GetNamedValue(Field.VERSION).GetString()
                        });
                    }
                    //Dispose();
                    break;
                }
                case MessageType.PING: {
                    if (message.ContainsKey(Field.ID)) {
                        Send(GetPongMessage(message.GetNamedValue(Field.ID).GetString()));
                    } else {
                        Send(GetPongMessage());
                    }
                    break;
                }

                case MessageType.NOSUB: {
                    string subscriptionId = message.GetNamedValue(Field.ID).GetString();
                    subscriptions.Remove(subscriptionId);

                    if (message.ContainsKey(Field.ERROR)) {
                        if (OnError != null) {
                            OnError(GetError(message.GetNamedValue(Field.ID).GetObject()));
                        }
                    }
                    break;
                }
                case MessageType.ADDED: {
                    if (OnAdded != null) {
                        OnAdded(
                            message.GetNamedValue(Field.COLLECTION).GetString(),
                            message.GetNamedValue(Field.ID).GetString(),
                            message.GetNamedValue(Field.FIELDS).GetObject());
                    }
                    break;
                }
                case MessageType.CHANGED: {
                    if (OnChanged != null) {
                        OnChanged(
                            message.GetNamedValue(Field.COLLECTION).GetString(),
                            message.GetNamedValue(Field.ID).GetString(),
                            message.GetNamedValue(Field.FIELDS).GetObject());
                    }
                    break;
                }

                case MessageType.REMOVED: {
                    if (OnRemoved != null) {
                        OnRemoved(
                            message.GetNamedValue(Field.COLLECTION).GetString(),
                            message.GetNamedValue(Field.ID).GetString());
                    }
                    break;
                }

                case MessageType.READY: {
                    JsonArray subscriptionIds = message.GetNamedArray(Field.SUBS);

                    foreach (var item in subscriptionIds) {
                        string subscriptionId = item.GetString();
                        Subscription subscription = subscriptions[subscriptionId];
                        if (subscription != null) {
                            subscription.isReady = true;
                            if (subscription.OnReady != null) {
                                subscription.OnReady(subscription);
                            }
                        }
                    }
                    break;
                }

                case MessageType.ADDED_BEFORE: {
                    if (OnAddedBefore != null) {
                        OnAddedBefore(
                            message.GetNamedValue(Field.COLLECTION).GetString(),
                            message.GetNamedValue(Field.ID).GetString(),
                            message.GetNamedValue(Field.FIELDS).GetObject(),
                            message.GetNamedValue(Field.BEFORE).GetString());
                    }
                    break;
                }

                case MessageType.MOVED_BEFORE: {
                    if (OnMovedBefore != null) {
                        OnMovedBefore(
                            message.GetNamedValue(Field.COLLECTION).GetString(),
                            message.GetNamedValue(Field.ID).GetString(),
                            message.GetNamedValue(Field.BEFORE).GetString());
                    }
                    break;
                }

                case MessageType.RESULT: {
                    string methodCallId = message.GetNamedValue(Field.ID).GetString();
                    MethodCall methodCall = methodCalls[methodCallId];
                    if (methodCall != null) {
                        if (message.ContainsKey(Field.ERROR)) {
                            methodCall.error = GetError(message.GetNamedValue(Field.ERROR).GetObject());
                        }
                        methodCall.result = message.GetNamedValue(Field.RESULT).GetObject();
                        if (methodCall.hasUpdated) {
                            methodCalls.Remove(methodCallId);
                        }
                        methodCall.hasResult = true;
                        if (methodCall.OnResult != null) {
                            methodCall.OnResult(methodCall);
                        }
                    }
                    break;
                }

                case MessageType.UPDATED: {
                    JsonArray methodCallIds = message.GetNamedArray(Field.METHODS);

                    foreach (var item in methodCallIds) {
                        string methodCallId = item.GetString();
                        MethodCall methodCall = methodCalls[methodCallId];
                        if (methodCall != null) {
                            if (methodCall.hasResult) {
                                methodCalls.Remove(methodCallId);
                            }
                            methodCall.hasUpdated = true;
                            if (methodCall.OnUpdated != null) {
                                methodCall.OnUpdated(methodCall);
                            }
                        }
                    }
                    break;
                }

                case MessageType.ERROR: {
                    if (OnError != null) {
                        OnError(GetError(message));
                    }
                    break;
                }
            }
        }

        private string GetConnectMessage() {
            JsonObject message = new JsonObject();
            message.SetNamedValue(Field.MSG, JsonValue.CreateStringValue(MessageType.CONNECT));
            if (sessionId != null) {
                message.SetNamedValue(Field.SESSION, JsonValue.CreateStringValue(sessionId));
            }
            message.SetNamedValue(Field.VERSION, JsonValue.CreateStringValue(DDP_PROTOCOL_VERSION));

            JsonArray supportedVersions = new JsonArray();
            supportedVersions.Add(JsonValue.CreateStringValue(DDP_PROTOCOL_VERSION));
            //supportedVersions.Add(JsonValue.CreateStringValue("pre2"));
            //supportedVersions.Add(JsonValue.CreateStringValue("pre1"));
            message.SetNamedValue(Field.SUPPORT, supportedVersions);

            return message.Stringify();
        }

        private string GetPongMessage() {
            JsonObject message = new JsonObject();
            message.SetNamedValue(Field.MSG, JsonValue.CreateStringValue(MessageType.PONG));

            return message.Stringify();
        }
        
        private string GetPongMessage(string id) {
            JsonObject message = new JsonObject();
            message.SetNamedValue(Field.MSG, JsonValue.CreateStringValue(MessageType.PONG));
            message.SetNamedValue(Field.ID, JsonValue.CreateStringValue(id));

            return message.Stringify();
        }

        private string GetSubscriptionMessage(Subscription subscription) {
            JsonObject message = new JsonObject();
            message.SetNamedValue(Field.MSG, JsonValue.CreateStringValue(MessageType.SUB));
            message.SetNamedValue(Field.ID, JsonValue.CreateStringValue(subscription.id));
            message.SetNamedValue(Field.NAME, JsonValue.CreateStringValue(subscription.name));
            
            JsonArray prms = new JsonArray();
            foreach(JsonValue item in subscription.items) {
                prms.Add(item);
            }
            message.SetNamedValue(Field.PARAMS, prms);

            return message.Stringify();
        }
        
        private string GetUnsubscriptionMessage(Subscription subscription) {
            JsonObject message = new JsonObject();
            message.SetNamedValue(Field.MSG, JsonValue.CreateStringValue(MessageType.UNSUB));
            message.SetNamedValue(Field.ID, JsonValue.CreateStringValue(subscription.id));

            return message.Stringify();
        }

        private string GetMethodCallMessage(MethodCall methodCall) {
            JsonObject message = new JsonObject();
            message.SetNamedValue(Field.MSG, JsonValue.CreateStringValue(MessageType.METHOD));
            message.SetNamedValue(Field.METHOD, JsonValue.CreateStringValue(methodCall.methodName));

            JsonArray prms = new JsonArray();
            foreach(JsonValue item in methodCall.items) {
                prms.Add(item);
            }
            message.SetNamedValue(Field.PARAMS, prms);

            message.SetNamedValue(Field.ID, JsonValue.CreateStringValue(methodCall.id));
            //message.SetNamedValue(Field.RANDOM_SEED, xxx);

            return message.Stringify();
        }

        private DdpError GetError(JsonObject obj) {
            string errorCode = null;
            if (obj.ContainsKey(Field.ERROR)) {
                errorCode = obj.GetNamedValue(Field.ERROR).GetString();
            }

            return new DdpError() {
                errorCode = errorCode,
                reason = obj.GetNamedValue(Field.REASON).GetString(),
                message = obj.ContainsKey(Field.MESSAGE) ? obj.GetNamedValue(Field.MESSAGE).GetString() : "",
                errorType = obj.ContainsKey(Field.ERROR_TYPE) ? obj.GetNamedValue(Field.ERROR_TYPE).GetString() : "",
                offendingMessage = obj.ContainsKey(Field.OFFENDING_MESSAGE) ? obj.GetNamedValue(Field.OFFENDING_MESSAGE).GetString() : ""
            };
        }



        private async void Send(string message) {
            if (logMessages) {
                Debug.Log("Send: " + message);
            }
            messageWriter.WriteString(message);
            await messageWriter.StoreAsync();
        }

        private void Closed(IWebSocket webSocket,  WebSocketClosedEventArgs args) {
            ddpConnectionState = ConnectionState.CLOSED;
            sessionId = null;
			subscriptions.Clear();
			methodCalls.Clear();
            if (OnConnectionClosed != null) {
                OnConnectionClosed(this);
            }
        }

        private void MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args) {
            using (DataReader reader = args.GetDataReader()) {
                reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                string read = reader.ReadString(reader.UnconsumedBufferLength);
                if (logMessages) Debug.Log("RECEIVED: " + read);
                HandleMessages(JsonObject.Parse(read));
            }
        }

        public Subscription Subscribe(string name, params JsonValue[] items) {
            Subscription subscription = new Subscription() {
                id = "" + subscriptionId++,
                name = name,
                items = items
            };
            subscriptions[subscription.id] = subscription;
            if (messageWriter != null) {
                Send(GetSubscriptionMessage(subscription));
            }
            return subscription;
        }

        public void Unsubscribe(Subscription subscription) {
            Send(GetUnsubscriptionMessage(subscription));
        }

        public MethodCall Call(string methodName, params JsonValue[] items) {
            MethodCall methodCall = new MethodCall() {
                id = "" + methodCallId++,
                methodName = methodName,
                items = items
            };
            methodCalls[methodCall.id] = methodCall;
            if (messageWriter != null) {
                Send(GetMethodCallMessage(methodCall));
            }
            return methodCall;
        }

        public void Dispose() {
            if (ddpConnectionState == ConnectionState.CONNECTED) {
                ddpConnectionState = ConnectionState.CLOSING;
                messageWebSocket.Dispose();
            }
        }
    }
}
#endif
