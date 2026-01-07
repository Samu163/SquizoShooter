using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class UDPServer : MonoBehaviour
{
    [Header("Server Configuration")]
    public int port = 9050;
    public float connectionTimeout = 5.0f;
    public float pingInterval = 1.0f; 

    private Socket serverSocket;
    private Thread receiveThread;
    private volatile bool isRunning = false;

    private readonly Dictionary<string, ClientProxy> connectedProxies = new Dictionary<string, ClientProxy>();
    private readonly object clientsLock = new object();

    private readonly Dictionary<int, bool> healStationStates = new Dictionary<int, bool>();
    private const float HEAL_STATION_COOLDOWN_TIME = 5.0f;

    private int maxRoundsToWin = 5;
    private Dictionary<string, int> playerScores = new Dictionary<string, int>();
    private bool isRoundEnding = false;
    private bool isGameStarted = false;
    private int totalPlayersConnected = 0;


    private enum MessageType : byte
    {
        Handshake = 1, Welcome = 2, PlayerData = 3, ShootAnim = 4, Shot = 5, Move = 6, Rotate = 7,
        HealRequest = 8, HealStationData = 9, KillConfirmed = 10, Goodbye = 11, WeaponChange = 12,
        WeaponStationData = 13, WeaponStationRequest = 15, LobbyData = 20, ClientReady = 21, StartGame = 22,
        RoundWin = 30, MatchWin = 31, RoundReset = 32, PlayerJump = 33, WeaponThrow = 34, Ping = 99
    }

    public void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();
            Debug.Log($"[Server] UDP server started on port {port}. Timeout: {connectionTimeout}s");
        }
        catch (Exception ex) { Debug.LogError("[Server] Error starting: " + ex.Message); }
    }

    void Update()
    {
        if (!isRunning) return;

        lock (clientsLock)
        {
            List<string> toDisconnect = new List<string>();
            long now = DateTime.Now.Ticks;
            long timeoutTicks = (long)(connectionTimeout * 10000000);

            foreach (var kvp in connectedProxies)
            {
                if (now - kvp.Value.LastPacketTime > timeoutTicks)
                    toDisconnect.Add(kvp.Key);
            }

            foreach (string key in toDisconnect)
            {
                Debug.LogWarning($"[Server] Client {key} timed out.");
                RemoveClientInternal(key);
            }
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (isRunning)
        {
            try
            {
                int bytes = serverSocket.ReceiveFrom(buffer, ref remoteEP);
                if (bytes > 0)
                {
                    byte[] data = new byte[bytes]; Array.Copy(buffer, data, bytes);
                    ProcessClientMessage(data, remoteEP);
                }
            }
            catch { if (!isRunning) break; }
        }
    }

    void ProcessClientMessage(byte[] data, EndPoint remote)
    {
        if (data == null || data.Length == 0) return;
        try
        {
            using (MemoryStream ms = new MemoryStream(data)) using (BinaryReader reader = new BinaryReader(ms))
            {
                MessageType msgType = (MessageType)reader.ReadByte();
                switch (msgType)
                {
                    case MessageType.Handshake: HandleHandshake(remote); break;
                    case MessageType.Ping: HandlePing(reader); break;
                    case MessageType.Move: HandleMove(reader); break;
                    case MessageType.Rotate: HandleRotate(reader); break;
                    case MessageType.PlayerData: HandlePlayerData(reader); break;
                    case MessageType.ShootAnim: HandleShootAnim(reader); break;
                    case MessageType.Shot: HandleShot(reader); break;
                    case MessageType.ClientReady: HandleClientReady(reader); break;
                    case MessageType.HealRequest: HandleHealRequest(reader); break;
                    case MessageType.WeaponChange: HandleWeaponSwitchNotification(reader); break;
                    case MessageType.WeaponStationRequest: HandleWeaponStationRequest(reader); break;
                    case MessageType.PlayerJump: HandlePlayerJump(reader); break;
                    case MessageType.WeaponThrow: HandleWeaponThrow(reader); break;
                    case MessageType.Goodbye: HandleGoodbye(reader); break;
                }
            }
        }
        catch (Exception e) { Debug.LogError($"[Server] Msg Error: {e.Message}"); }
    }

    void UpdateClientTimestamp(string key)
    {
        lock (clientsLock)
        {
            if (connectedProxies.TryGetValue(key, out ClientProxy client))
                client.LastPacketTime = DateTime.Now.Ticks;
        }
    }

    // --- HANDLERS ---

    void HandlePing(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
    }

    void HandleHandshake(EndPoint remote)
    {
        string newKey = Guid.NewGuid().ToString();
        string randomName = NameGenerator.GetRandomName();
        int assignedIndex = 0;
        bool shouldSpectate = false;

        lock (clientsLock)
        {
            assignedIndex = totalPlayersConnected;
            totalPlayersConnected++;

            ClientProxy newClient = new ClientProxy(newKey, randomName, remote, assignedIndex);

            if (isGameStarted)
            {
                if (connectedProxies.Count >= 2)
                {
                    shouldSpectate = true;
                    newClient.InitializeGameData(true);
                    Debug.Log($"[Server] {randomName} -> ESPECTADOR.");
                }
                else
                {
                    shouldSpectate = false;
                    newClient.InitializeGameData(false);
                    Debug.Log($"[Server] {randomName} -> JUGAR.");
                }
            }
            else
            {
                newClient.InitializeGameData(false);
            }

            connectedProxies.Add(newKey, newClient);
        }

        SendWelcome(newKey, remote, isGameStarted, assignedIndex, shouldSpectate);
        BroadcastLobbyState();

        if (isGameStarted)
        {
            SendAllPlayerPositionsToSingleClient(newKey, remote);
            SendAllHealStationStatesToSingleClient(remote);
            if (!shouldSpectate) CheckRoundWinCondition();
        }
    }

    void HandleMove(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        float x = reader.ReadSingle(); float y = reader.ReadSingle(); float z = reader.ReadSingle();
        Vector3 newPos = new Vector3(x, y, z);

        lock (clientsLock)
        {
            if (connectedProxies.TryGetValue(key, out ClientProxy client))
                client.Position = newPos;
        }
        BroadcastMove(key, newPos);
    }

    void HandleRotate(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        float x = reader.ReadSingle(); float y = reader.ReadSingle(); float z = reader.ReadSingle();
        Vector3 newRot = new Vector3(x, y, z);

        lock (clientsLock)
        {
            if (connectedProxies.TryGetValue(key, out ClientProxy client))
                client.Rotation = newRot;
        }
        BroadcastRotate(key, newRot);
    }

    void HandlePlayerData(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        float health = reader.ReadSingle();

        lock (clientsLock)
        {
            if (connectedProxies.TryGetValue(key, out ClientProxy client))
                client.Health = health;
        }
        BroadcastPlayerHealth(key, health);
    }

    void HandleShot(BinaryReader reader)
    {
        string shooter = reader.ReadString();
        string target = reader.ReadString();
        UpdateClientTimestamp(shooter);
        float dmg = reader.ReadSingle();

        if (shooter == target) return;

        bool wasKill = false;
        float newHealth = 0;

        lock (clientsLock)
        {
            if (connectedProxies.TryGetValue(target, out ClientProxy victim))
            {
                float current = victim.Health;
                newHealth = Mathf.Clamp(current - dmg, 0, 100);
                victim.Health = newHealth;

                if (newHealth <= 0 && current > 0)
                {
                    wasKill = true;
                    Debug.Log($"[Server] KILL: {shooter} -> {target}");
                }
            }
        }

        BroadcastPlayerHealth(target, newHealth);

        if (wasKill)
        {
            SendKillConfirmation(shooter);
            CheckRoundWinCondition();
        }
    }

    void HandleWeaponSwitchNotification(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        int id = reader.ReadInt32();
        lock (clientsLock) { if (connectedProxies.TryGetValue(key, out ClientProxy c)) c.WeaponID = id; }
        BroadcastWeaponChange(key, id);
    }

    void HandleClientReady(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        bool r = reader.ReadBoolean();
        lock (clientsLock) { if (connectedProxies.TryGetValue(key, out ClientProxy c)) c.IsReady = r; }
        BroadcastLobbyState();
    }

    void HandleGoodbye(BinaryReader reader)
    {
        string key = reader.ReadString();
        RemoveClientByKey(key);
    }

    void HandleShootAnim(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.ShootAnim); w.Write(key); BroadcastData(m.ToArray());
        }
    }

    void HandlePlayerJump(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.PlayerJump); w.Write(key); BroadcastData(m.ToArray());
        }
    }

    void HandleWeaponThrow(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        int wID = reader.ReadInt32();
        float px = reader.ReadSingle(); float py = reader.ReadSingle(); float pz = reader.ReadSingle();
        float dx = reader.ReadSingle(); float dy = reader.ReadSingle(); float dz = reader.ReadSingle();

        lock (clientsLock) { if (connectedProxies.TryGetValue(key, out ClientProxy c)) c.WeaponID = 0; }

        using (MemoryStream m = new MemoryStream()) using (BinaryWriter wr = new BinaryWriter(m))
        {
            wr.Write((byte)MessageType.WeaponThrow);
            wr.Write(key); wr.Write(wID);
            wr.Write(px); wr.Write(py); wr.Write(pz);
            wr.Write(dx); wr.Write(dy); wr.Write(dz);
            BroadcastData(m.ToArray());
        }
    }

    void HandleHealRequest(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        int id = reader.ReadInt32();
        bool approved = false;

        lock (clientsLock)
        {
            if (!healStationStates.TryGetValue(id, out bool cd) || !cd)
            {
                approved = true;
                healStationStates[id] = true;
                if (connectedProxies.TryGetValue(key, out ClientProxy c)) c.Health = 100f;
            }
        }

        if (approved)
        {
            BroadcastPlayerHealth(key, 100f);
            BroadcastHealStationState(id, 1);
            new System.Threading.Timer(_ => EndHealStationCooldown(id), null, (int)(HEAL_STATION_COOLDOWN_TIME * 1000), Timeout.Infinite);
        }
    }

    void HandleWeaponStationRequest(BinaryReader reader)
    {
        string key = reader.ReadString();
        UpdateClientTimestamp(key);
        int sID = reader.ReadInt32();
        int wID = reader.ReadInt32();
        bool approved = false;

        lock (clientsLock)
        {
            if (!healStationStates.TryGetValue(sID, out bool cd) || !cd)
            {
                approved = true;
                healStationStates[sID] = true;
                if (connectedProxies.TryGetValue(key, out ClientProxy c)) c.WeaponID = wID;
            }
        }

        if (approved)
        {
            BroadcastWeaponChange(key, wID);
            BroadcastWeaponStationState(sID, 1);
            new System.Threading.Timer(_ => EndWeaponStationCooldown(sID), null, (int)(HEAL_STATION_COOLDOWN_TIME * 1000), Timeout.Infinite);
        }
    }

    // --- GAME LOGIC ---

    void CheckRoundWinCondition()
    {
        if (!isGameStarted || isRoundEnding || connectedProxies.Count < 2) return;

        int aliveCount = 0;
        string lastAliveKey = "";

        lock (clientsLock)
        {
            foreach (var proxy in connectedProxies.Values)
            {
                if (proxy.Health > 0)
                {
                    aliveCount++;
                    lastAliveKey = proxy.ClientKey;
                }
            }
        }

        if (aliveCount <= 1) StartRoundEndSequence(lastAliveKey);
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
                if (connectedProxies.TryGetValue(winnerKey, out ClientProxy p)) winnerName = p.Name;
            }
        }

        if (matchWon)
        {
            BroadcastMatchWin(winnerName);
            new System.Threading.Timer(_ => ResetToLobby(), null, 5000, Timeout.Infinite);
        }
        else
        {
            BroadcastRoundWin(winnerKey, winnerName);
            new System.Threading.Timer(_ => ResetRound(), null, 3000, Timeout.Infinite);
        }
    }

    void ResetRound()
    {
        lock (clientsLock)
        {
            foreach (var proxy in connectedProxies.Values)
            {
                proxy.Health = 100f;
                proxy.IsSpectating = false;
            }
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
            foreach (var p in connectedProxies.Values) { p.Health = 100f; p.IsReady = false; p.IsSpectating = false; }
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
            foreach (var key in connectedProxies.Keys) playerScores[key] = 0;
        }
        BroadcastGameStart(rounds);
    }

    // --- BROADCASTS & HELPERS ---

    void BroadcastData(byte[] data)
    {
        List<EndPoint> eps;
        lock (clientsLock) eps = connectedProxies.Values.Select(p => p.RemoteEndPoint).ToList();
        foreach (var e in eps) SendBinaryToClient(data, e);
    }

    void SendAllPlayerPositionsToSingleClient(string newKey, EndPoint remote)
    {
        lock (clientsLock)
        {
            foreach (var proxy in connectedProxies.Values)
            {
                if (proxy.ClientKey == newKey) continue; // No me envío a mí mismo

                using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
                {
                    w.Write((byte)MessageType.Move); w.Write(proxy.ClientKey);
                    w.Write(proxy.Position.x); w.Write(proxy.Position.y); w.Write(proxy.Position.z);
                    SendBinaryToClient(m.ToArray(), remote);
                }
                using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
                {
                    w.Write((byte)MessageType.PlayerData); w.Write(proxy.ClientKey); w.Write(proxy.Health);
                    SendBinaryToClient(m.ToArray(), remote);
                }
                using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
                {
                    w.Write((byte)MessageType.WeaponChange); w.Write(proxy.ClientKey); w.Write(proxy.WeaponID);
                    SendBinaryToClient(m.ToArray(), remote);
                }
                Thread.Sleep(2);
            }
        }
    }

    void SendAllHealStationStatesToSingleClient(EndPoint remote)
    {
        lock (clientsLock)
        {
            if (healStationStates.Count == 0) return;
            foreach (var kvp in healStationStates)
            {
                int stateCode = kvp.Value ? 1 : 0;
                MessageType msg = kvp.Key > 100 ? MessageType.WeaponStationData : MessageType.HealStationData;
                using (MemoryStream ms = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(ms))
                {
                    w.Write((byte)msg); w.Write(kvp.Key); w.Write(stateCode);
                    SendBinaryToClient(ms.ToArray(), remote);
                }
                Thread.Sleep(5);
            }
        }
    }

    void RemoveClientByKey(string key)
    {
        lock (clientsLock) RemoveClientInternal(key);
    }

    void RemoveClientInternal(string key)
    {
        if (connectedProxies.ContainsKey(key))
        {
            connectedProxies.Remove(key);
            BroadcastLobbyState();
            CheckRoundWinCondition();

            using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write((byte)MessageType.Goodbye); w.Write(key);
                BroadcastData(m.ToArray());
            }
            Debug.Log($"[Server] Cliente {key} eliminado.");
        }
    }

    void SendWelcome(string k, EndPoint r, bool g, int i, bool s)
    {
        using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.Welcome); w.Write(k); w.Write(g); w.Write(i); w.Write(s);
            SendBinaryToClient(m.ToArray(), r);
        }
    }

    void BroadcastLobbyState()
    {
        using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.LobbyData);
            lock (clientsLock)
            {
                w.Write(connectedProxies.Count);
                foreach (var p in connectedProxies.Values)
                {
                    w.Write(p.ClientKey); w.Write(p.Name); w.Write(p.IsReady);
                }
            }
            BroadcastData(m.ToArray());
        }
    }

    void SendKillConfirmation(string k)
    {
        if (connectedProxies.ContainsKey(k))
        {
            using (MemoryStream m = new MemoryStream()) using (BinaryWriter w = new BinaryWriter(m))
            {
                w.Write((byte)MessageType.KillConfirmed); w.Write(k);
                SendBinaryToClient(m.ToArray(), connectedProxies[k].RemoteEndPoint);
            }
        }
    }
    void BroadcastGameStart(int r)
    {
        int off = UnityEngine.Random.Range(0, 100);

        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.StartGame);
            w.Write(r);
            w.Write(off);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastRoundReset()
    {
        int off = UnityEngine.Random.Range(0, 100);

        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.RoundReset);
            w.Write(off);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastRoundWin(string k, string n)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.RoundWin);
            w.Write(k ?? "");
            w.Write(n);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastMatchWin(string n)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.MatchWin);
            w.Write(n);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastMove(string k, Vector3 p)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.Move);
            w.Write(k);
            w.Write(p.x);
            w.Write(p.y);
            w.Write(p.z);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastRotate(string k, Vector3 r)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.Rotate);
            w.Write(k);
            w.Write(r.x);
            w.Write(r.y);
            w.Write(r.z);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastPlayerHealth(string k, float h)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.PlayerData);
            w.Write(k);
            w.Write(h);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastWeaponChange(string k, int i)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.WeaponChange);
            w.Write(k);
            w.Write(i);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastHealStationState(int id, int c)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.HealStationData);
            w.Write(id);
            w.Write(c);

            BroadcastData(m.ToArray());
        }
    }
    void BroadcastWeaponStationState(int id, int c)
    {
        using (MemoryStream m = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(m))
        {
            w.Write((byte)MessageType.WeaponStationData);
            w.Write(id);
            w.Write(c);

            BroadcastData(m.ToArray());
        }
    }
    void EndHealStationCooldown(int id)
    {
        lock (clientsLock)
            healStationStates[id] = false;

        BroadcastHealStationState(id, 0);
    }

    void EndWeaponStationCooldown(int id)
    {
        lock (clientsLock)
            healStationStates[id] = false;

        BroadcastWeaponStationState(id, 0);
    }
    void SendBinaryToClient(byte[] d, EndPoint r)
    {
        try
        {
            serverSocket.SendTo(d, d.Length, SocketFlags.None, r);
        }
        catch { }
    }
    public void StopServer()
    {
        isRunning = false;

        if (serverSocket != null)
            serverSocket.Close();
    }

    void OnDestroy() => StopServer();

    public int GetConnectedClientCount()
    {
        lock (clientsLock)
        {
            return connectedProxies.Count;
        }
    }

    public System.Collections.Generic.List<ClientProxy> GetConnectedClients()
    {
        lock (clientsLock)
        {
            return new System.Collections.Generic.List<ClientProxy>(connectedProxies.Values);
        }
    }

}