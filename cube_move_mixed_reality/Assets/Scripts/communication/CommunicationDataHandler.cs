using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class MeteorData<T>
{
    // note that this is also used for update and meteor does only send a
    // delta of the changed values. Because this is generic not all variables
    // of the item might be filled with data, you need to merge it later on.
    public T item;

    // Meteor database id.
    public string _id;
}

public class CommunicationDataHandler<T>
{
    public string path;


    [System.Serializable]
    public class OnItemChanged : UnityEvent<MeteorData<T>> { };

    public OnItemChanged OnItemAddedEvent = new OnItemChanged();
    public OnItemChanged OnItemChangedEvent = new OnItemChanged();

    public CommunicationDataHandler(string path)
    {
        this.path = path;
    }

    public T Add(string id, T item)
    {
        MeteorData<T> meteorItem = new MeteorData<T>
        {
            item = item,
            _id = id
        };
        OnItemAddedEvent.Invoke(meteorItem);
        return item;
    }

    public T Add(string id, string json)
    {
        T temp = JsonUtility.FromJson<T>(json);
        return Add(id, temp);
    }

    public T Changed(string id, T item)
    {
        MeteorData<T> meteorItem = new MeteorData<T>
        {
            item = item,
            _id = id
        };
        OnItemChangedEvent.Invoke(meteorItem);
        return item;
    }

    public T Changed(string id, string json)
    {
        T temp = JsonUtility.FromJson<T>(json);
        return Changed(id, temp);
    }

    /**
     * alternative for offline use: load data from files instead of Server
     */
    public void LoadFile()
    {
        TextAsset[] assetTexts = Resources.LoadAll<TextAsset>("data/" + path);
        for (int i = 0; i < assetTexts.Length; i++)
        {
            Add(i.ToString(), assetTexts[i].text);
        }
    }
}
