using System.Collections.Generic;
using UnityEngine;

public class SmartPoolManager : MonoBehaviour
{
    // Key: Prefab Instance ID -> Queue of available objects
    private Dictionary<int, Queue<PoolMember>> pools = new Dictionary<int, Queue<PoolMember>>();
    
    // Tracking tat ca active objects de mass reset
    private List<PoolMember> activeObjects = new List<PoolMember>();
    private Transform poolRoot;

    private void Awake()
    {
        poolRoot = new GameObject("Pool_Root").transform;
        poolRoot.SetParent(transform);
    }

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        // Lay GameObject tu prefab component
        GameObject result = Spawn(prefab.gameObject, position, rotation, parent);
        return result.GetComponent<T>();
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogError("[SmartPool] Cannot spawn null prefab!");
            return null;
        }

        int key = prefab.GetInstanceID();
        
        // 1. Init pool neu chua co
        if (!pools.ContainsKey(key))
        {
            pools[key] = new Queue<PoolMember>();
        }

        PoolMember member = null;

        // 2. Lay tu pool hoac tao moi
        if (pools[key].Count > 0)
        {
            member = pools[key].Dequeue();
        }
        else
        {
            GameObject obj = Instantiate(prefab, poolRoot);
            member = obj.AddComponent<PoolMember>();
            member.Init(key, this);
        }

        // 3. Setup transform
        member.transform.SetParent(parent);
        member.transform.position = position;
        member.transform.rotation = rotation;
        member.gameObject.SetActive(true);

        // 4. Notify IPoolable
        var poolables = member.GetComponents<IPoolable>();
        foreach (var p in poolables) p.OnSpawn();

        // 5. Track active object
        activeObjects.Add(member);

        return member.gameObject;
    }

    public void Return(PoolMember member)
    {
        if (member == null) return;
        
        // 1. Notify IPoolable
        if (member.gameObject.activeSelf)
        {
            var poolables = member.GetComponents<IPoolable>();
            foreach (var p in poolables) p.OnDespawn();
        }

        // 2. Disable & Move to Root
        member.gameObject.SetActive(false);
        member.transform.SetParent(poolRoot);

        // 3. Enqueue
        if (pools.ContainsKey(member.Key))
        {
            pools[member.Key].Enqueue(member);
        }
        else
        {
            // Truong hop la (vi du Scene reload ma object van con)
            Destroy(member.gameObject);
        }

        // 4. Untrack
        activeObjects.Remove(member);
    }

    public void DespawnAll()
    {
        // Clone list de tranh loi collection modified khi loop
        var listToReturn = new List<PoolMember>(activeObjects);
        foreach (var member in listToReturn)
        {
            if (member != null) Return(member);
        }
        activeObjects.Clear();
    }
}