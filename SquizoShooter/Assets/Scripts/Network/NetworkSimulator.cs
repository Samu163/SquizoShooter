using UnityEngine;

public class NetworkSimulator : MonoBehaviour
{
    public static NetworkSimulator Instance { get; private set; }

    [Header("Latency Settings")]
    public bool latencyEnabled = false;
    public float currentLatencyMs = 50f;

    [Header("Packet Loss Settings")]
    public bool packetLossEnabled = false;
    [Range(0f, 1f)]
    public float packetLossRate = 0.1f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool IsLatencyEnabled() => latencyEnabled;
    public float GetCurrentLatency() => currentLatencyMs;
    public bool IsPacketLossEnabled() => packetLossEnabled;
    public float GetPacketLossRate() => packetLossRate;

    public bool ShouldDropPacket()
    {
        return packetLossEnabled && Random.value < packetLossRate;
    }
}