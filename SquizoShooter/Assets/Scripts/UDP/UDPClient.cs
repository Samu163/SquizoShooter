using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

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
    private Dictionary<int, HealStation> healStations = new Dictionary<int, HealStation>();
    private readonly object healStationsLock = new object();

    private Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly object mainThreadLock = new object();

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
        Goodbye = 11
    }

    public void StartConnection()
    {
        Thread t = new Thread(ConnectProcess) { IsBackground = true };
        t.Start();
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

                    case MessageType.ShootAnim:
                        HandleShootAnim(reader);
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

                    case MessageType.HealStationData:
                        HandleHealStationData(reader);
                        break;

                    case MessageType.KillConfirmed:
                        HandleKillConfirmed(reader);
                        break;

                    case MessageType.Goodbye:
                        HandleGoodbye(reader);
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
        Debug.Log("Received client key: " + clientKey);

        Vector3 spawnPos = (GameplayManager.Instance != null)
                ? GameplayManager.Instance.GetRandomSpawnPosition()
                : Vector3.zero;

        GameObject myCube = Instantiate(cubePrefab, spawnPos, Quaternion.identity);

        lock (cubesLock)
        {
            playerCubes[clientKey] = myCube;
        }

        PlayerController player = myCube.GetComponent<PlayerController>();
        if (player != null)
        {
            player.SetAsLocalPlayer(true);
            player.enabled = true;
            foreach (var item in AllItems)
            {
                item.AssignCamera(player.GetPlayerCamera().playerCamera);
            }
        }

        if (uiController != null)
            uiController.ShowNotification("You joined the game!", Color.green);

        SendCubeMovement(spawnPos);
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
                        controller.PlayShootAnimation();
                    }
                }
            }
        });
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
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();

        Vector3 newPos = new Vector3(x, y, z);

        if (key == clientKey)
            return;

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (!playerCubes.ContainsKey(key))
                {
                    InstantiateRemoteCube(key, newPos);

                    if (uiController != null)
                        uiController.ShowPlayerJoined();
                }
                else
                {
                    GameObject cube = playerCubes[key];
                    if (cube != null)
                    {
                        PlayerController move = cube.GetComponent<PlayerController>();
                        if (move != null)
                        {
                            move.UpdatePosition(newPos);
                        }
                        else
                        {
                            cube.transform.position = newPos;
                        }
                    }
                }
            }
        });
    }

    void HandleRotate(BinaryReader reader)
    {
        string key = reader.ReadString();
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();

        Vector3 newRot = new Vector3(x, y, z);

        if (key == clientKey)
            return;

        SafeEnqueueMain(() =>
        {
            lock (cubesLock)
            {
                if (playerCubes.TryGetValue(key, out GameObject cube))
                {
                    if (cube != null)
                    {
                        cube.transform.rotation = Quaternion.Euler(newRot);
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

        Debug.Log($"[Client] Cubo remoto creado para {key} en {position}");
    }

    void RemoveCube(string key)
    {
        lock (cubesLock)
        {
            if (playerCubes.TryGetValue(key, out GameObject go))
            {
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

    public void SendCubeMovement(Vector3 position)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey))
        {
            Debug.LogWarning("[Client] No tengo clientKey todavía");
            return;
        }

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Move);
            writer.Write(clientKey);
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);

            byte[] data = ms.ToArray();
            SendBinary(data);
        }
    }

    public void SendCubeRotation(Vector3 rotation)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey))
        {
            Debug.LogWarning("[Client] No tengo clientKey todavía");
            return;
        }

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms))
        {
            writer.Write((byte)MessageType.Rotate);
            writer.Write(clientKey);
            writer.Write(rotation.x);
            writer.Write(rotation.y);
            writer.Write(rotation.z);

            byte[] data = ms.ToArray();
            SendBinary(data);
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