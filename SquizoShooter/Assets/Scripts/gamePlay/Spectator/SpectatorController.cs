using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SpectatorController : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 10f;
    public float rotateSpeed = 5f;
    public Vector3 followOffset = new Vector3(0, 3, -5); // Distancia desde el jugador observado

    private Transform target;
    private List<GameObject> playersCache = new List<GameObject>();
    private int currentTargetIndex = 0;
    private Camera specCamera;

    void Awake()
    {
        specCamera = GetComponentInChildren<Camera>();
        if (specCamera == null) specCamera = GetComponent<Camera>();
    }

    void OnEnable()
    {
        FindNewTarget();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) SwitchTarget(1);
        if (Input.GetMouseButtonDown(1)) SwitchTarget(-1);

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            FindNewTarget();
        }

        if (target != null)
        {
            Vector3 desiredPos = target.position + target.TransformDirection(followOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * moveSpeed);

            transform.LookAt(target.position + Vector3.up * 1.5f);
        }
    }

    public void FindNewTarget()
    {
        RefreshPlayerList();
        if (playersCache.Count > 0)
        {
            currentTargetIndex = Random.Range(0, playersCache.Count);
            target = playersCache[currentTargetIndex].transform;
        }
    }

    void SwitchTarget(int direction)
    {
        RefreshPlayerList();
        if (playersCache.Count == 0) return;

        currentTargetIndex += direction;
        if (currentTargetIndex >= playersCache.Count) currentTargetIndex = 0;
        if (currentTargetIndex < 0) currentTargetIndex = playersCache.Count - 1;

        target = playersCache[currentTargetIndex].transform;
    }

    void RefreshPlayerList()
    {      
        playersCache.Clear();
        var allPlayers = FindObjectsOfType<PlayerController>();

        foreach (var p in allPlayers)
        {
            if (!p.IsLocalPlayer && p.gameObject.activeInHierarchy)
            {
                playersCache.Add(p.gameObject);
            }
        }
    }
}