using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UDPServer : MonoBehaviour
{
    [Header("Server Configuration")]
    public int port = 9050;
    public string serverName = "My UDP Server";

    [Header("UI References")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI clientListText;
    public InputField chatInput;
    public TextMeshProUGUI chatLogText;
    public Button sendMessageButton;

    private Socket serverSocket;
    private Thread receiveThread;
    private Dictionary<string, EndPoint> connectedClients = new Dictionary<string, EndPoint>();
    private bool isRunning = false;
    private Queue<string> mainThreadActions = new Queue<string>();

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
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
            serverSocket.Bind(ipep);

            isRunning = true;
            UpdateStatus("UDP Server started on port " + port);

            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            UpdateStatus("Error starting server: " + e.Message);
            Debug.LogError(e);
        }
    }

    void ReceiveMessages()
    {
        byte[] buffer = new byte[4096];

        while (isRunning)
        {
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint remote = (EndPoint)sender;

                int bytesReceived = serverSocket.ReceiveFrom(buffer, ref remote);

                if (bytesReceived > 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                    Debug.Log("Received from " + remote.ToString() + ": " + message);

                    ProcessClientMessage(message, remote);
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogError("Error receiving UDP message: " + e.Message);
                }
            }
        }
    }

    void ProcessClientMessage(string message, EndPoint remote)
    {
        string clientKey = remote.ToString();

        if (message.StartsWith("HANDSHAKE:"))
        {
            // New client connecting
            string clientName = message.Substring(10);

            if (!connectedClients.ContainsKey(clientKey))
            {
                connectedClients[clientKey] = remote;
                Debug.Log("New client connected: " + clientName);

                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue("update_clients");
                }
            }

            SendPingToClient(remote);
            string welcomeMsg = "WELCOME:" + serverName;
            SendMessageToClient(welcomeMsg, remote);
        }
        else if (message.StartsWith("NAME:"))
        {
            connectedClients[clientKey] = remote;

            lock (mainThreadActions)
            {
                mainThreadActions.Enqueue("update_clients");
            }

            SendPingToClient(remote);
        }
        else if (message.StartsWith("CHAT:"))
        {
            string chatMsg = message.Substring(5);
            string clientName = clientKey.Split(':')[0];
            BroadcastMessage(clientName + ": " + chatMsg);
        }
    }
    void SendPingToClient(EndPoint remote)
    {
        try
        {
            byte[] data = Encoding.ASCII.GetBytes("ping");
            serverSocket.SendTo(data, data.Length, SocketFlags.None, remote);
            Debug.Log("Ping sent to: " + remote.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending ping: " + e.Message);
        }
    }

    void SendMessageToClient(string message, EndPoint remote)
    {
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            serverSocket.SendTo(data, data.Length, SocketFlags.None, remote);
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending message: " + e.Message);
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
                serverSocket.SendTo(data, data.Length, SocketFlags.None, client.Value);
            }
            catch (Exception e)
            {
                Debug.LogError("Error broadcasting to client: " + e.Message);
            }
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
                list += "- " + client.Key + "\n";
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

        if (serverSocket != null)
        {
            try
            {
                serverSocket.Close();
            }
            catch { }
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
        }
    }
}