using UnityEngine;
using System.Collections.Generic;

public class BulletTrailPool : MonoBehaviour
{
    public static BulletTrailPool Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private int poolSize = 50; 
    [SerializeField] private bool canGrow = true;

    private Queue<BulletTrail> poolQueue = new Queue<BulletTrail>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            CreateNewTrail();
        }
    }

    private BulletTrail CreateNewTrail()
    {
        GameObject obj = Instantiate(trailPrefab, transform);
        BulletTrail trail = obj.GetComponent<BulletTrail>();
        obj.SetActive(false);
        poolQueue.Enqueue(trail);
        return trail;
    }

    public BulletTrail GetTrail()
    {
        if (poolQueue.Count == 0)
        {
            if (canGrow)
            {
                return CreateNewTrail();
            }
            else
            {
                Debug.LogWarning("[BulletTrailPool] No quedan balas disponibles y canGrow es false.");
                return null;
            }
        }

        BulletTrail trail = poolQueue.Dequeue();

        if (trail == null)
        {
            return CreateNewTrail();
        }

        if (trail.gameObject.activeInHierarchy)
        {
            return GetTrail();
        }

        return trail;
    }

    public void ReturnToPool(BulletTrail trail)
    {
        trail.gameObject.SetActive(false);
        poolQueue.Enqueue(trail);
    }
}