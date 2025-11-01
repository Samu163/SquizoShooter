using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Globalization;

public class UDPServer : MonoBehaviour
{
    [Header("Server Configuration")]
    public int port = 9050;

    private Socket serverSocket;
    private Thread receiveThread;
    private volatile bool isRunning = false;

    private readonly Dictionary<string, EndPoint> connectedClients = new Dictionary<string, EndPoint>();
    private readonly Dictionary<string, Vector3> playerPositions = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Vector3> playerRotations = new Dictionary<string, Vector3>();
    private readonly object clientsLock = new object();

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
                    int bytes = serverSocket.ReceiveFrom(buffer, ref remoteEP);
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

            // Enviar posiciones de todos los jugadores existentes al nuevo cliente
            SendAllPlayerPositionsToSingleClient(newKey, remote);

            // Notificar a todos sobre el nuevo jugador
            BroadcastMove(newKey, Vector3.zero);
        }
        else if (message.StartsWith("MOVE:"))
        {
            // Formato: MOVE:<clientKey>:x;y;z
            string payload = message.Substring("MOVE:".Length);
            int sep = payload.IndexOf(':');
            if (sep < 0)
            {
                Debug.LogWarning($"[Server] MOVE mal formateado: {message}");
                return;
            }

            string senderKey = payload.Substring(0, sep);
            string coords = payload.Substring(sep + 1);
            string[] parts = coords.Split(';');
            if (parts.Length != 3)
            {
                Debug.LogWarning($"[Server] Coordenadas incompletas: {coords}");
                return;
            }

            // Usar InvariantCulture para parsear con punto decimal
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
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
                        connectedClients[senderKey] = remote;
                        playerPositions[senderKey] = newPos;
                    }
                }

                Debug.Log($"[Server] Actualizando posición de {senderKey}: {newPos}");
                BroadcastMove(senderKey, newPos);
            }
            else
            {
                Debug.LogWarning($"[Server] Error parseando coordenadas: {coords}");
            }
        }
        else if (message.StartsWith("ROTATE"))
        {
            string payload = message.Substring("ROTATE:".Length);
            int sep = payload.IndexOf(':');
            if (sep < 0)
            {
                Debug.LogWarning($"[Server] ROTATE mal formateado: {message}");
                return;
            }

            string senderKey = payload.Substring(0, sep);
            string coords = payload.Substring(sep + 1);
            string[] parts = coords.Split(';');
            if (parts.Length != 3)
            {
                Debug.LogWarning($"[Server] Coordenadas incompletas: {coords}");
                return;
            }

            // Usar InvariantCulture para parsear con punto decimal
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                Vector3 newRot = new Vector3(x, y, z);
                lock (clientsLock)
                {
                    if (playerRotations.ContainsKey(senderKey))
                    {
                        playerRotations[senderKey] = newRot;
                    }
                    else
                    {
                        connectedClients[senderKey] = remote;
                        playerRotations[senderKey] = newRot;
                    }
                }
                Debug.Log($"[Server] Actualizando rotación de {senderKey}: {newRot}");
                BroadcastRotate(senderKey, newRot);
            }
            else
            {
                Debug.LogWarning($"[Server] Error parseando coordenadas: {coords}");
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

                // No enviar al nuevo cliente su propia posición
                if (key == newKey)
                    continue;

                Vector3 pos = kv.Value;

                // Usar InvariantCulture para formatear con punto decimal
                string posStr = string.Format(CultureInfo.InvariantCulture, "{0:F6};{1:F6};{2:F6}",
                                             pos.x, pos.y, pos.z);
                string msg = $"MOVE:{key}:{posStr}";
                SendMessageToClient(msg, remote);

                // Pequeño delay para evitar que lleguen todos los mensajes a la vez
                Thread.Sleep(5);
            }
        }

        Debug.Log($"[Server] Enviadas {playerPositions.Count - 1} posiciones al cliente {newKey}");
    }

    void BroadcastMove(string senderKey, Vector3 pos)
    {
        // Usar InvariantCulture para formatear con punto decimal
        string posStr = string.Format(CultureInfo.InvariantCulture, "{0:F6};{1:F6};{2:F6}",
                                     pos.x, pos.y, pos.z);
        string msg = $"MOVE:{senderKey}:{posStr}";
        byte[] data = Encoding.UTF8.GetBytes(msg);

        List<EndPoint> snapshot = new List<EndPoint>();
        lock (clientsLock)
        {
            foreach (var kv in connectedClients)
                snapshot.Add(kv.Value);
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

    void BroadcastRotate(string senderKey, Vector3 rot)
    {
        string rotStr = string.Format(CultureInfo.InvariantCulture, "{0:F6};{1:F6};{2:F6}",
                                     rot.x, rot.y, rot.z);
        string msg = $"ROTATE:{senderKey}:{rotStr}";
        byte[] data = Encoding.UTF8.GetBytes(msg);

        List<EndPoint> snapshot = new List<EndPoint>();
        lock (clientsLock)
        {
            foreach (var kv in connectedClients)
                snapshot.Add(kv.Value);
        }

        foreach (var ep in snapshot)
        {
            try
            {
                serverSocket.SendTo(data, data.Length, SocketFlags.None, ep);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Server] Error enviando ROTATE a " + ep + ": " + e.Message);
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

        Debug.Log($"[Server] Cliente {key} desconectado");
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