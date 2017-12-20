using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Moulin.DDP;

public class GeometryHandler : MonoBehaviour {
    public Communication com;

    [Tooltip("Type of geometry (e.g. 'cube')")]
    public string Type;

    [Tooltip("Prefab for geometry")]
    public GameObject prefab;

    private JsonObjectCollection geometryCollection;

    // all known geometry
    Dictionary<string, GameObject> geometries = new Dictionary<string, GameObject>();

    // note that GameObject creation and manipulation needs to be called from the main thread, so we
    // fill lists and do the heavy-lifting in the update function
    private Queue<KeyValuePair<string, JSONObject>> createGeom = new Queue<KeyValuePair<string, JSONObject>>();
    private Queue<KeyValuePair<string, JSONObject>> updateGeom = new Queue<KeyValuePair<string, JSONObject>>();

    private void Start()
    {
        geometryCollection = com.GetCollection("geometry");
        if (geometryCollection == null)
        {
            com.OnDBStart.AddListener(() => {
                geometryCollection = com.GetCollection("geometry");
                SetupGeometryCollection();
            });
        }
        else
        {
            SetupGeometryCollection();
        }

    }

    private void SetupGeometryCollection()
    {
        lock (createGeom)
        {
            geometryCollection.OnAdded += (id, fields) =>
            {
                if (!fields.HasField("type")) return;
                string GeomType = fields.GetField("type").str;
                if (GeomType != Type) return;
                createGeom.Enqueue(new KeyValuePair<string, JSONObject>(id, fields));
            };
        }
        lock (updateGeom) {
            geometryCollection.OnChanged += (id, fields, cleared) =>
            {
                Debug.Log("Change " + id + " - " + fields);
                updateGeom.Enqueue(new KeyValuePair<string, JSONObject>(id, fields));
            };
        }
    }

    private void Update()
    {
        KeyValuePair<string, JSONObject> data;
        GameObject obj;
        
        lock (createGeom)
        {
            while (createGeom.Count > 0)
            {
                data = createGeom.Dequeue();
                obj = Instantiate(prefab);
                geometries.Add(data.Key, obj);
                UpdateGeometryPosition(data.Value.GetField("position"), obj);
            }
        }
        lock (updateGeom) { 
            while (updateGeom.Count > 0)
            {
                data = updateGeom.Dequeue();
                if (!geometries.ContainsKey(data.Key)) return;
                UpdateGeometryPosition(data.Value.GetField("position"), geometries[data.Key]);
            }
        }
    }

    public void UpdateGeometryPosition(JSONObject position, GameObject obj)
    {
        if (position == null) return;
        obj.transform.position = new Vector3(
            position[0].f,
            position[1].f,
            position[2].f);
    }
}
