using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TCPServer : MonoBehaviour
{
    [Header("Server Configuration")]
    public int port = 9050;
    public string serverName = "My Game Server";

    [Header("UI References")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI clientListText;
    public InputField chatInput;
    public TextMeshProUGUI chatLogText;
    public Button sendMessageButton;

    private Socket serverSocket;
    private Thread listenThread;
    private List<ClientConnection> connectedClients = new List<ClientConnection>();
    private bool isRunning = false;
    private Queue<string> mainThreadActions = new Queue<string>();

    private class ClientConnection
    {
        public Socket socket;
        public string clientName;
        public Thread receiveThread;
        public IPEndPoint endpoint;
    }

    void Start()
    {
        if (sendMessageButton != null)
            sendMessageButton.onClick.AddListener(SendChatMessage);

        StartServer();
    }

    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
            serverSocket.Bind(ipep);

            serverSocket.Listen(10);

            isRunning = true;
            UpdateStatus("Server started on port " + port);

            listenThread = new Thread(AcceptClients);
            listenThread.Start();
        }
        catch (Exception e)
        {
            UpdateStatus("Error starting server: " + e.Message);
            Debug.LogError(e);
        }
    }

    void AcceptClients()
    {
        while (isRunning)
        {
            try
            {
                Debug.Log("Waiting for clients...");

                Socket clientSocket = serverSocket.Accept();

                IPEndPoint clientEP = (IPEndPoint)clientSocket.RemoteEndPoint;
                Debug.Log("Client connected from: " + clientEP.ToString());

                ClientConnection client = new ClientConnection
                {
                    socket = clientSocket,
                    endpoint = clientEP,
                    clientName = "Unknown"
                };

                connectedClients.Add(client);

                SendWelcomeMessage(client);

                client.receiveThread = new Thread(() => ReceiveMessages(client));
                client.receiveThread.Start();

                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue("update_clients");
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogError("Error accepting client: " + e.Message);
                }
            }
        }
    }

    void SendWelcomeMessage(ClientConnection client)
    {
        try
        {
            string welcomeMsg = "WELCOME:" + serverName;
            byte[] data = Encoding.ASCII.GetBytes(welcomeMsg);
            client.socket.Send(data);
            Debug.Log("Welcome message sent to client");
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending welcome: " + e.Message);
        }
    }

    void ReceiveMessages(ClientConnection client)
    {
        byte[] buffer = new byte[4096];

        while (isRunning && client.socket.Connected)
        {
            try
            {
                int bytesReceived = client.socket.Receive(buffer);

                if (bytesReceived == 0)
                {
                    Debug.Log("Client disconnected: " + client.endpoint);
                    break;
                }

                string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                Debug.Log("Received from client: " + message);

                ProcessClientMessage(client, message);

                SendPing(client);
            }
            catch (Exception e)
            {
                Debug.LogError("Error receiving from client: " + e.Message);
                break;
            }
        }

        RemoveClient(client);
    }

    void SendPing(ClientConnection client)
    {
        try
        {
            byte[] data = Encoding.ASCII.GetBytes("ping");
            client.socket.Send(data);
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending ping: " + e.Message);
        }
    }

    void ProcessClientMessage(ClientConnection client, string message)
    {
        if (message.StartsWith("NAME:"))
        {
            client.clientName = message.Substring(5);
            lock (mainThreadActions)
            {
                mainThreadActions.Enqueue("update_clients");
            }
        }
        else if (message.StartsWith("CHAT:"))
        {
            string chatMsg = message.Substring(5);
            BroadcastMessage(client.clientName + ": " + chatMsg);
        }
    }

    void SendChatMessage()
    {
        if (chatInput != null && !string.IsNullOrEmpty(chatInput.text))
        {
            BroadcastMessage("Server: " + chatInput.text);
            chatInput.text = "";
        }
    }

    void BroadcastMessage(string message)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue("chat:" + message);
        }

        byte[] data = Encoding.ASCII.GetBytes("CHAT:" + message);

        foreach (var client in connectedClients)
        {
            try
            {
                client.socket.Send(data);
            }
            catch (Exception e)
            {
                Debug.LogError("Error broadcasting to client: " + e.Message);
            }
        }
    }

    void RemoveClient(ClientConnection client)
    {
        try
        {
            client.socket.Shutdown(SocketShutdown.Both);
            client.socket.Close();
        }
        catch { }

        connectedClients.Remove(client);

        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue("update_clients");
        }
    }

    void Update()
    {
        // Process main thread actions
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                string action = mainThreadActions.Dequeue();

                if (action == "update_clients")
                {
                    UpdateClientList();
                }
                else if (action.StartsWith("chat:"))
                {
                    UpdateChatLog(action.Substring(5));
                }
                else if (action.StartsWith("status:"))
                {
                    UpdateStatus(action.Substring(7));
                }
            }
        }
    }

    void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        Debug.Log("Status: " + message);
    }

    void UpdateClientList()
    {
        if (clientListText != null)
        {
            string list = "Connected Clients (" + connectedClients.Count + "):\n";
            foreach (var client in connectedClients)
            {
                list += "- " + client.clientName + " (" + client.endpoint.Address + ")\n";
            }
            clientListText.text = list;
        }
    }

    void UpdateChatLog(string message)
    {
        if (chatLogText != null)
        {
            chatLogText.text += message + "\n";
        }
    }

    void OnDestroy()
    {
        isRunning = false;

        foreach (var client in connectedClients)
        {
            try
            {
                client.socket.Shutdown(SocketShutdown.Both);
                client.socket.Close();
            }
            catch { }
        }

        if (serverSocket != null)
        {
            try
            {
                serverSocket.Close();
            }
            catch { }
        }

        if (listenThread != null && listenThread.IsAlive)
        {
            listenThread.Abort();
        }
    }
}