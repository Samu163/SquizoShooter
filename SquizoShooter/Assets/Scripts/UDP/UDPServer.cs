using System;
using System.Collections.Generic;
using System.IO;
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

    private readonly Dictionary<string, EndPoint> connectedClients = new Dictionary<string, EndPoint>();
    private readonly Dictionary<string, Vector3> playerPositions = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Vector3> playerRotations = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, float> playerHealth = new Dictionary<string, float>();
    private readonly Dictionary<string, int> playerWeapons = new Dictionary<string, int>();

    private readonly Dictionary<string, string> playerNames = new Dictionary<string, string>();
    private readonly Dictionary<string, bool> playerReadyStatus = new Dictionary<string, bool>();


    private readonly Dictionary<int, bool> healStationStates = new Dictionary<int, bool>();
    private const float HEAL_STATION_COOLDOWN_TIME = 5.0f;

    private int maxRoundsToWin = 5;
    private Dictionary<string, int> playerScores = new Dictionary<string, int>();
    private bool isRoundEnding = false;
    private bool isGameStarted = false; 


    private readonly object clientsLock = new object();

    private enum MessageType : byte
    {
        Handshake = 1,
        Welcome = 2,
        PlayerData = 3,
        ShootAnim = 4,
        Shot = 5,
        Move = 6,
        Rotate = 7,
        HealRequest = 8,
        HealStationData = 9,
        KillConfirmed = 10,
        Goodbye = 11,
        WeaponChange = 12,
        WeaponStationData = 13,
        WeaponStationRequest = 15,
        LobbyData = 20, 
        ClientReady = 21, 
        StartGame = 22,
        RoundWin = 30, 
        MatchWin = 31,
        RoundReset = 32
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
                    int bytes = serverSocket.ReceiveFrom(buffer, ref remoteEP);
                    if (bytes > 0)
                    {
                        byte[] data = new byte[bytes];
                        Array.Copy(buffer, data, bytes);
                        ProcessClientMessage(data, remoteEP);
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

    void ProcessClientMessage(byte[] data, EndPoint remote)
    {
        if (data == null || data.Length == 0) return;

        try
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                MessageType msgType = (MessageType)reader.ReadByte();

                switch (msgType)
                {
                    case MessageType.Handshake:
                        HandleHandshake(remote);
                        break;
                    case MessageType.ClientReady: 
                        HandleClientReady(reader); 
                        break;

                    case MessageType.PlayerData:
                        HandlePlayerData(reader);
                        break;

                    case MessageType.ShootAnim:
                        HandleShootAnim(reader);
                        break;

                    case MessageType.Shot:
                        HandleShot(reader);
                        break;

                    case MessageType.Move:
                        HandleMove(reader, remote);
                        break;

                    case MessageType.Rotate:
                        HandleRotate(reader, remote);
                        break;

                    case MessageType.HealRequest:
                        HandleHealRequest(reader);
                        break;

                    case MessageType.WeaponChange:
                        HandleWeaponSwitchNotification(reader); 
                        break;

                    case MessageType.WeaponStationRequest:
                        HandleWeaponStationRequest(reader);
                        break;

                    case MessageType.Goodbye:
                        HandleGoodbye(reader);
                        break;

                    default:
                        Debug.LogWarning($"[Server] Tipo de mensaje desconocido: {msgType}");
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Server] Error procesando mensaje: {e.Message}");
        }
    }

    void HandleWeaponSwitchNotification(BinaryReader reader)
    {
        string senderKey = reader.ReadString();
        int weaponID = reader.ReadInt32();

        lock (clientsLock)
        {
            playerWeapons[senderKey] = weaponID;
        }
        BroadcastWeaponChange(senderKey, weaponID);
    }

    void HandleWeaponStationRequest(BinaryReader reader)
    {
        string senderKey = reader.ReadString();
        int stationID = reader.ReadInt32();
        int weaponID = reader.ReadInt32();

        bool wasApproved = false;

        lock (clientsLock)
        {
            if (healStationStates.TryGetValue(stationID, out bool isCooldown) && isCooldown)
            {
                wasApproved = false;
            }
            else
            {
                Debug.Log($"[Server] WeaponStation {stationID} aprobada para {senderKey}. Arma: {weaponID}");
                wasApproved = true;
                healStationStates[stationID] = true; 
                playerWeapons[senderKey] = weaponID; 
            }
        }

        if (wasApproved)
        {
            BroadcastWeaponChange(senderKey, weaponID);
            BroadcastWeaponStationState(stationID, 1);

            Timer cooldownTimer = new Timer(
                (state) => EndWeaponStationCooldown(stationID),
                null, (int)(HEAL_STATION_COOLDOWN_TIME * 1000), Timeout.Infinite
            );
        }
    }
    void HandleHandshake(EndPoint remote)
    {
        string newKey = Guid.NewGuid().ToString();
        string randomName = NameGenerator.GetRandomName(); // Asignar nombre aleatorio

        lock (clientsLock)
        {
            connectedClients[newKey] = remote;

            // Datos Iniciales Juego
            playerPositions[newKey] = Vector3.zero;
            playerHealth[newKey] = 100f;
            playerWeapons[newKey] = 1;

            // Datos Iniciales Lobby
            playerNames[newKey] = randomName;
            playerReadyStatus[newKey] = false;
        }

        Debug.Log($"[Server] HANDSHAKE de {randomName} ({newKey})");
        SendWelcome(newKey, remote, isGameStarted);
        BroadcastLobbyState();
        if (isGameStarted)
        {
            SendAllPlayerPositionsToSingleClient(newKey, remote);
            SendAllHealStationStatesToSingleClient(remote);
        }
    }
    void HandleClientReady(BinaryReader reader)
    {
        string key = reader.ReadString();
        bool isReady = reader.ReadBoolean();

        lock (clientsLock)
        {
            if (playerReadyStatus.ContainsKey(key))
                playerReadyStatus[key] = isReady;
        }
        BroadcastLobbyState();
    }
    public void RequestStartGame(int rounds)
    {
        maxRoundsToWin = rounds;
        isGameStarted = true; 

        lock (clientsLock)
        {
            playerScores.Clear();
            foreach (var key in connectedClients.Keys) playerScores[key] = 0;
        }

        BroadcastGameStart(rounds);
    }
    void BroadcastLobbyState()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.LobbyData);

            lock (clientsLock)
            {
                writer.Write(connectedClients.Count);
                foreach (var key in connectedClients.Keys)
                {
                    writer.Write(key);
                    writer.Write(playerNames.ContainsKey(key) ? playerNames[key] : "Unknown");
                    writer.Write(playerReadyStatus.ContainsKey(key) ? playerReadyStatus[key] : false);
                }
            }

            byte[] data = ms.ToArray();
            List<EndPoint> snapshot;
            lock (clientsLock) snapshot = new List<EndPoint>(connectedClients.Values);
            foreach (var ep in snapshot) SendBinaryToClient(data, ep);
        }
    }

    void BroadcastGameStart()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.StartGame);
            byte[] data = ms.ToArray();

            List<EndPoint> snapshot;
            lock (clientsLock) snapshot = new List<EndPoint>(connectedClients.Values);
            foreach (var ep in snapshot) SendBinaryToClient(data, ep);
        }
    }
    void HandlePlayerData(BinaryReader reader)
    {
        string senderKey = reader.ReadString();
        float health = reader.ReadSingle();

        lock (clientsLock)
        {
            playerHealth[senderKey] = health;
        }

        Debug.Log($"[Server] Vida actualizada: {senderKey} -> {health}");
        BroadcastPlayerHealth(senderKey, health);
    }

    void HandleShootAnim(BinaryReader reader)
    {
        string shooterKey = reader.ReadString();
        Debug.Log($"[Server] SHOOT_ANIM de {shooterKey}");
        BroadcastShootAnim(shooterKey);
    }

    void HandleShot(BinaryReader reader)
    {
        string shooterKey = reader.ReadString();
        string targetKey = reader.ReadString();
        float damage = reader.ReadSingle();

        if (shooterKey == targetKey)
        {
            Debug.LogWarning($"[Server] SHOT ignorado: shooter == target ({shooterKey}).");
            return;
        }

        bool wasKill = false;
        float newHealth = 0f;

        lock (clientsLock)
        {
            float currentHealth = 100f;
            if (playerHealth.ContainsKey(targetKey))
                currentHealth = playerHealth[targetKey];
            else
                playerHealth[targetKey] = currentHealth;

            newHealth = Mathf.Clamp(currentHealth - damage, 0f, 100f);
            playerHealth[targetKey] = newHealth;

            if (newHealth <= 0f && currentHealth > 0f)
            {
                wasKill = true;
                Debug.Log($"[Server] ¡KILL! {shooterKey} eliminó a {targetKey}");
            }

            Debug.Log($"[Server] SHOT recibido: {shooterKey} -> {targetKey} dmg={damage} newHealth={newHealth}");
            BroadcastPlayerHealth(targetKey, newHealth);
        }

        if (wasKill)
        {
            SendKillConfirmation(shooterKey);
            CheckRoundWinCondition();

        }
    }
    void CheckRoundWinCondition()
    {
        if (!isGameStarted || isRoundEnding || connectedClients.Count < 2) return;

        int aliveCount = 0;
        string lastAliveKey = "";

        lock (clientsLock)
        {
            foreach (var kvp in connectedClients)
            {
                if (playerHealth.ContainsKey(kvp.Key) && playerHealth[kvp.Key] > 0)
                {
                    aliveCount++;
                    lastAliveKey = kvp.Key;
                }
            }
        }

        if (aliveCount <= 1)
        {
            StartRoundEndSequence(lastAliveKey);
        }
    }
    void StartRoundEndSequence(string winnerKey)
    {
        isRoundEnding = true;

        string winnerName = "Nadie";
        bool matchWon = false;

        lock (clientsLock)
        {
            if (!string.IsNullOrEmpty(winnerKey))
            {
                if (!playerScores.ContainsKey(winnerKey)) playerScores[winnerKey] = 0;
                playerScores[winnerKey]++;

                if (playerScores[winnerKey] >= maxRoundsToWin) matchWon = true;

                if (playerNames.ContainsKey(winnerKey)) winnerName = playerNames[winnerKey];
            }
        }

        if (matchWon)
        {
            Debug.Log($"[Server] Game Ended" +
                $"Winner:" +
                $" {winnerName}");
            BroadcastMatchWin(winnerName);

            // Esperar 5 segundos y volver al Lobby (reseteando el server)
            new System.Threading.Timer(_ => ResetToLobby(), null, 5000, Timeout.Infinite);
        }
        else
        {
            Debug.Log($"[Server] Winner: {winnerName}");
            BroadcastRoundWin(winnerKey, winnerName);

            // Esperar 3 segundos y reiniciar ronda
            new System.Threading.Timer(_ => ResetRound(), null, 3000, Timeout.Infinite);
        }
    }
    void ResetRound()
    {
        lock (clientsLock)
        {
            List<string> keys = new List<string>(playerHealth.Keys);
            foreach (var k in keys) playerHealth[k] = 100f;
        }
        isRoundEnding = false;
        BroadcastRoundReset();
    }

    void ResetToLobby()
    {
        isGameStarted = false;
        isRoundEnding = false;

        lock (clientsLock)
        {
            List<string> keys = new List<string>(playerHealth.Keys);
            foreach (var k in keys) playerHealth[k] = 100f;

            List<string> rKeys = new List<string>(playerReadyStatus.Keys);
            foreach (var k in rKeys) playerReadyStatus[k] = false;
        }
        BroadcastLobbyState();
    }

    void BroadcastGameStart(int maxRounds)
    {
        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((byte)MessageType.StartGame);
            w.Write(maxRounds); 
            byte[] data = ms.ToArray();
            List<EndPoint> eps; lock (clientsLock) eps = new List<EndPoint>(connectedClients.Values);
            foreach (var e in eps) SendBinaryToClient(data, e);
        }
    }

    void BroadcastRoundWin(string winnerKey, string winnerName)
    {
        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((byte)MessageType.RoundWin);
            w.Write(winnerKey);
            w.Write(winnerName);
            byte[] data = ms.ToArray();
            List<EndPoint> eps; lock (clientsLock) eps = new List<EndPoint>(connectedClients.Values);
            foreach (var e in eps) SendBinaryToClient(data, e);
        }
    }

    void BroadcastMatchWin(string winnerName)
    {
        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((byte)MessageType.MatchWin);
            w.Write(winnerName);
            byte[] data = ms.ToArray();
            List<EndPoint> eps; lock (clientsLock) eps = new List<EndPoint>(connectedClients.Values);
            foreach (var e in eps) SendBinaryToClient(data, e);
        }
    }

    void BroadcastRoundReset()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((byte)MessageType.RoundReset);
            byte[] data = ms.ToArray();
            List<EndPoint> eps; lock (clientsLock) eps = new List<EndPoint>(connectedClients.Values);
            foreach (var ep in eps) SendBinaryToClient(data, ep);
        }
    }

    void HandleMove(BinaryReader reader, EndPoint remote)
    {
        string senderKey = reader.ReadString();
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();

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

    void HandleRotate(BinaryReader reader, EndPoint remote)
    {
        string senderKey = reader.ReadString();
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();

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

    void HandleHealRequest(BinaryReader reader)
    {
        string senderKey = reader.ReadString();
        int stationID = reader.ReadInt32();

        bool wasApproved = false;
        float newHealth = 0f;

        lock (clientsLock)
        {
            if (healStationStates.TryGetValue(stationID, out bool isCooldown) && isCooldown)
            {
                Debug.Log($"[Server] Petición de cura para {stationID} denegada (ya en cooldown).");
                wasApproved = false;
            }
            else
            {
                Debug.Log($"[Server] Petición de cura para {stationID} APROBADA. Iniciando cooldown.");
                wasApproved = true;

                healStationStates[stationID] = true;

                if (playerHealth.ContainsKey(senderKey))
                {
                    playerHealth[senderKey] = 100f;
                    newHealth = 100f;
                }
            }
        }

        if (wasApproved)
        {
            if (newHealth > 0)
            {
                BroadcastPlayerHealth(senderKey, newHealth);
            }
            BroadcastHealStationState(stationID, 1);
            Timer cooldownTimer = new Timer(
                (state) => EndHealStationCooldown(stationID),
                null,
                (int)(HEAL_STATION_COOLDOWN_TIME * 1000),
                Timeout.Infinite
            );
        }
    }

    void HandleWeaponRequest(BinaryReader reader)
    {
        string senderKey = reader.ReadString();
        int stationID = reader.ReadInt32();
        int weaponID = reader.ReadInt32(); 

        bool wasApproved = false;

        lock (clientsLock)
        {
            if (healStationStates.TryGetValue(stationID, out bool isCooldown) && isCooldown)
            {
                Debug.Log($"[Server] Petición de arma para {stationID} denegada (ya en cooldown).");
                wasApproved = false;
            }
            else
            {
                Debug.Log($"[Server] Petición de arma para {stationID} APROBADA. Iniciando cooldown y cambiando a arma {weaponID}.");
                wasApproved = true;

                healStationStates[stationID] = true;

                playerWeapons[senderKey] = weaponID;
            }
        }

        if (wasApproved)
        {
            BroadcastWeaponChange(senderKey, weaponID);

            BroadcastWeaponStationState(stationID, 1);

            Timer cooldownTimer = new Timer(
                (state) => EndWeaponStationCooldown(stationID),
                null,
                (int)(HEAL_STATION_COOLDOWN_TIME * 1000),
                Timeout.Infinite
            );
        }
    }

    void HandleGoodbye(BinaryReader reader)
    {
        string key = reader.ReadString();
        RemoveClientByKey(key);
    }

    void EndHealStationCooldown(int stationID)
    {
        Debug.Log($"[Server] Cooldown de HealStation {stationID} terminado.");

        lock (clientsLock)
        {
            healStationStates[stationID] = false;
        }

        BroadcastHealStationState(stationID, 0);
    }

    void EndWeaponStationCooldown(int stationID)
    {
        Debug.Log($"[Server] Cooldown de WeaponStation {stationID} terminado.");

        lock (clientsLock)
        {
            healStationStates[stationID] = false;
        }

        BroadcastWeaponStationState(stationID, 0); 
    }

    void SendKillConfirmation(string shooterKey)
    {
        EndPoint shooterEndPoint;
        lock (clientsLock)
        {
            if (!connectedClients.TryGetValue(shooterKey, out shooterEndPoint))
            {
                Debug.LogWarning($"[Server] No se pudo encontrar EndPoint para shooter: {shooterKey}");
                return;
            }
        }

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.KillConfirmed);
            writer.Write(shooterKey);

            byte[] data = ms.ToArray();
            SendBinaryToClient(data, shooterEndPoint);
        }

        Debug.Log($"[Server] Enviado KILL_CONFIRMED a {shooterKey}");
    }

    void SendWelcome(string clientKey, EndPoint remote, bool gameInProgress)
    {
        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(ms))
        {
            w.Write((byte)MessageType.Welcome);
            w.Write(clientKey);
            w.Write(gameInProgress);

            byte[] data = ms.ToArray();
            SendBinaryToClient(data, remote);
        }
    }

    void SendAllPlayerPositionsToSingleClient(string newKey, EndPoint remote)
    {
        lock (clientsLock)
        {
            foreach (var kv in playerPositions)
            {
                string key = kv.Key;
                if (key == newKey) continue;

                Vector3 pos = kv.Value;

                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)MessageType.Move);
                    writer.Write(key);
                    writer.Write(pos.x);
                    writer.Write(pos.y);
                    writer.Write(pos.z);

                    byte[] data = ms.ToArray();
                    SendBinaryToClient(data, remote);
                }

                if (playerHealth.TryGetValue(key, out float h))
                {
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(ms))
                    {
                        writer.Write((byte)MessageType.PlayerData);
                        writer.Write(key);
                        writer.Write(h);

                        byte[] data = ms.ToArray();
                        SendBinaryToClient(data, remote);
                    }
                }

                // Send weapon state
                if (playerWeapons.TryGetValue(key, out int weaponID))
                {
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(ms))
                    {
                        writer.Write((byte)MessageType.WeaponChange);
                        writer.Write(key);
                        writer.Write(weaponID);

                        byte[] data = ms.ToArray();
                        SendBinaryToClient(data, remote);
                    }
                }

                Thread.Sleep(5);
            }
        }

        Debug.Log($"[Server] Enviadas {playerPositions.Count - 1} posiciones al cliente {newKey}");
    }

    void SendAllHealStationStatesToSingleClient(EndPoint remote)
    {
        lock (clientsLock)
        {
            if (healStationStates.Count == 0) return;

            Debug.Log($"[Server] Enviando {healStationStates.Count} estados de HealStation/WeaponStation al nuevo cliente...");

            foreach (var kvp in healStationStates)
            {
                int stationID = kvp.Key;
                bool isCooldown = kvp.Value;
                int stateCode = isCooldown ? 1 : 0;


                MessageType msgToSend = MessageType.HealStationData;
                if (stationID > 100)
                {
                    msgToSend = MessageType.WeaponStationData;
                }


                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)msgToSend);
                    writer.Write(stationID);
                    writer.Write(stateCode);

                    byte[] data = ms.ToArray();
                    SendBinaryToClient(data, remote);
                }

                Thread.Sleep(5);
            }
        }
    }

    void BroadcastMove(string senderKey, Vector3 pos)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Move);
            writer.Write(senderKey);
            writer.Write(pos.x);
            writer.Write(pos.y);
            writer.Write(pos.z);

            byte[] data = ms.ToArray();

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
    }

    void BroadcastRotate(string senderKey, Vector3 rot)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Rotate);
            writer.Write(senderKey);
            writer.Write(rot.x);
            writer.Write(rot.y);
            writer.Write(rot.z);

            byte[] data = ms.ToArray();

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
    }

    void BroadcastPlayerHealth(string senderKey, float health)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.PlayerData);
            writer.Write(senderKey);
            writer.Write(health);

            byte[] data = ms.ToArray();

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
    }

    void BroadcastShootAnim(string shooterKey)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.ShootAnim);
            writer.Write(shooterKey);

            byte[] data = ms.ToArray();

            List<EndPoint> snapshot;
            lock (clientsLock)
            {
                snapshot = new List<EndPoint>(connectedClients.Values);
            }

            foreach (var ep in snapshot)
            {
                try { serverSocket.SendTo(data, data.Length, SocketFlags.None, ep); }
                catch (Exception e) { Debug.LogWarning("[Server] Error enviando SHOOT_ANIM: " + e.Message); }
            }
        }
    }

    void BroadcastHealStationState(int stationID, int stateCode)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.HealStationData);
            writer.Write(stationID);
            writer.Write(stateCode);

            byte[] data = ms.ToArray();

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
    }

    void BroadcastWeaponChange(string senderKey, int weaponID)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.WeaponChange);
            writer.Write(senderKey);
            writer.Write(weaponID);

            byte[] data = ms.ToArray();

            List<EndPoint> snapshot;
            lock (clientsLock)
            {
                snapshot = new List<EndPoint>(connectedClients.Values);
            }

            foreach (var ep in snapshot)
            {
                try { serverSocket.SendTo(data, data.Length, SocketFlags.None, ep); }
                catch (Exception e) { Debug.LogWarning("[Server] Error enviando WEAPON_CHANGE: " + e.Message); }
            }
        }
    }

    void BroadcastWeaponStationState(int stationID, int stateCode)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.WeaponStationData);
            writer.Write(stationID);
            writer.Write(stateCode);

            byte[] data = ms.ToArray();

            List<EndPoint> snapshot;
            lock (clientsLock)
            {
                snapshot = new List<EndPoint>(connectedClients.Values);
            }

            Debug.Log($"[Server] Transmitiendo estado de WeaponStation {stationID} a {snapshot.Count} clientes. Estado: {stateCode}");

            foreach (var ep in snapshot)
            {
                try { serverSocket.SendTo(data, data.Length, SocketFlags.None, ep); }
                catch (Exception e) { Debug.LogWarning("[Server] Error enviando WEAPON_STATION_DATA: " + e.Message); }
            }
        }
    }

    void SendBinaryToClient(byte[] data, EndPoint remote)
    {
        try
        {
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
            connectedClients.Remove(key);
            playerPositions.Remove(key); playerHealth.Remove(key);
            playerRotations.Remove(key); playerWeapons.Remove(key);

            playerNames.Remove(key);
            playerReadyStatus.Remove(key);
        }

        BroadcastLobbyState();
        CheckRoundWinCondition();

        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Goodbye); writer.Write(key);
            byte[] data = ms.ToArray();
            List<EndPoint> snapshot; lock (clientsLock) snapshot = new List<EndPoint>(connectedClients.Values);
            foreach (var ep in snapshot) try { serverSocket.SendTo(data, data.Length, SocketFlags.None, ep); } catch { }
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