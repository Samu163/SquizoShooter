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
    private readonly Dictionary<string, float> playerHealth = new Dictionary<string, float>();

    // --- NUEVOS CAMPOS PARA HEALTH STATIONS ---
    private readonly Dictionary<int, bool> healStationStates = new Dictionary<int, bool>(); // true = en cooldown
    private const float HEAL_STATION_COOLDOWN_TIME = 5.0f; // Debe coincidir con el cliente
                                                           // ---------------------------------------------

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
                playerHealth[newKey] = 100f; // Asignar vida inicial
            }

            SendMessageToClient($"WELCOME:{newKey}", remote);

            // Enviar posiciones de todos los jugadores existentes al nuevo cliente
            SendAllPlayerPositionsToSingleClient(newKey, remote);

            // --- NUEVO ---
            // Enviar estado de todas las heal stations al nuevo cliente
            SendAllHealStationStatesToSingleClient(remote);
            // -----------

            // Notificar a todos sobre el nuevo jugador
            BroadcastMove(newKey, Vector3.zero);
        }
        else if (message.StartsWith("PLAYERDATA:"))
        {
            string payload = message.Substring("PLAYERDATA:".Length);
            int sep = payload.IndexOf(':');
            if (sep < 0) return;

            string senderKey = payload.Substring(0, sep);
            string valueStr = payload.Substring(sep + 1);

            if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float health))
            {
                lock (clientsLock)
                {
                    // Guarda la vida en un nuevo diccionario
                    playerHealth[senderKey] = health;
                }

                Debug.Log($"[Server] Vida actualizada: {senderKey} -> {health}");
                BroadcastPlayerHealth(senderKey, health);
            }
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

        // --- INICIO DEL NUEVO BLOQUE HEAL_REQUEST ---
        else if (message.StartsWith("HEAL_REQUEST:"))
        {
            // Formato: HEAL_REQUEST:ID_JUGADOR:ID_ESTACION
            string[] parts = message.Split(':');
            if (parts.Length != 3)
            {
                Debug.LogWarning($"[Server] HEAL_REQUEST mal formateado: {message}");
                return;
            }

            string senderKey = parts[1];
            if (!int.TryParse(parts[2], out int stationID))
            {
                Debug.LogWarning($"[Server] HEAL_REQUEST ID de estación inválido: {parts[2]}");
                return;
            }

            bool wasApproved = false;
            float newHealth = 0f;

            lock (clientsLock)
            {
                // 1. Verificar si la estación ya está en cooldown
                if (healStationStates.TryGetValue(stationID, out bool isCooldown) && isCooldown)
                {
                    // Ya está en cooldown, no hacer nada.
                    Debug.Log($"[Server] Petición de cura para {stationID} denegada (ya en cooldown).");
                    wasApproved = false;
                }
                else
                {
                    // 2. No está en cooldown. ¡Aprobar la cura!
                    Debug.Log($"[Server] Petición de cura para {stationID} APROBADA. Iniciando cooldown.");
                    wasApproved = true;

                    // 3. Poner la estación en cooldown
                    healStationStates[stationID] = true;

                    // 4. Curar al jugador (asumimos que cura al máximo, 100f)
                    if (playerHealth.ContainsKey(senderKey))
                    {
                        playerHealth[senderKey] = 100f;
                        newHealth = 100f;
                    }
                }
            }

            // 5. Si fue aprobada, notificar a todos
            if (wasApproved)
            {
                // a. Notificar a todos la nueva vida del jugador
                if (newHealth > 0)
                {
                    BroadcastPlayerHealth(senderKey, newHealth);
                }

                // b. Notificar a todos que la estación está en cooldown (estado 1)
                BroadcastHealStationState(stationID, 1);

                // c. Iniciar un timer para quitar el cooldown
                Timer cooldownTimer = new Timer(
                    (state) => EndHealStationCooldown(stationID), // Llama a la función cuando se cumple
                    null, // Sin estado extra
                    (int)(HEAL_STATION_COOLDOWN_TIME * 1000), // Tiempo en milisegundos
                    Timeout.Infinite // No se repite
                );
            }
        }
        // --- FIN DEL NUEVO BLOQUE HEAL_REQUEST ---

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

    // --- NUEVA FUNCIÓN (Callback del Timer) ---
    void EndHealStationCooldown(int stationID)
    {
        Debug.Log($"[Server] Cooldown de HealStation {stationID} terminado.");

        lock (clientsLock)
        {
            healStationStates[stationID] = false; // Marcar como disponible
        }

        // Broadcast el nuevo estado (disponible = 0)
        BroadcastHealStationState(stationID, 0);
    }
    // ----------------------------------------

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

    // --- NUEVA FUNCIÓN ---
    void SendAllHealStationStatesToSingleClient(EndPoint remote)
    {
        lock (clientsLock)
        {
            if (healStationStates.Count == 0) return;

            Debug.Log($"[Server] Enviando {healStationStates.Count} estados de HealStation al nuevo cliente...");

            foreach (var kvp in healStationStates)
            {
                int stationID = kvp.Key;
                bool isCooldown = kvp.Value;
                int stateCode = isCooldown ? 1 : 0; // 1 = cooldown, 0 = disponible

                string msg = $"HEAL_STATION_DATA:{stationID}:{stateCode}";
                SendMessageToClient(msg, remote);

                Thread.Sleep(5); // Pequeño delay
            }
        }
    }
    // ----------------------

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

    void BroadcastPlayerHealth(string senderKey, float health)
    {
        string msg = string.Format(CultureInfo.InvariantCulture, "PLAYERDATA:{0}:{1:F1}", senderKey, health);
        byte[] data = Encoding.UTF8.GetBytes(msg);

        List<EndPoint> snapshot;
        lock (clientsLock)
        {
            snapshot = new List<EndPoint>(connectedClients.Values);
        }

        foreach (var ep in snapshot)
        {
            try { serverSocket.SendTo(data, data.Length, SocketFlags.None, ep); }
            catch (Exception e) { Debug.LogWarning("[Server] Error enviando PLAYERDATA: " + e.Message); }
        }
    }

    // --- NUEVA FUNCIÓN ---
    void BroadcastHealStationState(int stationID, int stateCode)
    {
        // stateCode: 1 = en cooldown, 0 = disponible
        string msg = $"HEAL_STATION_DATA:{stationID}:{stateCode}";
        byte[] data = Encoding.UTF8.GetBytes(msg);

        List<EndPoint> snapshot;
        lock (clientsLock)
        {
            snapshot = new List<EndPoint>(connectedClients.Values);
        }

        Debug.Log($"[Server] Transmitiendo estado de HealStation {stationID} a {snapshot.Count} clientes. Estado: {stateCode}");

        foreach (var ep in snapshot)
        {
            try { serverSocket.SendTo(data, data.Length, SocketFlags.None, ep); }
            catch (Exception e) { Debug.LogWarning("[Server] Error enviando HEAL_STATION_DATA: " + e.Message); }
        }
    }
    // ----------------------

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
            if (playerHealth.ContainsKey(key))
            {
                playerHealth.Remove(key);
            }
            if (playerRotations.ContainsKey(key))
            {
                playerRotations.Remove(key);
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