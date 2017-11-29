using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeometryCreation : MonoBehaviour {
    public Communication com;

    public string type;

    public GameObject prefab;

    private Dictionary<string, GameObject> geometries = new Dictionary<string, GameObject>();

    // note that GameObject creation and manipulation needs to be called from the main thread, so we
    // fill lists and do the heavy-lifting in the update function
    private Queue<MeteorData<Geometry>> createGeom = new Queue<MeteorData<Geometry>>();
    private Queue<MeteorData<Geometry>> updateGeom = new Queue<MeteorData<Geometry>>();

    private object _queueLock = new object();

    private void Start()
    {
        com.geometry.OnItemAddedEvent.AddListener(OnGeometryAdded);
    }

    private void Update()
    {
        MeteorData<Geometry> data;
        GameObject obj;
        while (createGeom.Count > 0)
        {
            data = createGeom.Dequeue();
            obj = Instantiate(prefab);
            geometries[data._id] = obj;
            UpdateGeometryPosition(data.item, obj);
        }

        while (updateGeom.Count > 0)
        {
            data = updateGeom.Dequeue();
            UpdateGeometryPosition(data.item, geometries[data._id]);
        }
    }

    public void UpdateGeometryPosition(Geometry geom, GameObject obj)
    {
        obj.transform.position = new Vector3(
            geom.position[0],
            geom.position[1],
            geom.position[2]);
    }

    public void OnGeometryUpdated(MeteorData<Geometry> data)
    {
        lock (_queueLock)
        {
            updateGeom.Enqueue(data);
        }
    }

	public void OnGeometryAdded(MeteorData<Geometry> data)
    {
        lock (_queueLock)
        {
            createGeom.Enqueue(data);
        }
    }
}
