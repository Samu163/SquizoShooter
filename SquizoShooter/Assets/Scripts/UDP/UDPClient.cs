using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
    public List <LookAtPlayer> AllItems = new List<LookAtPlayer>();
    private Dictionary<int, HealStation> healStations = new Dictionary<int, HealStation>();
    private readonly object healStationsLock = new object();

    private Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly object mainThreadLock = new object();

    public bool IsConnected => isConnected;

    // Nueva propiedad pública para exponer la key local (lectura)
    public string ClientKey => clientKey;

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

            SendRawMessage("HANDSHAKE");

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
                        string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                        Debug.Log($"[Client] Recibido: {msg}");
                        SafeEnqueueMain(() => ProcessServerMessage(msg));
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

    void ProcessServerMessage(string message)
    {
        if (message.StartsWith("WELCOME:"))
        {
            clientKey = message.Substring(8);
            Debug.Log("Received client key: " + clientKey);

            Vector3 spawnPos = (GameplayManager.Instance != null)
                    ? GameplayManager.Instance.GetRandomSpawnPosition()
                    : Vector3.zero;

            GameObject myCube = Instantiate(cubePrefab, spawnPos, Quaternion.identity);

            lock (cubesLock)
            {
                playerCubes[clientKey] = myCube;
            }

            PlayerController move = myCube.GetComponent<PlayerController>();
            if (move != null)
            {
                move.SetAsLocalPlayer(true);
                move.enabled = true;
                foreach (var item in AllItems)
                {
                    item.AssignCamera(move.playerCamera);
                }
            }

            if (uiController != null)
                uiController.ShowNotification("You joined the game!", Color.green);

            SendCubeMovement(spawnPos);
            return;
        }

        if (message.StartsWith("SHOOT_ANIM:"))
        {
            string shooterKey = message.Substring("SHOOT_ANIM:".Length);
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
            return;
        }

        if (message.StartsWith("PLAYERDATA:"))
        {
            string payload = message.Substring("PLAYERDATA:".Length);
            int sep = payload.IndexOf(':');
            if (sep < 0) return;

            string key = payload.Substring(0, sep);
            string valueStr = payload.Substring(sep + 1);

            if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float health))
            {
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
            return;
        }

        if (message.StartsWith("MOVE:"))
        {
            string[] parts = message.Split(':');
            if (parts.Length < 3)
            {
                Debug.LogWarning($"[Client] MOVE mal formateado: {message}");
                return;
            }

            string key = parts[1];
            string[] coords = parts[2].Split(';');
            if (coords.Length < 3)
            {
                Debug.LogWarning($"[Client] Coordenadas incompletas: {parts[2]}");
                return;
            }

            if (!float.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(coords[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                Debug.LogWarning($"[Client] Error parseando coordenadas: {parts[2]}");
                return;
            }

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
            return;
        }

        if (message.StartsWith("ROTATE"))
        {
            string[] parts = message.Split(':');
            if (parts.Length < 3)
            {
                Debug.LogWarning($"[Client] ROTATE mal formateado: {message}");
                return;
            }
            string key = parts[1];
            string[] coords = parts[2].Split(';');
            if (coords.Length < 3)
            {
                Debug.LogWarning($"[Client] Coordenadas incompletas: {parts[2]}");
                return;
            }

            if (!float.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(coords[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                Debug.LogWarning($"[Client] Error parseando coordenadas: {parts[2]}");
                return;
            }
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
            return;
        }

        if (message.StartsWith("HEAL_STATION_DATA:"))
        {
            string[] parts = message.Split(':');

            if (parts.Length == 3 &&
                int.TryParse(parts[1], out int stationID) &&
                int.TryParse(parts[2], out int stateCode))
            {
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
            return;
        }

        if (message.StartsWith("KILL_CONFIRMED:"))
        {
            string shooterKey = message.Substring("KILL_CONFIRMED:".Length);
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
            return;
        }

        if (message.StartsWith("GOODBYE:"))
        {
            string key = message.Substring(8);
            SafeEnqueueMain(() => RemoveCube(key));
            if (uiController != null)
                uiController.ShowPlayerLeft();
            return;
        }
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

        string message = $"HEAL_REQUEST:{clientKey}:{stationID}";
        SendRawMessage(message);
    }
    public void SendCubeMovement(Vector3 position)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey))
        {
            Debug.LogWarning("[Client] No tengo clientKey todavía");
            return;
        }

        string posStr = string.Format(CultureInfo.InvariantCulture, "{0:F6};{1:F6};{2:F6}",
                                      position.x, position.y, position.z);
        string payload = $"{clientKey}:{posStr}";
        string message = "MOVE:" + payload;
        SendRawMessage(message);
    }

    public void SendCubeRotation(Vector3 rotation)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey))
        {
            Debug.LogWarning("[Client] No tengo clientKey todavía");
            return;
        }

        string rotStr = string.Format(CultureInfo.InvariantCulture, "{0:F6};{1:F6};{2:F6}",
                                      rotation.x, rotation.y, rotation.z);
        string payload = $"{clientKey}:{rotStr}";
        string message = "ROTATE:" + payload;
        SendRawMessage(message);
    }

    public void SendPlayerHealth(float health)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey)) return;

        string message = string.Format(CultureInfo.InvariantCulture, "PLAYERDATA:{0}:{1:F1}", clientKey, health);
        SendRawMessage(message);
    }

    public void SendShotToServer(string targetKey, float damage)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey)) return;
        if (string.IsNullOrEmpty(targetKey)) return;

        string dmgStr = damage.ToString("F1", CultureInfo.InvariantCulture);
        string message = $"SHOT:{clientKey}:{targetKey}:{dmgStr}";
        SendRawMessage(message);
    }

    // NEW: broadcast shoot animation intent
    public void SendShootAnim()
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey)) return;

        string message = $"SHOOT_ANIM:{clientKey}";
        SendRawMessage(message);
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

    private void SendRawMessage(string message)
    {
        try
        {
            if (serverEndPoint == null)
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            byte[] data = Encoding.UTF8.GetBytes(message);
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
                SendRawMessage("GOODBYE:" + clientKey);
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