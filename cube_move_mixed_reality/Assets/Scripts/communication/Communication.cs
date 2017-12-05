using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using System.IO;
using UnityEngine.Events;
#if WINDOWS_UWP
using Windows.Data.Json;
using Meteor.ddp;
#endif

public class Communication : MonoBehaviour {
    public CommunicationDataHandler<Geometry> geometry = new CommunicationDataHandler<Geometry>("geom");

    public string serverUrl = "ws://localhost:3000/websocket";

#if WINDOWS_UWP
    private DdpConnection ddpConnection;
#endif

    // Use this for initialization
    void Start () {
        Connect();
	}

    public void Connect()
    {
#if WINDOWS_UWP
        Debug.Log("connecting to " + serverUrl);
        ddpConnection = new DdpConnection(serverUrl);
        ddpConnection.OnConnected += (DdpConnection connection) => {
            Debug.Log("WEBSOCKET Connected.");
        };
        ddpConnection.OnError += DdpConnection_OnError;
        ddpConnection.OnDisconnected += DdpConnection_OnDisconnected;
        ddpConnection.OnConnectionClosed += DdpConnection_OnConnectionClosed;

        ddpConnection.OnAdded += DdpConnection_OnAdded;
        ddpConnection.OnChanged += DdpConnection_OnChanged;
        ddpConnection.OnRemoved += DdpConnection_OnRemoved;

        ddpConnection.Connect();

        ddpConnection.Subscribe("geometry");
#endif
    }

#if WINDOWS_UWP
    private void DdpConnection_OnConnectionClosed(DdpConnection connection) {
        Debug.Log("CONNECTION CLOSED");
    }

    private void DdpConnection_OnDisconnected(DdpConnection connection) {
        Debug.Log("DISCONNECT");
    }

    private void DdpConnection_OnRemoved(string collection, string docId) {

    }

    private void DdpConnection_OnError(DdpError error) {
        Debug.Log("ERROR " + error.message + " - " + error.reason);
    }

    private void DdpConnection_OnChanged(string collection, string docId, JsonObject fields) {
        if (collection.Equals("geometry"))
        {
            Geometry g = geometry.Changed(docId, fields.Stringify());
        }
        Debug.Log("changed " + docId + " - " + collection + " - " + fields.ToString());
    }

    private void DdpConnection_OnAdded(string collection, string docId, JsonObject fields) {
        if (collection.Equals("geometry")) {
            Geometry g = geometry.Add(docId, fields.Stringify());
        }
        Debug.Log("added " + docId + " - " + collection);
    }
#endif
}
