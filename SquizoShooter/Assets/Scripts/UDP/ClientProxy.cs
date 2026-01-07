using System.Net;
using UnityEngine;

public class ClientProxy
{
    public string ClientKey { get; private set; }
    public EndPoint RemoteEndPoint { get; private set; }
    public long LastPacketTime { get; set; }

    public string Name { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public float Health { get; set; }
    public int WeaponID { get; set; }
    public bool IsReady { get; set; }
    public bool IsSpectating { get; set; }
    public int SpawnIndex { get; set; }

    public ClientProxy(string key, string name, EndPoint endPoint, int spawnIndex)
    {
        ClientKey = key;
        Name = name;
        RemoteEndPoint = endPoint;
        SpawnIndex = spawnIndex;
        LastPacketTime = System.DateTime.Now.Ticks; 

        Position = Vector3.zero;
        Rotation = Vector3.zero;
        Health = 100f;
        WeaponID = 1;
        IsReady = false;
        IsSpectating = false;
    }
    public void InitializeGameData(bool asSpectator)
    {
        Position = Vector3.zero;
        WeaponID = 1;
        IsSpectating = asSpectator;
        Health = asSpectator ? 0f : 100f;
    }
}