using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ServerDiscoveryManager : MonoBehaviour
{
    [Header("Discovery Settings")]
    public int discoveryPort = 47777;

    private UdpClient discoveryBroadcaster;
    private UdpClient discoveryListener;
    private Thread discoveryThread;
    private volatile bool isRunning = false;

    // Events
    public event Action<string, int> OnServerFound;

    // Start broadcasting server information
    public void StartBroadcasting(string serverIP, int serverPort)
    {
        if (isRunning)
        {
            Debug.LogWarning("[ServerDiscovery] Already running!");
            return;
        }

        isRunning = true;
        discoveryThread = new Thread(() => BroadcastLoop(serverIP, serverPort)) { IsBackground = true };
        discoveryThread.Start();

        Debug.Log($"[ServerDiscovery] Started broadcasting: {serverIP}:{serverPort}");
    }

    // Start listening for server broadcasts
    public void StartListening()
    {
        if (isRunning)
        {
            Debug.LogWarning("[ServerDiscovery] Already running!");
            return;
        }

        isRunning = true;
        discoveryThread = new Thread(ListenLoop) { IsBackground = true };
        discoveryThread.Start();

        Debug.Log("[ServerDiscovery] Started listening for servers...");
    }

    // Stop discovery process
    public void Stop()
    {
        isRunning = false;

        if (discoveryThread != null && discoveryThread.IsAlive)
        {
            discoveryThread.Join(1000);
        }

        try { discoveryBroadcaster?.Close(); } catch { }
        try { discoveryListener?.Close(); } catch { }

        Debug.Log("[ServerDiscovery] Stopped");
    }

    private void BroadcastLoop(string serverIP, int serverPort)
    {
        try
        {
            discoveryBroadcaster = new UdpClient();
            discoveryBroadcaster.EnableBroadcast = true;

            IPEndPoint broadcastEP = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
            string message = $"GAMESERVER:{serverIP}:{serverPort}";
            byte[] data = Encoding.UTF8.GetBytes(message);

            Debug.Log($"[ServerDiscovery] Broadcasting on port {discoveryPort}");

            while (isRunning)
            {
                try
                {
                    discoveryBroadcaster.Send(data, data.Length, broadcastEP);
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ServerDiscovery] Broadcast error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerDiscovery] Broadcaster setup error: {ex.Message}");
        }
        finally
        {
            try { discoveryBroadcaster?.Close(); } catch { }
        }
    }

    private void ListenLoop()
    {
        try
        {
            discoveryListener = new UdpClient(discoveryPort);
            discoveryListener.EnableBroadcast = true;
            discoveryListener.Client.ReceiveTimeout = 10000;

            Debug.Log($"[ServerDiscovery] Listening on port {discoveryPort}");

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                try
                {
                    byte[] data = discoveryListener.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);

                    Debug.Log($"[ServerDiscovery] Received: {message} from {remoteEP.Address}");

                    if (message.StartsWith("GAMESERVER:"))
                    {
                        string[] parts = message.Split(':');
                        if (parts.Length >= 3)
                        {
                            string serverIP = parts[1];

                            if (int.TryParse(parts[2], out int serverPort))
                            {
                                Debug.Log($"[ServerDiscovery] Found server at {serverIP}:{serverPort}");

                                isRunning = false;

                                // Trigger event on main thread
                                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                                {
                                    OnServerFound?.Invoke(serverIP, serverPort);
                                });

                                break;
                            }
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        Debug.Log("[ServerDiscovery] No servers found, retrying...");
                        continue;
                    }
                    else if (isRunning)
                    {
                        Debug.LogWarning($"[ServerDiscovery] Error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"[ServerDiscovery] Error: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ServerDiscovery] Listener setup error: {ex.Message}");
        }
        finally
        {
            try { discoveryListener?.Close(); } catch { }
        }
    }

    private void OnDestroy()
    {
        Stop();
    }
}