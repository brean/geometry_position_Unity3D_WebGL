using UnityEngine;
using Meteor.ddp;


public class Communication : MonoBehaviour {
    public CommunicationDataHandler<Geometry> geometry = new CommunicationDataHandler<Geometry>("geom");

    public string serverUrl = "ws://localhost:3000/websocket";

    private DdpConnection ddpConnection;

    // Use this for initialization
    void Start () {
        Connect();
	}

    public void Connect()
    {
        Debug.Log("connecting to " + serverUrl);
        ddpConnection = new DdpConnection(serverUrl);
        ddpConnection.OnConnected += (DdpConnection connection) => {
            ddpConnection.Subscribe("geometry");
        };
        ddpConnection.OnError += DdpConnection_OnError;
        ddpConnection.OnDisconnected += DdpConnection_OnDisconnected;
        ddpConnection.OnConnectionClosed += DdpConnection_OnConnectionClosed;

        ddpConnection.OnAdded += DdpConnection_OnAdded;
        ddpConnection.OnChanged += DdpConnection_OnChanged;
        ddpConnection.OnRemoved += DdpConnection_OnRemoved;

        ddpConnection.Connect();
    }
    
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

    private void DdpConnection_OnChanged(string collection, string docId, string json) {
        if (collection.Equals("geometry"))
        {
            Geometry g = geometry.Changed(docId, Geometry.FromJson(json));
        }
        Debug.Log("changed " + docId + " - " + collection + " - " + json);
    }

    private void DdpConnection_OnAdded(string collection, string docId, string json) {
        if (collection.Equals("geometry")) {
            Geometry g = geometry.Add(docId, Geometry.FromJson(json));
        }
        Debug.Log("added " + docId + " - " + collection);
    }
}
