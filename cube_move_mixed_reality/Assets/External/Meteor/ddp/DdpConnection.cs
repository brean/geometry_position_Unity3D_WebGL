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

using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_5_4_OR_NEWER
using UnityEngine; // for Debug.Log
#endif
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
        public delegate void OnAddedDelegate(string collection, string docId, string json);
        public delegate void OnChangedDelegate(string collection, string docId, string json);
        public delegate void OnRemovedDelegate(string collection, string docId);
        public delegate void OnAddedBeforeDelegate(string collection, string docId, object fields, string before);
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

        // WebSocket provided by UWP framework or System.Net
        private WebSocketConnection con;

        private Uri uri;

        public DdpConnection(string url) {
            uri = new Uri(url);
#if WINDOWS_UWP
            con = new WebSocketUWP(this);
#else
            con = new WebSocketSystemNet(this);
#endif
        }

        // run StartConnect async
        public void Connect() {
            Task.Run(() => StartConnect());
        }

        private async void StartConnect() {
            await con.Connect(uri);
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

        public void HandleMessages(string json) {
#if UNITY_5_4_OR_NEWER
            if (logMessages)
            {
                Debug.Log("RECEIVED: " + json);
            }
#endif
            ReceivedData data = ReceivedData.FromJson(json);
            if (data.Msg == null) {
                // silently ignore messages that don't have the MSG-key
                return;
            }
            string msg = (string)data.Msg;
            switch (msg) {
                case MessageType.CONNECTED: {
                    sessionId = data.Session;
                    ddpConnectionState = ConnectionState.CONNECTED;
                    OnConnectedFirst();
                    OnConnected?.Invoke(this);
                    break;
                }
                case MessageType.FAILED: {
                    OnError?.Invoke(new DdpError() {
                        errorCode = "Connection refused",
                        reason = "The server is using an unsupported DDP protocol version: " +
                            data.Version
                    });
                    //Dispose();
                    break;
                }
                case MessageType.PING: {
                    if (data.Id == null) {
                        Send(GetPongMessage());
                    } else {
                        Send(GetPongMessage(data.Id));
                    }
                    break;
                }

                case MessageType.NOSUB: {
                    string subscriptionId = data.Id;
                    subscriptions.Remove(subscriptionId);

                    if (data.Error != null && OnError != null) {
                        OnError(GetError(data.Error));
                    }
                    break;
                }
                case MessageType.ADDED: {
                    OnAdded?.Invoke(data.Collection, data.Id, data.Fields.ToString());
                    break;
                }
                case MessageType.CHANGED: {
                    OnChanged?.Invoke(data.Collection, data.Id, data.Fields.ToString());
                    break;
                }
                case MessageType.REMOVED: {
                    OnRemoved?.Invoke(data.Collection, data.Id);
                    break;
                }

                case MessageType.READY: {
                    foreach (string subscriptionId in data.Subs) {
                        if (!subscriptions.ContainsKey(subscriptionId))
                        {
                            continue;
                        }
                        Subscription subscription = subscriptions[subscriptionId];
                        if (subscription == null)
                        {
                            continue;
                        }
                        subscription.isReady = true;
                        subscription.OnReady?.Invoke(subscription);
                    }
                    break;
                }

                case MessageType.ADDED_BEFORE: {
                    OnAddedBefore?.Invoke(data.Collection, data.Id, data.Fields, data.Before);
                    break;
                }

                case MessageType.MOVED_BEFORE: {
                    OnMovedBefore?.Invoke(data.Collection, data.Id, data.Before);
                    break;
                }

                case MessageType.RESULT: {
                    string methodCallId = data.Id;
                    MethodCall methodCall = methodCalls[methodCallId];
                    if (methodCall != null) {
                        if (data.Error != null) {
                            methodCall.error = GetError(data.Error);
                        }
                        methodCall.result = data.Result;
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
                    foreach (string methodCallId in data.Methods) {
                        MethodCall methodCall = methodCalls[methodCallId];
                        if (methodCall != null) {
                            if (methodCall.hasResult) {
                                methodCalls.Remove(methodCallId);
                            }
                            methodCall.hasUpdated = true;
                            methodCall.OnUpdated?.Invoke(methodCall);
                        }
                    }
                    break;
                }

                case MessageType.ERROR: {
                    OnError?.Invoke(GetError(data.Error));
                    break;
                }
            }
        }

        private string GetConnectMessage() {
            ConnectMessage msg = new ConnectMessage();
            msg.Msg = MessageType.CONNECT;
            if (sessionId != null)
            {
                msg.Session = sessionId;
            }
            msg.Version = DDP_PROTOCOL_VERSION;
            msg.Support = new string[] { DDP_PROTOCOL_VERSION };

            return msg.ToJson();
        }

        private string GetPongMessage() {
            PongMessage msg = new PongMessage();
            msg.Msg = MessageType.PONG;
            return msg.ToJson();
        }
        
        private string GetPongMessage(string id) {
            PongMessage msg = new PongMessage();
            msg.Msg = MessageType.PONG;
            msg.Id = id;
            return msg.ToJson();
        }

        private string GetSubscriptionMessage(Subscription subscription) {
            SubscriptionMessage msg = new SubscriptionMessage();
            msg.Msg = MessageType.SUB;
            msg.Id = subscription.id;
            msg.Name = subscription.name;
            msg.Params = subscription.items;
            return msg.ToJson();
        }
        
        private string GetUnsubscriptionMessage(Subscription subscription) {
            SubscriptionMessage msg = new SubscriptionMessage();
            msg.Msg = MessageType.UNSUB;
            msg.Id = subscription.id;
            return msg.ToJson();
        }

        private string GetMethodCallMessage(MethodCall methodCall) {
            MethodCallMessage mcm = new MethodCallMessage();
            mcm.Msg = MessageType.METHOD;
            mcm.Method = methodCall.methodName;
            mcm.Id = methodCall.id;
            mcm.Params = methodCall.items;
            return mcm.ToJson();
        }

        private DdpError GetError(ReceivedDataError err) {
            return new DdpError() {
                errorCode = err.Error,
                reason = err.Reason,
                message = err.Message ?? "",
                errorType = err.ErrorType ?? "",
                offendingMessage = err.OffendingMessage ?? ""
            };
        }



        private async void Send(string message) {
#if UNITY_5_4_OR_NEWER
            if (logMessages) {
                Debug.Log("Send: " + message);
            }
            await con.Send(message);
#endif
        }

        public void Closed() {
            ddpConnectionState = ConnectionState.CLOSED;
            sessionId = null;
			subscriptions.Clear();
			methodCalls.Clear();
            if (OnConnectionClosed != null) {
                OnConnectionClosed(this);
            }
        }

        public Subscription Subscribe(string name, params object[] items) {
            Subscription subscription = new Subscription() {
                id = "" + subscriptionId++,
                name = name,
                items = items
            };
            subscriptions[subscription.id] = subscription;
            Send(GetSubscriptionMessage(subscription));
            return subscription;
        }

        public void Unsubscribe(Subscription subscription) {
            Send(GetUnsubscriptionMessage(subscription));
        }

        public MethodCall Call(string methodName, params object[] items) {
            MethodCall methodCall = new MethodCall() {
                id = "" + methodCallId++,
                methodName = methodName,
                items = items
            };
            methodCalls[methodCall.id] = methodCall;
            Send(GetMethodCallMessage(methodCall));
            return methodCall;
        }

        public void Dispose() {
            if (ddpConnectionState == ConnectionState.CONNECTED) {
                ddpConnectionState = ConnectionState.CLOSING;
                con.Dispose();
            }
        }
    }
}
