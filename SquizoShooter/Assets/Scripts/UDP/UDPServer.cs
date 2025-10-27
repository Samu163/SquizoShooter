using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class UDPServer : MonoBehaviour
{
    [Header("Server Configuration")]
    public int port = 9050;

    private Socket serverSocket;
    private Thread receiveThread;
    private bool isRunning = false;

    private Dictionary<string, Vector3> playerPositions = new Dictionary<string, Vector3>(); // Almacena las posiciones de los jugadores
    private Dictionary<string, EndPoint> connectedClients = new Dictionary<string, EndPoint>(); // Almacena los clientes conectados

    public void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);  // Mantén el puerto como único (9050)
            serverSocket.Bind(ipep);

            isRunning = true;
            Debug.Log("UDP Server started on port " + port);

            // Iniciar el hilo para recibir mensajes
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error starting server: " + e.Message);
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
            catch (System.Exception e)
            {
                Debug.LogError("Error receiving UDP message: " + e.Message);
            }
        }
    }

    void ProcessClientMessage(string message, EndPoint remote)
    {
        string clientKey = remote.ToString();

        if (message == "HANDSHAKE")
        {
            if (!connectedClients.ContainsKey(clientKey))
            {
                connectedClients[clientKey] = remote;
                playerPositions[clientKey] = Vector3.zero;

                SendWelcomeMessage(remote, clientKey);
                SendAllPlayerPositions(remote); // Enviar posiciones de todos los jugadores al nuevo cliente
            }
        }
        else if (message.StartsWith("MOVE:"))
        {
            string[] parts = message.Substring(5).Split(',');
            float x = float.Parse(parts[0]);
            float y = float.Parse(parts[1]);
            float z = float.Parse(parts[2]);

            if (playerPositions.ContainsKey(clientKey))
            {
                playerPositions[clientKey] = new Vector3(x, y, z);
                BroadcastCubePositions(); // Difundir las nuevas posiciones a todos los clientes
            }
        }
    }

    void SendAllPlayerPositions(EndPoint client)
    {
        Debug.Log("Sending all player positions to new client.");
        foreach (var player in playerPositions)
        {
            // Enviar la posición de cada jugador al nuevo cliente
            string message = "MOVE:" + player.Value.x + "," + player.Value.y + "," + player.Value.z;
            SendMessageToClient(message, client);
            Debug.Log($"Sent position {player.Value} of player {player.Key} to new client.");
        }
    }

    void SendWelcomeMessage(EndPoint remote, string clientKey)
    {
        string welcomeMsg = "WELCOME:" + clientKey; // Enviar el clientKey al cliente
        SendMessageToClient(welcomeMsg, remote);
    }

    void SendMessageToClient(string message, EndPoint remote)
    {
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            serverSocket.SendTo(data, data.Length, SocketFlags.None, remote);
            Debug.Log("Message sent to client: " + message);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error sending message to client: " + e.Message);
        }
    }

    void BroadcastCubePositions()
    {
        // Difundir las posiciones de todos los jugadores conectados a todos los clientes
        foreach (var client in connectedClients)
        {
            // Crear el mensaje con la nueva posición de este jugador
            string message = "MOVE:" + playerPositions[client.Key].x + "," + playerPositions[client.Key].y + "," + playerPositions[client.Key].z;
            byte[] data = Encoding.ASCII.GetBytes(message);

            try
            {
                serverSocket.SendTo(data, data.Length, SocketFlags.None, client.Value);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error broadcasting to client: " + e.Message);
            }
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
