using UnityEngine;
using System.Net;
using System.Net.Sockets;

public class GameplayManager : MonoBehaviour
{
    [Header("References")]
    public GameObject udpServerObject; 
    public GameObject udpClientObject;  
    public GameObject playerPrefab;     

    [Header("Server Settings")]
    [Tooltip("Base port for servers. Each new server will use a different port.")]
    public int baseServerPort = 9050;

    private UDPServer udpServer;
    private UDPClient udpClient;
    private int assignedPort;

    void Start()
    {
        udpServer = udpServerObject.GetComponent<UDPServer>();
        udpClient = udpClientObject.GetComponent<UDPClient>();

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
        Debug.Log("[GameplayManager] Starting HOST mode: Finding available port and creating server");

        // Find an available port
        assignedPort = FindAvailablePort(baseServerPort);

        if (assignedPort == -1)
        {
            Debug.LogError("[GameplayManager] Could not find available port!");
            return;
        }

        Debug.Log($"[GameplayManager] Assigned port: {assignedPort}");

        // Configure server with the available port
        udpServer.port = assignedPort;

        //Activate and start the server
        udpServerObject.SetActive(true);
        udpServer.StartServer();

        //Wait a small moment for the server to initialize
        Invoke(nameof(ConnectToLocalServer), 0.3f);
    }

    private void ConnectToLocalServer()
    {
        //Connect to localhost with the assigned port
        udpClient.serverIP = "127.0.0.1";
        udpClient.serverPort = assignedPort;
        udpClientObject.SetActive(true);
        udpClient.StartConnection();

        Debug.Log($"[GameplayManager] Host connected to own server at 127.0.0.1:{assignedPort}");
        Debug.Log($"[GameplayManager] Other players can join using your IP and port {assignedPort}");
    }

    private void JoinGame()
    {
        Debug.Log("[GameplayManager] Starting CLIENT mode: Connecting to remote server");

        // activate the client
        // The client should have serverIP and serverPort already configured in the Inspector
        udpClientObject.SetActive(true);
        udpClient.StartConnection();

        Debug.Log($"[GameplayManager] Client connecting to {udpClient.serverIP}:{udpClient.serverPort}");
    }

    // Finds an available port starting from the base port
    private int FindAvailablePort(int startPort)
    {
        const int maxAttempts = 100; // Try 100 different ports

        for (int i = 0; i < maxAttempts; i++)
        {
            int portToTry = startPort + i;

            if (IsPortAvailable(portToTry))
            {
                Debug.Log($"[GameplayManager] Port {portToTry} is available");
                return portToTry;
            }
            else
            {
                Debug.Log($"[GameplayManager] Port {portToTry} is in use, trying next...");
            }
        }

        return -1; // No available port found
    }

    // Checks if a specific port is available
    private bool IsPortAvailable(int port)
    {
        Socket testSocket = null;
        try
        {
            testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, port);
            testSocket.Bind(endpoint);
            return true; // Port is available
        }
        catch (SocketException)
        {
            return false; // Port is in use
        }
        finally
        {
            if (testSocket != null)
            {
                testSocket.Close();
                testSocket.Dispose();
            }
        }
    }

    public void SetServerConnection(string ip, int port)
    {
        if (udpClient != null)
        {
            udpClient.serverIP = ip;
            udpClient.serverPort = port;
        }
    }

    public int GetServerPort()
    {
        return assignedPort;
    }


    public void Disconnect()
    {
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