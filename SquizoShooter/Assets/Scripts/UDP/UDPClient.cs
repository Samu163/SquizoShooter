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

    private string clientKey = null; // GUID as string
    private readonly object cubesLock = new object();
    private Dictionary<string, GameObject> playerCubes = new Dictionary<string, GameObject>();

    private Queue<Action> mainThreadActions = new Queue<Action>();
    private readonly object mainThreadLock = new object();

    public bool IsConnected => isConnected;

    // Llamar para iniciar conexión (desde otro script o Start)
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

            // Bind al puerto del cliente (0 -> SO asigna puerto libre)
            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, clientPort);
            clientSocket.Bind(localEP);

            // Opcional: establecer tiempo de bloqueo corto en Receive para permitir checks periódicos.
            // clientSocket.ReceiveTimeout = 2000; // si lo pones, atraparás SocketException con Timeout

            // Enviar handshake
            SendRawMessage("HANDSHAKE");

            isConnected = true;
            Debug.Log($"UDP client started and bound to {(clientSocket.LocalEndPoint != null ? clientSocket.LocalEndPoint.ToString() : "unknown")}");

            // Empezar hilo de recepción
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError("UDPClient ConnectProcess error: " + ex.Message);
            isConnected = false;
            SafeEnqueueMain(() => Debug.LogError("Conexión UDP fallida: " + ex.Message));
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
                    // Blocking receive. Si cierras el socket desde otro hilo, esto lanzará una excepción y saldrás.
                    int bytes = clientSocket.ReceiveFrom(buffer, ref remoteEP);
                    if (bytes > 0)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, bytes);
                        Debug.Log($"[Client] Recibido de {remoteEP}: {msg}");
                        // Procesar en hilo principal
                        SafeEnqueueMain(() => ProcessServerMessage(msg));
                    }
                }
                catch (SocketException se)
                {
                    // Si se está cerrando el socket, salir silenciosamente
                    if (!isConnected)
                        break;
                    Debug.LogWarning("[Client] SocketException in ReceiveLoop: " + se.Message);
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
            // asegurar limpieza
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

            // Instanciar mi propio cubo local
            GameObject myCube = Instantiate(cubePrefab, Vector3.zero, Quaternion.identity);
            playerCubes[clientKey] = myCube;

            // Mantener activo el script de movimiento SOLO en el local
            CubeMovement move = myCube.GetComponent<CubeMovement>();
            if (move != null)
                move.enabled = true;

            // Enviar mi posición inicial al servidor
            SendCubeMovement(Vector3.zero);
            return;
        }

        if (message.StartsWith("MOVE:"))
        {
            // Nuevo formato: MOVE:<key>:x,y,z
            string[] parts = message.Split(':');
            if (parts.Length < 3) return;

            string key = parts[1];
            string[] coords = parts[2].Split(',');
            if (coords.Length < 3) return;

            float x = float.Parse(coords[0]);
            float y = float.Parse(coords[1]);
            float z = float.Parse(coords[2]);
            Vector3 newPos = new Vector3(x, y, z);

            // Si es mi propio cubo, ignorar (ya se mueve localmente)
            if (key == clientKey)
                return;

            // Si es un jugador remoto, crear/actualizar su cubo
            if (!playerCubes.ContainsKey(key))
            {
                mainThreadActions.Enqueue(() => InstantiateRemoteCube(newPos, key));
            }
            else
            {
                mainThreadActions.Enqueue(() => playerCubes[key].transform.position = newPos);
            }
        }
    }

    void InstantiateRemoteCube(Vector3 position, string key)
    {
        GameObject newCube = Instantiate(cubePrefab, position, Quaternion.identity);

        // Desactivar el control local
        CubeMovement movementScript = newCube.GetComponent<CubeMovement>();
        if (movementScript != null)
            movementScript.enabled = false;

        playerCubes[key] = newCube;

        Debug.Log($"Instantiated remote cube for {key} at {position}");
    }

    void InstantiateOrMoveCube(string key, Vector3 pos)
    {
        lock (cubesLock)
        {
            if (!playerCubes.ContainsKey(key))
            {
                GameObject go = Instantiate(cubePrefab, pos, Quaternion.identity);
                playerCubes[key] = go;
                Debug.Log($"[Client] Instantiated cube for {key} at {pos}");
            }
            else
            {
                playerCubes[key].transform.position = pos;
            }
        }
    }

    void RemoveCube(string key)
    {
        lock (cubesLock)
        {
            if (playerCubes.TryGetValue(key, out GameObject go))
            {
                Destroy(go);
                playerCubes.Remove(key);
                Debug.Log($"[Client] Removed cube for {key}");
            }
        }
    }

    public void SendCubeMovement(Vector3 position)
    {
        if (!isConnected || clientSocket == null) return;
        if (string.IsNullOrEmpty(clientKey))
        {
            // si aún no tenemos clientKey, enviar sin clave para que servidor ignore (o se puede bufferizar)
            Debug.LogWarning("[Client] Aún no tengo clientKey, intentando enviar MOVE sin clave");
        }

        string payload = $"{clientKey}:{position.x.ToString("G9")},{position.y.ToString("G9")},{position.z.ToString("G9")}";
        string message = "MOVE:" + payload;
        SendRawMessage(message);
    }

    private void SendRawMessage(string message)
    {
        try
        {
            if (serverEndPoint == null) serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            byte[] data = Encoding.UTF8.GetBytes(message);
            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEndPoint);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Client] Error sending message: " + ex.Message);
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
        // ejecutar acciones en el hilo principal (instanciar cubos, mover objetos Unity, etc.)
        while (true)
        {
            Action a = null;
            lock (mainThreadLock)
            {
                if (mainThreadActions.Count > 0) a = mainThreadActions.Dequeue();
            }
            if (a == null) break;
            try { a.Invoke(); } catch (Exception e) { Debug.LogError("[Client] Error en acción principal: " + e.Message); }
        }
    }

    void DisconnectInternal()
    {
        // Llamado desde receive thread cuando termina
        isConnected = false;
        try
        {
            if (clientSocket != null)
            {
                clientSocket.Close(); // desbloquea Receive
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
            // intenta notificar al servidor (opcional)
            if (!string.IsNullOrEmpty(clientKey))
            {
                SendRawMessage("GOODBYE:" + clientKey);
            }
        }
        catch { }

        // cerrar socket para desbloquear receive
        try { clientSocket?.Close(); } catch { }

        // esperar hilo
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }

        clientSocket = null;
        Debug.Log("[Client] Disconnected");
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
