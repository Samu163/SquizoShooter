using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using System.Collections.Generic;


public class GameplayManager : MonoBehaviour
{
    public static GameplayManager Instance;

    [Header("References")]
    public GameObject udpServerObject;
    public GameObject udpClientObject;
    public GameObject playerPrefab;
    public UiController uiController;

    [Header("Spawn Configuration")]
    public Transform[] spawnPoints;
    

    [Header("Server Settings")]
    public int baseServerPort = 9050;

    private UDPServer udpServer;
    private UDPClient udpClient;
    private ServerDiscoveryManager serverDiscovery;
    private int assignedPort;
    private string localIP;

    void Awake()
    {
        Instance = this;
        // Initialize main thread dispatcher
        if (UnityMainThreadDispatcher._instance == null)
        {
            var go = new GameObject("MainThreadDispatcher");
            UnityMainThreadDispatcher._instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }

        serverDiscovery = gameObject.AddComponent<ServerDiscoveryManager>();
        serverDiscovery.OnServerFound += OnServerDiscovered;
    }

    void Start()
    {
        udpServer = udpServerObject.GetComponent<UDPServer>();
        udpClient = udpClientObject.GetComponent<UDPClient>();

        localIP = NetworkUtils.GetLocalIPAddress();
        Debug.Log($"[GameplayManager] Local IP: {localIP}");

        GameManager.GameMode mode = GameManager.Instance.GetGameMode();

        if (mode == GameManager.GameMode.Server)
        {
            HostGame();
        }
        else if (mode == GameManager.GameMode.Client)
        {
            JoinGame();
        }
    }

    //Gameplay Methods
    public Vector3 GetRandomSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn points available, using default position");
            return Vector3.zero;
        }

        int randomIndex = UnityEngine.Random.Range(0, spawnPoints.Length);
        return spawnPoints[randomIndex].position;
    }



    //--Init network settings--//

    private void HostGame()
    {
        Debug.Log("[GameplayManager] Starting HOST mode");

        assignedPort = NetworkUtils.FindAvailablePort(baseServerPort);

        if (assignedPort == -1)
        {
            Debug.LogError("[GameplayManager] Could not find available port!");
            return;
        }

        Debug.Log($"[GameplayManager] Server port: {assignedPort}");

        // Start server
        udpServer.port = assignedPort;
        udpServerObject.SetActive(true);
        udpServer.StartServer();

        serverDiscovery.StartBroadcasting(localIP, assignedPort);

        // Connect to own server
        Invoke(nameof(ConnectToLocalServer), 0.5f);
    }

    private void ConnectToLocalServer()
    {
        udpClient.serverIP = "127.0.0.1";
        udpClient.serverPort = assignedPort;
        udpClientObject.SetActive(true);
        udpClient.StartConnection();

        // Show connected status
        //if (uiController != null)
        //    uiController.ShowConnectedStatus();

        Debug.Log($"[GameplayManager] Host connected to own server");
        Debug.Log("========================================");
        Debug.Log($"SERVER INFO - Share with other players:");
        Debug.Log($"IP: {localIP}");
        Debug.Log($"Port: {assignedPort}");
        Debug.Log("========================================");
    }

    private void JoinGame()
    {
        Debug.Log("[GameplayManager] Starting CLIENT mode - Searching for servers...");

        // Start listening for server broadcasts
        serverDiscovery.StartListening();
    }

    private void OnServerDiscovered(string serverIP, int serverPort)
    {
        Debug.Log($"[GameplayManager] Connecting to discovered server: {serverIP}:{serverPort}");

        udpClient.serverIP = serverIP;
        udpClient.serverPort = serverPort;
        udpClientObject.SetActive(true);
        udpClient.StartConnection();

        // Show connected status
        //if (uiController != null)
        //    uiController.ShowConnectedStatus();
    }

    public void Disconnect()
    {
        // Stop discovery
        if (serverDiscovery != null)
        {
            serverDiscovery.Stop();
        }

        // Disconnect client/server
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.Disconnect();
        }

        if (udpServer != null)
        {
            udpServer.StopServer();
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}