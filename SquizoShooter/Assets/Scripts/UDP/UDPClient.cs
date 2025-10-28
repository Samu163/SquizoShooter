using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Globalization;

public class UDPClient : MonoBehaviour
{
    [Header("Connection Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;
    [Tooltip("Si pones 0, el sistema asignará un puerto automáticamente")]
    public int clientPort = 0;

    [Header("Gameplay")]
    public GameObject cubePrefab;

    private Socket clientSocket;
    private EndPoint serverEndPoint;
    private Thread receiveThread;
    private volatile bool isConnected = false;

    private string clientKey = null;
    private readonly object cubesLock = new object();
    private Dictionary<string, GameObject> playerCubes = new Dictionary<string, GameObject>();

    private Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly object mainThreadLock = new object();

    public bool IsConnected => isConnected;

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

            // Instanciar cubo local
            GameObject myCube = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);

            lock (cubesLock)
            {
                playerCubes[clientKey] = myCube;
            }

            // Marcar como jugador local y habilitar movimiento
            CubeMovement move = myCube.GetComponent<CubeMovement>();
            if (move != null)
            {
                move.SetAsLocalPlayer(true);
                move.enabled = true;
            }

            // Enviar posición inicial
            SendCubeMovement(Vector3.zero);
            return;
        }

        if (message.StartsWith("MOVE:"))
        {
            // Formato: MOVE:<key>:x;y;z (usando punto y coma como separador)
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

            // Usar InvariantCulture para parsear con punto decimal
            if (!float.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(coords[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                Debug.LogWarning($"[Client] Error parseando coordenadas: {parts[2]}");
                return;
            }

            Vector3 newPos = new Vector3(x, y, z);

            // Ignorar mi propio movimiento
            if (key == clientKey)
                return;

            // Actualizar o crear cubo remoto (todo en el hilo principal)
            SafeEnqueueMain(() =>
            {
                lock (cubesLock)
                {
                    if (!playerCubes.ContainsKey(key))
                    {
                        // Crear nuevo cubo remoto
                        InstantiateRemoteCube(key, newPos);
                    }
                    else
                    {
                        // Actualizar posición existente
                        GameObject cube = playerCubes[key];
                        if (cube != null)
                        {
                            CubeMovement move = cube.GetComponent<CubeMovement>();
                            if (move != null)
                            {
                                move.UpdateCubePosition(newPos);
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

        if (message.StartsWith("GOODBYE:"))
        {
            string key = message.Substring(8);
            SafeEnqueueMain(() => RemoveCube(key));
        }
    }

    void InstantiateRemoteCube(string key, Vector3 position)
    {
        GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);

        // Desactivar control local
        CubeMovement movementScript = newCube.GetComponent<CubeMovement>();
        if (movementScript != null)
        {
            movementScript.SetAsLocalPlayer(false);
            movementScript.enabled = true;
            movementScript.UpdateCubePosition(position);
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

    public void SendCubeMovement(Vector3 position)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey))
        {
            Debug.LogWarning("[Client] No tengo clientKey todavía");
            return;
        }

        // Usar InvariantCulture para formatear con punto decimal y punto y coma como separador
        string posStr = string.Format(CultureInfo.InvariantCulture, "{0:F6};{1:F6};{2:F6}",
                                      position.x, position.y, position.z);
        string payload = $"{clientKey}:{posStr}";
        string message = "MOVE:" + payload;
        SendRawMessage(message);
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
        // Ejecutar acciones en el hilo principal
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