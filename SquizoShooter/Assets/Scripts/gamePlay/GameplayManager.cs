using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using System.Collections.Generic;

public class GameplayManager : MonoBehaviour
{
    [Header("References")]
    public GameObject udpServerObject;
    public GameObject udpClientObject;
    public GameObject playerPrefab;

    [Header("Server Settings")]
    public int baseServerPort = 9050;
    public int discoveryPort = 47777; // Changed to a different port

    private UDPServer udpServer;
    private UDPClient udpClient;
    private int assignedPort;
    private string localIP;

    // Server discovery
    private UdpClient discoveryBroadcaster;
    private UdpClient discoveryListener;
    private Thread discoveryThread;
    private volatile bool isDiscoveryRunning = false;
    private bool isHost = false;

    void Awake()
    {
        if (UnityMainThreadDispatcher._instance == null)
        {
            var go = new GameObject("MainThreadDispatcher");
            UnityMainThreadDispatcher._instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
    }

    void Start()
    {
        udpServer = udpServerObject.GetComponent<UDPServer>();
        udpClient = udpClientObject.GetComponent<UDPClient>();

        localIP = GetLocalIPAddress();
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

    private void HostGame()
    {
        Debug.Log("[GameplayManager] Starting HOST mode");
        isHost = true;

        assignedPort = FindAvailablePort(baseServerPort);

        if (assignedPort == -1)
        {
            Debug.LogError("[GameplayManager] Could not find available port!");
            return;
        }

        Debug.Log($"[GameplayManager] Server port: {assignedPort}");

        udpServer.port = assignedPort;
        udpServerObject.SetActive(true);
        udpServer.StartServer();

        // Start broadcasting
        StartDiscoveryBroadcast();

        Invoke(nameof(ConnectToLocalServer), 0.5f);
    }

    private void ConnectToLocalServer()
    {
        udpClient.serverIP = "127.0.0.1";
        udpClient.serverPort = assignedPort;
        udpClientObject.SetActive(true);
        udpClient.StartConnection();

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
        isHost = false;
        StartDiscoverySearch();
    }

    private void StartDiscoveryBroadcast()
    {
        isDiscoveryRunning = true;
        discoveryThread = new Thread(BroadcastLoop) { IsBackground = true };
        discoveryThread.Start();
    }

    private void BroadcastLoop()
    {
        try
        {
            discoveryBroadcaster = new UdpClient();
            discoveryBroadcaster.EnableBroadcast = true;

            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
            string message = $"GAMESERVER:{localIP}:{assignedPort}";
            byte[] data = Encoding.UTF8.GetBytes(message);

            Debug.Log($"[Discovery] Broadcasting on port {discoveryPort}");

            while (isDiscoveryRunning)
            {
                try
                {
                    discoveryBroadcaster.Send(data, data.Length, broadcastEP);
                    Thread.Sleep(1000); // Broadcast every second
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Discovery] Broadcast error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Discovery] Broadcaster error: {ex.Message}");
        }
        finally
        {
            try { discoveryBroadcaster?.Close(); } catch { }
        }
    }

    private void StartDiscoverySearch()
    {
        isDiscoveryRunning = true;
        discoveryThread = new Thread(SearchLoop) { IsBackground = true };
        discoveryThread.Start();
    }

    private void SearchLoop()
    {
        try
        {
            discoveryListener = new UdpClient(discoveryPort);
            discoveryListener.EnableBroadcast = true;
            discoveryListener.Client.ReceiveTimeout = 10000; // 10 second timeout

            Debug.Log($"[Discovery] Listening on port {discoveryPort}");

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (isDiscoveryRunning)
            {
                try
                {
                    byte[] data = discoveryListener.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);

                    Debug.Log($"[Discovery] Received: {message} from {remoteEP.Address}");

                    if (message.StartsWith("GAMESERVER:"))
                    {
                        string[] parts = message.Split(':');
                        if (parts.Length >= 3)
                        {
                            string serverIP = parts[1];
                            int serverPort;

                            if (int.TryParse(parts[2], out serverPort))
                            {
                                Debug.Log($"[Discovery] Found server at {serverIP}:{serverPort}");

                                isDiscoveryRunning = false;

                                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                                {
                                    ConnectToServer(serverIP, serverPort);
                                });

                                break;
                            }
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        Debug.Log("[Discovery] No servers found, retrying...");
                        continue;
                    }
                    else if (isDiscoveryRunning)
                    {
                        Debug.LogWarning($"[Discovery] Error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    if (isDiscoveryRunning)
                    {
                        Debug.LogError($"[Discovery] Error: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Discovery] Listener error: {ex.Message}");
        }
        finally
        {
            try { discoveryListener?.Close(); } catch { }
        }
    }

    private void ConnectToServer(string ip, int port)
    {
        Debug.Log($"[GameplayManager] Connecting to: {ip}:{port}");

        udpClient.serverIP = ip;
        udpClient.serverPort = port;
        udpClientObject.SetActive(true);
        udpClient.StartConnection();
    }

    private string GetLocalIPAddress()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        if (ipStr.StartsWith("192.168.") || ipStr.StartsWith("10."))
                        {
                            return ipStr;
                        }
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }
    }

    private int FindAvailablePort(int startPort)
    {
        for (int i = 0; i < 100; i++)
        {
            int port = startPort + i;
            if (IsPortAvailable(port))
            {
                return port;
            }
        }
        return -1;
    }

    private bool IsPortAvailable(int port)
    {
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        isDiscoveryRunning = false;

        if (discoveryThread != null && discoveryThread.IsAlive)
        {
            discoveryThread.Join(1000);
        }

        try { discoveryBroadcaster?.Close(); } catch { }
        try { discoveryListener?.Close(); } catch { }

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

public class UnityMainThreadDispatcher : MonoBehaviour
{
    public static UnityMainThreadDispatcher _instance;
    private readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        return _instance;
    }

    public void Enqueue(Action action)
    {
        if (action == null) return;

        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                try
                {
                    _executionQueue.Dequeue().Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDispatcher] Error: {ex.Message}");
                }
            }
        }
    }
}