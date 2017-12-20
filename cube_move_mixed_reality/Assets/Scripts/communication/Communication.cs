using UnityEngine;
using Moulin.DDP;
using UnityEngine.Events;

public class Communication : MonoBehaviour {
    public string serverUrl = "ws://localhost:3000/websocket";
    public UnityEvent OnDBStart;

    private DdpConnection ddpConnection;

    private LocalDB localDB;
    
    private void Start () {
        Connect();
	}

    public JsonObjectCollection GetCollection(string name)
    {
        if (localDB == null) return null;
        return (JsonObjectCollection)localDB.GetCollection(name);
    }

    public void SetupDB() {
        localDB = new LocalDB((db, collectionName) => {
            return new JsonObjectCollection(db, collectionName);
        }, ddpConnection);
        OnDBStart.Invoke();
    }

    public void Connect()
    {
        Debug.Log("connecting to " + serverUrl);
        ddpConnection = new DdpConnection(serverUrl);
        ddpConnection.OnDebugMessage += (string message) =>
        {
            Debug.Log(message);
        };
        ddpConnection.OnConnected += (DdpConnection connection) => {
            Debug.Log("connected!");
            ddpConnection.Subscribe("cube");
            ddpConnection.Subscribe("sphere");
            ddpConnection.Subscribe("monkey");
        };

        ddpConnection.OnError += DdpConnection_OnError;
        SetupDB();
        ddpConnection.Connect();
    }
    
    private void DdpConnection_OnError(DdpError error) {
        Debug.Log("ERROR " + error.message + " - " + error.reason);
    }
    
}
