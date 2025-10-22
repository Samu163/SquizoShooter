using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



public struct DTO
{
    public string playerName;
    public int level;
    public List<Pokemon> ownedPokemons;

    public DTO(string playerName = "DefaultPlayer", int level = 1, List<Pokemon> ownedPokemons = null)
    {
        this.playerName = playerName;
        this.level = level;
        this.ownedPokemons = ownedPokemons ?? new List<Pokemon>();
    }
}


public class TCPClient : MonoBehaviour
{
    [Header("Connection Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;
    public string playerName = "Player";

    [Header("UI References")]
    public InputField ipInputField;
    public InputField nameInputField;
    public Button connectButton;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI chatLogText;
    public InputField chatInput;
    public Button sendMessageButton;
    public GameObject connectionPanel;
    public GameObject chatPanel;

    private Socket clientSocket;
    private Thread receiveThread;
    private bool isConnected = false;
    private Queue<string> mainThreadActions = new Queue<string>();

    private MemoryStream stream;

    void Start()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(ConnectToServer);

        if (sendMessageButton != null)
            sendMessageButton.onClick.AddListener(SendChatMessage);

        if (connectionPanel != null)
            connectionPanel.SetActive(true);

        if (chatPanel != null)
            chatPanel.SetActive(false);

    }

    public string SerializePokemon(Pokemon pokemon)
    {
        string json = JsonUtility.ToJson(pokemon);
        return json;
    }

    public string SerializeDTO(DTO data)
    {

        string json = JsonUtility.ToJson(data);
        for (int i = 0; i < data.ownedPokemons.Count; i++)
        {
            json += ",";
            json += SerializePokemon(data.ownedPokemons[i]);
        }
        stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(json);
        return json;
    }

    void DeserializeJson()
    {
        var t = new DTO();
        BinaryReader reader = new BinaryReader(stream);
        stream.Seek(0, SeekOrigin.Begin);
        string json = reader.ReadString();
        Debug.Log(json);
        t = JsonUtility.FromJson<DTO>(json);
        Debug.Log(t.playerName.ToString() + " " + t.level.ToString());
        for (int i = 0; i < t.ownedPokemons.Count; i++)
        {
            Debug.Log(t.ownedPokemons[i].name);
        }
    }

    public void ConnectToServer()
    {
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            serverIP = ipInputField.text;
        }

        // Get player name from input field if available
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text))
        {
            playerName = nameInputField.text;
        }

        Thread connectThread = new Thread(ConnectProcess);
        connectThread.Start();
    }

    void ConnectProcess()
    {
        try
        {
            UpdateStatus("Connecting to server...");

            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            clientSocket.Connect(serverEndPoint);

            isConnected = true;
            UpdateStatus("Connected to server!");

            lock (mainThreadActions)
            {
                mainThreadActions.Enqueue("connected");
            }


            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();

            Thread.Sleep(100);

            SendPlayerName();
        }
        catch (Exception e)
        {
            UpdateStatus("Connection failed: " + e.Message);
            Debug.LogError("Connection error: " + e);
            isConnected = false;
        }
    }

    void SendPlayerName()
    {
        try
        {
            string message = "NAME:" + playerName;
            byte[] data = Encoding.ASCII.GetBytes(message);
            clientSocket.Send(data);
            Debug.Log("Player name sent: " + playerName);
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending player name: " + e.Message);
        }
    }

    void ReceiveMessages()
    {
        byte[] buffer = new byte[4096];

        while (isConnected)
        {
            try
            {
                int bytesReceived = clientSocket.Receive(buffer);

                if (bytesReceived == 0)
                {
                    UpdateStatus("Server disconnected");
                    isConnected = false;
                    break;
                }

                string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                Debug.Log("Received from server: " + message);

                ProcessServerMessage(message);
            }
            catch (Exception e)
            {
                if (isConnected)
                {
                    Debug.LogError("Error receiving message: " + e.Message);
                    isConnected = false;
                }
                break;
            }
        }
        Disconnect();
    }

    void ProcessServerMessage(string message)
    {
        if (message.StartsWith("WELCOME:"))
        {
            string serverName = message.Substring(8);
            UpdateStatus("Connected to: " + serverName);
        }
        else if (message == "ping")
        {
            Debug.Log("Received ping from server");
        }
        else if (message.StartsWith("CHAT:"))
        {
            string chatMsg = message.Substring(5);
            lock (mainThreadActions)
            {
                mainThreadActions.Enqueue("chat:" + chatMsg);
            }
        }
    }

    void SendChatMessage()
    {
        if (chatInput != null && !string.IsNullOrEmpty(chatInput.text) && isConnected)
        {
            try
            {
                string message = "CHAT:" + chatInput.text;
                byte[] data = Encoding.ASCII.GetBytes(message);
                clientSocket.Send(data);
                chatInput.text = "";

                //List<Pokemon> pokemons = new List<Pokemon>();
                //pokemons.Add(new Pokemon { name = "Pikachu" });
                //pokemons.Add(new Pokemon { name = "Bulbasur" });
                //pokemons.Add(new Pokemon { name = "Charmander" });
                //pokemons.Add(new Pokemon { name = "Starmie" });

                //DTO newData = new DTO("DefaultPlayer", 1, pokemons);

                //byte[] data = Encoding.ASCII.GetBytes("CHAT:" + SerializeDTO(newData));
                //clientSocket.Send(data);
                //chatInput.text = "";
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending chat message: " + e.Message);
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

                if (action == "connected")
                {
                    if (connectionPanel != null)
                        connectionPanel.SetActive(false);
                    if (chatPanel != null)
                        chatPanel.SetActive(true);
                }
                else if (action.StartsWith("chat:"))
                {
                    UpdateChatLog(action.Substring(5));
                }
                else if (action.StartsWith("status:"))
                {
                    if (statusText != null)
                        statusText.text = action.Substring(7);
                }
            }
        }
    }

    void UpdateStatus(string message)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue("status:" + message);
        }
        Debug.Log("Status: " + message);
    }

    void UpdateChatLog(string message)
    {
        if (chatLogText != null)
        {
            chatLogText.text += message + "\n";
        }
    }

    void Disconnect()
    {
        isConnected = false;

        if (clientSocket != null)
        {
            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
            }
            catch { }
        }
    }

    void OnDestroy()
    {
        Disconnect();

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
        }
    }
}