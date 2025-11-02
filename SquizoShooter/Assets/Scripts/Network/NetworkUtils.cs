using System.Net;
using System.Net.Sockets;
using UnityEngine;

public static class NetworkUtils
{
    // Gets the local IP address of this machine on the network
    public static string GetLocalIPAddress()
    {
        try
        {
            // Method 1: Connect to external IP to find local IP (most reliable)
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            // Method 2: Fallback to DNS lookup
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());

                // First pass: look for private network IPs
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        if (ipStr.StartsWith("192.168.") || ipStr.StartsWith("10.") || ipStr.StartsWith("172."))
                        {
                            return ipStr;
                        }
                    }
                }

                // Second pass: return any IPv4 that's not localhost
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = ip.ToString();
                        if (ipStr != "127.0.0.1")
                        {
                            return ipStr;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkUtils] Error getting local IP: {ex.Message}");
            }

            return "127.0.0.1";
        }
    }

    // Finds an available port starting from the specified port
    public static int FindAvailablePort(int startPort, int maxAttempts = 100)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            int port = startPort + i;
            if (IsPortAvailable(port))
            {
                Debug.Log($"[NetworkUtils] Port {port} is available");
                return port;
            }
        }

        Debug.LogError($"[NetworkUtils] No available port found after {maxAttempts} attempts");
        return -1;
    }

    // Checks if a specific port is available
    public static bool IsPortAvailable(int port)
    {
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}