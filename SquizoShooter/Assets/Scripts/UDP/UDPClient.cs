using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPClient : MonoBehaviour
{
    [Header("Connection Settings")]
    public string serverIP = "127.0.0.1"; // IP predeterminada
    public int serverPort = 9050; // Puerto del servidor
    public int clientPort = 9002; // Puerto del cliente (modificado a 9002)

    public GameObject cubePrefab; // Prefab del cubo

    private Socket clientSocket;
    private Thread receiveThread;
    private bool isConnected = false;
    private EndPoint serverEndPoint;
    private string clientKey; // Identificador único del cliente
    private Dictionary<string, GameObject> playerCubes = new Dictionary<string, GameObject>(); // Almacena los cubos de todos los jugadores

    private Queue<Action> mainThreadActions = new Queue<Action>(); // Cola para manejar las instancias en el hilo principal

    public bool IsConnected { get { return isConnected; } }

    // Método para iniciar la conexión al servidor de forma automática
    public void StartConnection()
    {
        Thread connectThread = new Thread(ConnectProcess);
        connectThread.Start();
    }

    void ConnectProcess()
    {
        try
        {
            // Establecer el punto de conexión al servidor
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Asignar un puerto al cliente
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, clientPort);
            clientSocket.Bind(clientEndPoint);

            // Enviar el mensaje de "HANDSHAKE" al servidor
            string handshake = "HANDSHAKE";
            byte[] data = Encoding.ASCII.GetBytes(handshake);
            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEndPoint);

            isConnected = true;
            Debug.Log("Connected to server on port: " + clientPort);

            // Iniciar el hilo de recepción de mensajes
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("Error connecting to server: " + e.Message);
            isConnected = false;
        }
    }

    void ReceiveMessages()
    {
        byte[] buffer = new byte[4096];

        while (isConnected)
        {
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint remote = (EndPoint)sender;

                // Recibir los mensajes del servidor
                int bytesReceived = clientSocket.ReceiveFrom(buffer, ref remote);

                if (bytesReceived > 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
                    Debug.Log("Received from server: " + message);

                    mainThreadActions.Enqueue(() => ProcessServerMessage(message));
                }
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
            clientKey = message.Substring(8); // Obtener el clientKey del servidor
            Debug.Log("Received client key: " + clientKey);
            SendCubeMovement(Vector3.zero); // Enviar la posición inicial del cubo
        }
        else if (message.StartsWith("MOVE:"))
        {
            string[] parts = message.Substring(5).Split(',');
            float x = float.Parse(parts[0]);
            float y = float.Parse(parts[1]);
            float z = float.Parse(parts[2]);
            Vector3 newPosition = new Vector3(x, y, z);

            // Mover el cubo del jugador según la posición recibida
            if (!playerCubes.ContainsKey(clientKey))
            {
                mainThreadActions.Enqueue(() => InstantiateCube(newPosition, clientKey));
            }
            else
            {
                playerCubes[clientKey].transform.position = newPosition;
            }
        }
    }

    void InstantiateCube(Vector3 position, string key)
    {
        if (!playerCubes.ContainsKey(key))
        {
            GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);
            playerCubes[key] = newCube;
            Debug.Log("Instantiated new cube for player: " + key + " at position " + position);
        }
        else
        {
            playerCubes[key].transform.position = position;
            Debug.Log("Updated position for existing cube: " + key);
        }
    }

    public void SendCubeMovement(Vector3 movement)
    {
        try
        {
            string message = "MOVE:" + movement.x + "," + movement.y + "," + movement.z;
            byte[] data = Encoding.ASCII.GetBytes(message);
            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEndPoint);
        }
        catch (Exception e)
        {
            Debug.LogError("Error sending move data: " + e.Message);
        }
    }

    void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            clientSocket.Close();
            Debug.Log("Disconnected from server");
        }
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    void Update()
    {
        while (mainThreadActions.Count > 0)
        {
            mainThreadActions.Dequeue().Invoke();
        }
    }
}
