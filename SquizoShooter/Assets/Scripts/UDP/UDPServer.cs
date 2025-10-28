using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class UDPServer : MonoBehaviour
{
    [Header("Server Configuration")]
    public int port = 9050;

    private Socket serverSocket;
    private Thread receiveThread;
    private volatile bool isRunning = false;

    // clientKey -> endpoint
    private readonly Dictionary<string, EndPoint> connectedClients = new Dictionary<string, EndPoint>();
    private readonly Dictionary<string, Vector3> playerPositions = new Dictionary<string, Vector3>();
    private readonly object clientsLock = new object();

    void Start()
    {
        // Puedes arrancar el servidor desde Start si quieres:
        // StartServer();
    }

    public void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, port);
            serverSocket.Bind(ipep);

            isRunning = true;
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();

            Debug.Log("[Server] UDP server started on port " + port);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Server] Error starting UDP server: " + ex.Message);
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            while (isRunning)
            {
                try
                {
                    int bytes = serverSocket.ReceiveFrom(buffer, ref remoteEP); // blocking
                    if (bytes > 0)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                        Debug.Log($"[Server] Recibido de {remoteEP}: {msg}");
                        ProcessClientMessage(msg, remoteEP);
                    }
                }
                catch (SocketException se)
                {
                    if (!isRunning) break;
                    Debug.LogWarning("[Server] SocketException en ReceiveLoop: " + se.Message);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Server] Error en ReceiveLoop: " + e.Message);
                }
            }
        }
        finally
        {
            Debug.Log("[Server] ReceiveLoop terminado");
        }
    }

    void ProcessClientMessage(string message, EndPoint remote)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (message == "HANDSHAKE")
        {
            string newKey = Guid.NewGuid().ToString();

            lock (clientsLock)
            {
                connectedClients[newKey] = remote;
                playerPositions[newKey] = Vector3.zero;
            }

            SendMessageToClient($"WELCOME:{newKey}", remote);

            SendAllPlayerPositionsToSingleClient(newKey, remote);

            BroadcastMove(newKey, playerPositions[newKey]);
        }
        else if (message.StartsWith("MOVE:"))
        {
            // Formato: MOVE:<clientKey>:x,y,z
            string payload = message.Substring("MOVE:".Length);
            int sep = payload.IndexOf(':');
            if (sep < 0) return;
            string senderKey = payload.Substring(0, sep);
            string coords = payload.Substring(sep + 1);
            string[] parts = coords.Split(',');
            if (parts.Length != 3) return;

            if (float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z))
            {
                Vector3 newPos = new Vector3(x, y, z);
                lock (clientsLock)
                {
                    if (playerPositions.ContainsKey(senderKey))
                    {
                        playerPositions[senderKey] = newPos;
                    }
                    else
                    {
                        // si no estaba registrado, registrar con el endpoint actual
                        connectedClients[senderKey] = remote;
                        playerPositions[senderKey] = newPos;
                    }
                }
                // Difundir el movimiento a todos
                BroadcastMove(senderKey, newPos);
            }
        }
        else if (message.StartsWith("GOODBYE:"))
        {
            string key = message.Substring("GOODBYE:".Length);
            RemoveClientByKey(key);
        }
        else
        {
            Debug.LogWarning("[Server] Mensaje desconocido: " + message);
        }
    }

    void SendAllPlayerPositionsToSingleClient(string newKey, EndPoint remote)
    {
        lock (clientsLock)
        {
            foreach (var kv in playerPositions)
            {
                string key = kv.Key;
                Vector3 pos = kv.Value;
                // enviar MOVE:<key>:x,y,z
                string msg = $"MOVE:{key}:{pos.x.ToString("G9")},{pos.y.ToString("G9")},{pos.z.ToString("G9")}";
                SendMessageToClient(msg, remote);
            }
        }
    }

    void BroadcastCubePositions()
    {
        foreach (var receiver in connectedClients)
        {
            foreach (var player in playerPositions)
            {
                string message = $"MOVE:{player.Key}:{player.Value.x},{player.Value.y},{player.Value.z}";
                byte[] data = Encoding.ASCII.GetBytes(message);
                serverSocket.SendTo(data, data.Length, SocketFlags.None, receiver.Value);
            }
        }
    }

    void BroadcastMove(string senderKey, Vector3 pos)
    {
        string msg = $"MOVE:{senderKey}:{pos.x.ToString("G9")},{pos.y.ToString("G9")},{pos.z.ToString("G9")}";
        byte[] data = Encoding.UTF8.GetBytes(msg);

        // Snapshot para iterar sin bloquear long time
        List<EndPoint> snapshot = new List<EndPoint>();
        lock (clientsLock)
        {
            foreach (var kv in connectedClients) snapshot.Add(kv.Value);
        }

        foreach (var ep in snapshot)
        {
            try
            {
                serverSocket.SendTo(data, data.Length, SocketFlags.None, ep);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Server] Error enviando MOVE a " + ep + ": " + e.Message);
            }
        }
    }

    void SendMessageToClient(string message, EndPoint remote)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            serverSocket.SendTo(data, data.Length, SocketFlags.None, remote);
        }
        catch (Exception e)
        {
            Debug.LogError("[Server] Error enviando a cliente: " + e.Message);
        }
    }

    void RemoveClientByKey(string key)
    {
        lock (clientsLock)
        {
            if (connectedClients.ContainsKey(key))
            {
                connectedClients.Remove(key);
            }
            if (playerPositions.ContainsKey(key))
            {
                playerPositions.Remove(key);
            }
        }

        // Notificar a los demás
        string goodbyeMsg = "GOODBYE:" + key;
        byte[] data = Encoding.UTF8.GetBytes(goodbyeMsg);
        List<EndPoint> snapshot;
        lock (clientsLock)
        {
            snapshot = new List<EndPoint>(connectedClients.Values);
        }
        foreach (var ep in snapshot)
        {
            try { serverSocket.SendTo(data, data.Length, SocketFlags.None, ep); }
            catch { }
        }
    }

    public void StopServer()
    {
        isRunning = false;
        try { serverSocket?.Close(); } catch { }
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(500);
        serverSocket = null;
        Debug.Log("[Server] Stopped");
    }

    void OnDestroy()
    {
        isRunning = false;
        try { serverSocket?.Close(); } catch { }
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(500);
    }
}
