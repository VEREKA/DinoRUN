using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    private Dictionary<SpikeScript, Queue<SpikeScript>> pools = new Dictionary<SpikeScript, Queue<SpikeScript>>();
    private readonly HashSet<SpikeScript> instances = new HashSet<SpikeScript>();
    private readonly HashSet<SpikeScript> available = new HashSet<SpikeScript>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance == this) SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        // Children survive with the manager, but every new scene starts with an empty playfield.
        instances.RemoveWhere(instance => instance == null);
        available.RemoveWhere(instance => instance == null);
        foreach (var instance in instances)
            if (instance.gameObject.activeSelf) Return(instance);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public SpikeScript Spawn(SpikeScript prefab, Vector3 position)
    {
        if (prefab == null)
            return null;

        if (pools.TryGetValue(prefab, out var q) && q.Count > 0)
        {
            while (q.Count > 0)
            {
                var inst = q.Dequeue();
                available.Remove(inst);
                if (inst == null)
                    continue;

                inst.transform.position = position;
                inst.Initialize();
                inst.gameObject.SetActive(true);
                return inst;
            }
        }

        var obj = Instantiate(prefab, position, Quaternion.identity, transform);
        obj.prefabSource = prefab;
        obj.Initialize();
        instances.Add(obj);
        return obj;
    }

    public void Return(SpikeScript instance)
    {
        if (instance == null)
            return;

        var prefab = instance.prefabSource;
        if (prefab == null)
        {
            Destroy(instance.gameObject);
            return;
        }

        if (!available.Add(instance)) return;
        if (instances.Add(instance)) instance.transform.SetParent(transform, true);

        var body = instance.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        instance.gameObject.SetActive(false);

        if (!pools.TryGetValue(prefab, out var q))
        {
            q = new Queue<SpikeScript>();
            pools[prefab] = q;
        }

        q.Enqueue(instance);
    }
}
