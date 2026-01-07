using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UDPClient : MonoBehaviour
{
    [Header("Connection Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;
    [Tooltip("Si pones 0, el sistema asignará un puerto automáticamente")]
    public int clientPort = 0;

    [Header("Gameplay")]
    public GameObject cubePrefab;
    public UiController uiController;

    private Socket clientSocket;
    private EndPoint serverEndPoint;
    private Thread receiveThread;
    private volatile bool isConnected = false;

    private string clientKey = null;
    private readonly object cubesLock = new object();
    private Dictionary<string, GameObject> playerCubes = new Dictionary<string, GameObject>();
    public List<LookAtPlayer> AllItems = new List<LookAtPlayer>();

    private List<PlayerNameTag> activeNameTags = new List<PlayerNameTag>();
    private Camera currentActiveCamera;

    private Dictionary<int, HealStation> healStations = new Dictionary<int, HealStation>();
    private readonly object healStationsLock = new object();
    private Dictionary<int, WeaponStation> weaponStations = new Dictionary<int, WeaponStation>();
    private readonly object weaponStationsLock = new object();

    private Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly object mainThreadLock = new object();

    public int MySpawnIndex => mySpawnIndex;
    private int mySpawnIndex = 0;
    public int CurrentRoundOffset { get; private set; } = 0;
    public struct LobbyPlayerInfo
    {
        public string Key;
        public string Name;
        public bool IsReady;
    }

    public List<LobbyPlayerInfo> CurrentLobbyPlayers { get; private set; } = new List<LobbyPlayerInfo>();

    public event Action<List<LobbyPlayerInfo>> OnLobbyUpdated;
    public event Action OnGameStarted;

    public bool IsConnected => isConnected;
    public string ClientKey => clientKey;

    // Message type codes (must match server)
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
        RoundReset = 32,
        PlayerJump = 33,
        WeaponThrow = 34,
        Ping = 99

    }

    public void StartConnection()
    {
        Thread t = new Thread(ConnectProcess) { IsBackground = true };
        t.Start();
        StartCoroutine(PingRoutine());
    }
    IEnumerator PingRoutine()
    {
        while (true)
        {
            if (isConnected && !string.IsNullOrEmpty(clientKey))
            {
                SendPing();
            }
            yield return new WaitForSeconds(1.0f); 
        }
    }
    void SendPing()
    {
        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Ping);
            writer.Write(clientKey);
            SendBinary(ms.ToArray());
        }
    }

    void ConnectProcess()
    {
        try
        {
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, clientPort);
            clientSocket.Bind(localEP);

            SendHandshake();

            isConnected = true;
            Debug.Log($"UDP client started and bound to {clientSocket.LocalEndPoint}");

            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError("UDPClient ConnectProcess error: " + ex.Message);
            isConnected = false;
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            while (isConnected)
            {
                try
                {
                    int bytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
                    if (bytes > 0)
                    {
                        byte[] data = new byte[bytes];
                        Array.Copy(buffer, data, bytes);
                        SafeEnqueueMain(() => ProcessServerMessage(data));
                    }
                }
                catch (SocketException se)
                {
                    if (!isConnected) break;
                    Debug.LogWarning("[Client] SocketException: " + se.Message);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Client] Error en ReceiveLoop: " + e.Message);
                    break;
                }
            }
        }
        finally
        {
            Debug.Log("[Client] ReceiveLoop terminado");
            DisconnectInternal();
        }
    }

    void ProcessServerMessage(byte[] data)
    {
        if (data == null || data.Length == 0) return;

        if (UiController.Instance != null)
            UiController.Instance.IncrementPacketsReceived();

        try
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                MessageType msgType = (MessageType)reader.ReadByte();

                switch (msgType)
                {
                    case MessageType.Welcome:
                        HandleWelcome(reader);
                        break;
                    case MessageType.LobbyData: 
                        HandleLobbyData(reader); 
                        break;
                    case MessageType.StartGame: 
                        HandleStartGame(reader); 
                        break;
                    case MessageType.RoundWin: 
                        HandleRoundWin(reader); 
                        break;
                    case MessageType.MatchWin: 
                        HandleMatchWin(reader); 
                        break;
                    case MessageType.RoundReset: 
                        HandleRoundReset(reader); 
                        break;
                    case MessageType.PlayerData:
                        HandlePlayerData(reader);
                        break;

                    case MessageType.Move:
                        HandleMove(reader);
                        break;

                    case MessageType.Rotate:
                        HandleRotate(reader);
                        break;

                    case MessageType.ShootAnim:
                        HandleShootAnim(reader);
                        break;

                    case MessageType.WeaponChange:
                        HandleWeaponChange(reader);
                        break;

                    case MessageType.KillConfirmed:
                        HandleKillConfirmed(reader);
                        break;

                    case MessageType.HealStationData:
                        HandleHealStationData(reader);
                        break;

                    case MessageType.WeaponStationData: 
                        HandleWeaponStationData(reader);
                        break;

                    case MessageType.Goodbye:
                        HandleGoodbye(reader);
                        break;

                    case MessageType.PlayerJump:
                        HandlePlayerJump(reader);
                        break;

                    case MessageType.WeaponThrow:
                        HandleWeaponThrow(reader);
                        break;

                    default:
                        Debug.LogWarning($"[Client] Tipo de mensaje desconocido: {msgType}");
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Client] Error procesando mensaje: {e.Message}");
        }
    }

    void HandleWelcome(BinaryReader reader)
    {
        clientKey = reader.ReadString();
        bool gameInProgress = reader.ReadBoolean();
        mySpawnIndex = reader.ReadInt32();
        bool shouldSpectate = reader.ReadBoolean(); 

        isConnected = true;
        Debug.Log($"Conectado. Key: {clientKey}. Index: {mySpawnIndex}. Spectate: {shouldSpectate}");

        SafeEnqueueMain(() =>
        {
            if (gameInProgress)
            {
                if (shouldSpectate)
                {
                    Debug.Log("[Client] Uniéndose tarde a partida llena -> ESPECTADOR.");
                    if (uiController != null)
                    {
                        uiController.EnableSpectatorMode();
                        uiController.ShowNotification("ESPERANDO SIGUIENTE RONDA...", Color.yellow);
                    }
                }
                else
                {
                    Debug.Log("[Client] Uniéndose a Host solitario -> A JUGAR.");
                    if (uiController != null) uiController.EnableGameHUD();
                    SpawnMyPlayerNow();
                }
            }
            else
            {
                if (uiController != null) uiController.EnterLobbyMode();
            }
        });
    }
    void HandlePlayerJump(BinaryReader reader)
    {
        string key = reader.ReadString();
        if (key == clientKey) return; // Ignorar mi propio salto (ya sonó localmente)

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(key, out GameObject cube))
                {
                    var audio = cube.GetComponent<PlayerAudioController>();
                    if (audio != null) audio.PlayJump();
                }
            }
        });
    }

    public void SendJump()
    {
        if (!isConnected || string.IsNullOrEmpty(clientKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.PlayerJump);
            writer.Write(clientKey);
            SendBinary(ms.ToArray());
        }
    }
    public void SpawnMyPlayerNow()
    {
        Vector3 spawnPos = Vector3.zero;
        if (GameplayManager.Instance != null)
        {
            int finalIndex = mySpawnIndex + CurrentRoundOffset;
            spawnPos = GameplayManager.Instance.GetSpawnPosition(finalIndex);
        }
        GameObject myCube = Instantiate(cubePrefab, spawnPos, Quaternion.identity);

        PlayerNameTag nameTag = myCube.GetComponentInChildren<PlayerNameTag>();
        if (nameTag != null)
        {
            string myName = "";
            foreach (var p in CurrentLobbyPlayers) if (p.Key == clientKey) myName = p.Name;
            nameTag.SetText(myName, Color.green);
        }
        lock (cubesLock)
        {
            playerCubes[clientKey] = myCube;
        }

        PlayerController player = myCube.GetComponent<PlayerController>();
        if (player != null)
        {
            player.SetAsLocalPlayer(true);
            player.enabled = true;
            foreach (var item in AllItems) item.AssignCamera(player.GetPlayerCamera().playerCamera);
        }
        SendCubeMovement(spawnPos, 0);
    }

    void HandleRoundReset(BinaryReader reader)
    {
        int offset = reader.ReadInt32(); 

        SafeEnqueueMain(() => {
            CurrentRoundOffset = offset; 

            if (RoundScoreUI.Instance) RoundScoreUI.Instance.HideRoundMessage();
            if (uiController) uiController.HideDeathScreen();

            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(clientKey, out GameObject myCube))
                {
                    PlayerController pc = myCube.GetComponent<PlayerController>();
                    if (pc)
                    {
                        pc.Respawn(); 
                        pc.SetAsLocalPlayer(true);
                        if (uiController) uiController.EnableGameHUD();
                    }
                }
                else
                {
                    SpawnMyPlayerNow(); 
                    if (uiController) uiController.EnableGameHUD();
                }
            }
        });
    }
    void HandleLobbyData(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        List<LobbyPlayerInfo> players = new List<LobbyPlayerInfo>();

        for (int i = 0; i < count; i++)
        {
            players.Add(new LobbyPlayerInfo
            {
                Key = reader.ReadString(),
                Name = reader.ReadString(),
                IsReady = reader.ReadBoolean()
            });
        }

        CurrentLobbyPlayers = players;
        OnLobbyUpdated?.Invoke(players);
    }
    void HandleStartGame(BinaryReader reader)
    {
        int maxRounds = reader.ReadInt32();
        int offset = reader.ReadInt32(); 

        SafeEnqueueMain(() => {
            CurrentRoundOffset = offset;

            if (RoundScoreUI.Instance != null)
                RoundScoreUI.Instance.Configure(maxRounds);

            OnGameStarted?.Invoke();
        });
    }

    void HandleRoundWin(BinaryReader reader)
    {
        string winnerKey = reader.ReadString();
        string winnerName = reader.ReadString();

        SafeEnqueueMain(() => {
            // Si soy el ganador, subo mi slider
            if (winnerKey == clientKey && RoundScoreUI.Instance != null)
            {
                RoundScoreUI.Instance.AddWin();
            }

            // Mostrar mensaje a todos
            if (RoundScoreUI.Instance != null)
                RoundScoreUI.Instance.ShowRoundWinner(winnerName, false);
        });
    }

    void HandleMatchWin(BinaryReader reader)
    {
        string winnerName = reader.ReadString();

        SafeEnqueueMain(() => {
            if (RoundScoreUI.Instance != null)
                RoundScoreUI.Instance.ShowRoundWinner(winnerName, true); // true = Mensaje de Partida

            StartCoroutine(ReturnToMainMenuRoutine());
        });
    }
    IEnumerator ReturnToMainMenuRoutine()
    {
        yield return new WaitForSeconds(5.0f);
        Debug.Log("[Client] Partida terminada. Volviendo al menú principal...");
        Disconnect();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MainMenu");
    }
    public void SendReadyState(bool ready)
    {
        if (!isConnected) return;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.ClientReady);
            writer.Write(clientKey);
            writer.Write(ready);
            SendBinary(ms.ToArray());
        }
    }

    string GetNameByKey(string key)
    {
        if (CurrentLobbyPlayers != null)
        {
            foreach (var p in CurrentLobbyPlayers)
            {
                if (p.Key == key) return p.Name;
            }
        }
        return "Unknown";
    }

    void HandleShootAnim(BinaryReader reader)
    {
        string shooterKey = reader.ReadString();
        if (shooterKey == clientKey) return;

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(shooterKey, out GameObject cube) && cube != null)
                {
                    var controller = cube.GetComponent<PlayerController>();
                    if (controller != null)
                    {
                        controller.PlayShootVisuals();
                    }
                }
            }
        });
    }
    void HandleWeaponChange(BinaryReader reader)
    {
        string senderKey = reader.ReadString();
        int weaponID = reader.ReadInt32();

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(senderKey, out GameObject cube) && cube != null)
                {
                    var wm = cube.GetComponent<WeaponManager>();
                    if (senderKey != clientKey)
                    {
                        if (wm != null) wm.SetWeaponByID(weaponID);
                    }

                    if (senderKey == clientKey && wm != null && wm.CurrentWeaponID != weaponID)
                    {
                        wm.SetWeaponByID(weaponID);
                    }
                }
            }
        });
    }
    public void SendWeaponChange(int weaponID)
    {
        if (!isConnected || string.IsNullOrEmpty(clientKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.WeaponChange);
            writer.Write(clientKey);
            writer.Write(weaponID); 

            SendBinary(ms.ToArray());
        }
    }
    void HandlePlayerData(BinaryReader reader)
    {
        string key = reader.ReadString();
        float health = reader.ReadSingle();

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(key, out GameObject cube))
                {
                    var controller = cube.GetComponent<PlayerController>();
                    if (controller != null)
                    {
                        controller.UpdateHealth(health);
                        Debug.Log($"[Client] Actualizada salud de {key} a {health}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Client] PLAYERDATA recibido para key desconocida: {key}");
                }
            }
        });
    }

    void HandleMove(BinaryReader reader)
    {
        string key = reader.ReadString();
        int seq = reader.ReadInt32(); 
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        Vector3 newPos = new Vector3(x, y, z);

        if (key == clientKey) return;

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (!playerCubes.ContainsKey(key))
                {
                    InstantiateRemoteCube(key, newPos);
                    if (uiController != null) uiController.ShowPlayerJoined();
                }
                else
                {
                    GameObject cube = playerCubes[key];
                    if (cube != null)
                    {
                        var sync = cube.GetComponent<PlayerSync>();
                        if (sync) sync.OnReceivePosition(newPos, seq);
                        else cube.transform.position = newPos; 
                    }
                }
            }
        });
    }

    void HandleRotate(BinaryReader reader)
    {
        string key = reader.ReadString();
        int seq = reader.ReadInt32(); 
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        Vector3 newRot = new Vector3(x, y, z);

        if (key == clientKey) return;

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(key, out GameObject cube))
                {
                    var sync = cube.GetComponent<PlayerSync>();
                    if (sync) sync.OnReceiveRotation(newRot, seq);
                }
            }
        });
    }

    public void SendWeaponThrow(int weaponID, Vector3 position, Vector3 direction)
    {
        if (!isConnected || string.IsNullOrEmpty(clientKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.WeaponThrow);
            writer.Write(clientKey);
            writer.Write(weaponID);
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
            writer.Write(direction.x);
            writer.Write(direction.y);
            writer.Write(direction.z);

            SendBinary(ms.ToArray());
        }

        Debug.Log($"[Client] Sent weapon throw: ID {weaponID}");
    }

    void HandleWeaponThrow(BinaryReader reader)
    {
        string senderKey = reader.ReadString();
        int weaponID = reader.ReadInt32();
        float px = reader.ReadSingle();
        float py = reader.ReadSingle();
        float pz = reader.ReadSingle();
        float dx = reader.ReadSingle();
        float dy = reader.ReadSingle();
        float dz = reader.ReadSingle();

        Vector3 position = new Vector3(px, py, pz);
        Vector3 direction = new Vector3(dx, dy, dz);

        if (senderKey == clientKey) return; // Ignorar mi propio throw

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(senderKey, out GameObject cube) && cube != null)
                {
                    WeaponThrowSystem throwSystem = cube.GetComponent<WeaponThrowSystem>();
                    if (throwSystem != null)
                    {
                        throwSystem.HandleRemoteWeaponThrow(weaponID, position, direction);
                        Debug.Log($"[Client] Remote player {senderKey} threw weapon {weaponID}");
                    }
                    else
                    {
                        Debug.LogWarning($"[Client] WeaponThrowSystem not found on player {senderKey}");
                    }

                    // También desequipar el arma del jugador remoto visualmente
                    WeaponManager wm = cube.GetComponent<WeaponManager>();
                    if (wm != null)
                    {
                        wm.UnequipCurrentWeapon();
                    }
                }
            }
        });
    }
    void HandleHealStationData(BinaryReader reader)
    {
        int stationID = reader.ReadInt32();
        int stateCode = reader.ReadInt32();

        bool isCooldown = (stateCode == 1);

        SafeEnqueueMain(() =>
        {
            lock (healStationsLock)
            {
                if (healStations.TryGetValue(stationID, out HealStation station))
                {
                    station.SetNetworkState(isCooldown);
                }
            }
        });
    }

    void HandleWeaponStationData(BinaryReader reader)
    {
        int stationID = reader.ReadInt32();
        int stateCode = reader.ReadInt32();

        bool isCooldown = (stateCode == 1);

        SafeEnqueueMain(() =>
        {
            lock (weaponStationsLock)
            {
                if (weaponStations.TryGetValue(stationID, out WeaponStation station))
                {
                    station.SetNetworkState(isCooldown);
                }
            }
        });
    }


    void HandleKillConfirmed(BinaryReader reader)
    {
        string shooterKey = reader.ReadString();
        if (shooterKey == clientKey)
        {
            SafeEnqueueMain(() =>
            {
                if (KillCountUI.instance != null)
                {
                    KillCountUI.instance.AddKill();
                }
                else
                {
                    Debug.LogWarning("[Client] KillCountUI.instance es null!");
                }
            });

            Debug.Log($"[Client] Kill confirmado para jugador local!");
        }
    }

    void HandleGoodbye(BinaryReader reader)
    {
        string key = reader.ReadString();
        SafeEnqueueMain(() => RemoveCube(key));
        if (uiController != null)
            uiController.ShowPlayerLeft();
    }

    void InstantiateRemoteCube(string key, Vector3 position)
    {
        GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);

        PlayerNameTag nameTag = newCube.GetComponentInChildren<PlayerNameTag>();
        if (nameTag != null)
        {
            string remoteName = "Enemy";
            foreach (var p in CurrentLobbyPlayers) if (p.Key == key) remoteName = p.Name;
            nameTag.SetText(remoteName, Color.white);
        }

        PlayerController movementScript = newCube.GetComponent<PlayerController>();
        if (movementScript != null)
        {
            movementScript.SetAsLocalPlayer(false);
            movementScript.enabled = true;
            movementScript.UpdatePosition(position);
        }

        lock (cubesLock)
        {
            playerCubes[key] = newCube;
        }

        Debug.Log($"[Client] Cubo remoto creado para {key} ({GetNameByKey(key)})");
    }

    void RemoveCube(string key)
    {
        lock (cubesLock)
        {
            if (playerCubes.TryGetValue(key, out GameObject go))
            {
                PlayerNameTag tag = go.GetComponentInChildren<PlayerNameTag>();
                if (tag != null) activeNameTags.Remove(tag);

                Destroy(go);
                playerCubes.Remove(key);
                Debug.Log($"[Client] Cubo removido: {key}");
            }
        }
    }

    public void RegisterHealStation(int id, HealStation station)
    {
        if (id == -1)
        {
            Debug.LogError("Una HealStation tiene un ID de -1. ¡Asigna un ID único en el Inspector!", station);
            return;
        }

        lock (healStationsLock)
        {
            if (healStations.ContainsKey(id))
            {
                Debug.LogWarning($"Ya existe una HealStation registrada con ID {id}. Sobrescribiendo.");
            }
            healStations[id] = station;
            Debug.Log($"[Client] HealStation {id} registrada.");
        }
    }

    public void RegisterWeaponStation(int id, WeaponStation station) 
    {
        if (id == -1)
        {
            Debug.LogError("Una WeaponStation tiene un ID de -1. ¡Asigna un ID único en el Inspector!", station);
            return;
        }

        lock (weaponStationsLock)
        {
            if (weaponStations.ContainsKey(id))
            {
                Debug.LogWarning($"Ya existe una WeaponStation registrada con ID {id}. Sobrescribiendo.");
            }
            weaponStations[id] = station;
            Debug.Log($"[Client] WeaponStation {id} registrada.");
        }
    }

    public void SendHealRequest(int stationID)
    {
        if (!isConnected || string.IsNullOrEmpty(clientKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.HealRequest);
            writer.Write(clientKey);
            writer.Write(stationID);

            byte[] data = ms.ToArray();
            SendBinary(data);
        }
    }

    public void SendWeaponRequest(int stationID, int weaponID)
    {
        if (!isConnected || string.IsNullOrEmpty(clientKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.WeaponStationRequest);
            writer.Write(clientKey);
            writer.Write(stationID);
            writer.Write(weaponID);

            SendBinary(ms.ToArray());
        }
    }
    public void SendCubeMovement(Vector3 position, int seq)
    {
        if (!isConnected || clientSocket == null) return;
        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Move);
            writer.Write(clientKey);
            writer.Write(seq); // ENVIAR SECUENCIA
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
            SendBinary(ms.ToArray());
        }
    }

    public void SendCubeRotation(Vector3 rotation, int seq)
    {
        if (!isConnected || clientSocket == null) return;
        using (MemoryStream ms = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Rotate);
            writer.Write(clientKey);
            writer.Write(seq); // ENVIAR SECUENCIA
            writer.Write(rotation.x);
            writer.Write(rotation.y);
            writer.Write(rotation.z);
            SendBinary(ms.ToArray());
        }
    }

    public void SendPlayerHealth(float health)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.PlayerData);
            writer.Write(clientKey);
            writer.Write(health);

            byte[] data = ms.ToArray();
            SendBinary(data);
        }
    }

    public void SendShotToServer(string targetKey, float damage)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey)) return;
        if (string.IsNullOrEmpty(targetKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Shot);
            writer.Write(clientKey);
            writer.Write(targetKey);
            writer.Write(damage);

            byte[] data = ms.ToArray();
            SendBinary(data);
        }
    }

    public void SendShootAnim()
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey)) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.ShootAnim);
            writer.Write(clientKey);

            byte[] data = ms.ToArray();
            SendBinary(data);
        }
    }

    public string GetKeyForGameObject(GameObject go)
    {
        if (go == null) return null;
        lock (cubesLock)
        {
            foreach (var kv in playerCubes)
            {
                if (kv.Value == go)
                    return kv.Key;
            }
        }
        return null;
    }

    private void SendHandshake()
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Handshake);
            byte[] data = ms.ToArray();
            SendBinary(data);
        }
    }

    private void SendBinary(byte[] data)
    {
        try
        {
            if (serverEndPoint == null)
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            if (UiController.Instance != null)
                UiController.Instance.IncrementPacketsSent();

            if (NetworkSimulator.Instance != null && NetworkSimulator.Instance.ShouldDropPacket())
            {
                Debug.Log("[Client] Packet DROPPED (simulation)");
                return;
            }

            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEndPoint);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Client] Error enviando mensaje: " + ex.Message);
        }
    }

    void SafeEnqueueMain(Action a)
    {
        lock (mainThreadLock)
        {
            mainThreadActions.Enqueue(a);
        }
    }

    void Update()
    {
        while (true)
        {
            Action a = null;
            lock (mainThreadLock)
            {
                if (mainThreadActions.Count > 0)
                    a = mainThreadActions.Dequeue();
            }
            if (a == null) break;

            try
            {
                a.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError("[Client] Error ejecutando acción: " + e.Message);
            }
        }
    }

    void DisconnectInternal()
    {
        isConnected = false;
        try
        {
            if (clientSocket != null)
            {
                clientSocket.Close();
                clientSocket = null;
            }
        }
        catch { }
    }

    public void Disconnect()
    {
        if (!isConnected) return;
        isConnected = false;

        try
        {
            if (!string.IsNullOrEmpty(clientKey))
            {
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)MessageType.Goodbye);
                    writer.Write(clientKey);

                    byte[] data = ms.ToArray();
                    SendBinary(data);
                }
            }
        }
        catch { }

        try { clientSocket?.Close(); } catch { }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }

        clientSocket = null;
        Debug.Log("[Client] Desconectado");
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }
}