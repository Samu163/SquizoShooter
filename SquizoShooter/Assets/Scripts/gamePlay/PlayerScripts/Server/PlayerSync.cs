using UnityEngine;
using System.Collections.Generic;

public class PlayerSync : MonoBehaviour
{
    private PlayerController playerController;
    private UDPClient udpClient;

    private Vector3 lastSentPosition;
    private Vector3 lastSentRotation;
    private float lastSentHealth;
    private int localSequenceNumber = 0;

    private const float positionThreshold = 0.01f;
    private const float rotationThreshold = 0.5f;
    private const float healthThreshold = 0.01f;

    // --- DATOS REMOTOS (PARA INTERPOLAR) ---
    [System.Serializable]
    public struct StateSnapshot
    {
        public Vector3 position;
        public Vector3 rotation;
        public float timestamp;
        public int sequence;
    }

    private List<StateSnapshot> stateBuffer = new List<StateSnapshot>();
    private int lastReceivedSequence = -1;
    private float interpolationDelay = 0.15f; 

    private Vector3 latestRemotePos;
    private Vector3 latestRemoteRot;

    public void Initialize(PlayerController pc)
    {
        playerController = pc;
        udpClient = FindObjectOfType<UDPClient>();
        latestRemotePos = transform.position;
        latestRemoteRot = transform.eulerAngles;
    }

    void Update()
    {
        if (playerController.IsDead) return;

        if (playerController.IsLocalPlayer)
        {
            SendPositionToServer();
            SendRotationToServer();
            SendPlayerDataToServer();
        }
        else
        {
            InterpolateRemotePlayer();
        }
    }

    void InterpolateRemotePlayer()
    {
        if (stateBuffer.Count == 0) return;

        float renderTime = Time.time - interpolationDelay;

        while (stateBuffer.Count > 2 && stateBuffer[0].timestamp < renderTime - 1.0f)
        {
            stateBuffer.RemoveAt(0);
        }

        StateSnapshot fromNode = stateBuffer[0];
        StateSnapshot toNode = stateBuffer[0];
        bool found = false;

        for (int i = 0; i < stateBuffer.Count - 1; i++)
        {
            if (stateBuffer[i].timestamp <= renderTime && stateBuffer[i + 1].timestamp >= renderTime)
            {
                fromNode = stateBuffer[i];
                toNode = stateBuffer[i + 1];
                found = true;
                break;
            }
        }

        if (found)
        {
            float timeDiff = toNode.timestamp - fromNode.timestamp;
            float t = (renderTime - fromNode.timestamp) / timeDiff;

            transform.position = Vector3.Lerp(fromNode.position, toNode.position, t);

            float yaw = Mathf.LerpAngle(fromNode.rotation.y, toNode.rotation.y, t);
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            float pitch = Mathf.LerpAngle(fromNode.rotation.x, toNode.rotation.x, t);
            if (playerController.GetVisuals())
                playerController.GetVisuals().UpdateAiming(pitch);
        }
        else
        {
            if (stateBuffer.Count > 0)
            {
                var last = stateBuffer[stateBuffer.Count - 1];
                transform.position = Vector3.Lerp(transform.position, last.position, Time.deltaTime * 5f);
            }
        }
    }

    public void OnReceivePosition(Vector3 newPos, int seq)
    {
        if (seq <= lastReceivedSequence) return;
        lastReceivedSequence = seq;
        latestRemotePos = newPos;
        AddToBuffer(latestRemotePos, latestRemoteRot, seq);
    }

    public void OnReceiveRotation(Vector3 newRot, int seq)
    {
        int safeSeq = (seq > lastReceivedSequence) ? seq : lastReceivedSequence;
        latestRemoteRot = newRot;
        AddToBuffer(latestRemotePos, latestRemoteRot, safeSeq);
    }

    void AddToBuffer(Vector3 pos, Vector3 rot, int seq)
    {
        stateBuffer.Add(new StateSnapshot
        {
            position = pos,
            rotation = rot,
            sequence = seq,
            timestamp = Time.time
        });
    }

    // --- ENVÍO ---
    public void SendPositionToServer()
    {
        if (udpClient && udpClient.IsConnected)
        {
            if (Vector3.Distance(playerController.transform.position, lastSentPosition) > positionThreshold)
            {
                localSequenceNumber++;
                udpClient.SendCubeMovement(playerController.transform.position, localSequenceNumber);
                lastSentPosition = playerController.transform.position;
            }
        }
    }

    public void SendRotationToServer()
    {
        if (udpClient && udpClient.IsConnected)
        {
            float yaw = playerController.transform.eulerAngles.y;
            float pitch = 0f;
            if (playerController.GetPlayerCamera())
                pitch = playerController.GetPlayerCamera().CameraTransform.eulerAngles.x;

            Vector3 currentRotation = new Vector3(pitch, yaw, 0);

            if (Vector3.Distance(lastSentRotation, currentRotation) > rotationThreshold)
            {
                udpClient.SendCubeRotation(currentRotation, localSequenceNumber);
                lastSentRotation = currentRotation;
            }
        }
    }

    public void SendPlayerDataToServer()
    {
        if (udpClient && udpClient.IsConnected)
        {
            var life = playerController.GetLifeComponent();
            if (life && Mathf.Abs(life.Health - lastSentHealth) > healthThreshold)
            {
                udpClient.SendPlayerHealth(life.Health);
                lastSentHealth = life.Health;
            }
        }
    }

    public void SendWeaponChange(int id) { if (udpClient) udpClient.SendWeaponChange(id); }
    public void SendWeaponThrow(int id, Vector3 p, Vector3 d) { if (udpClient) udpClient.SendWeaponThrow(id, p, d); }
    public void SendWeaponPickup(int id) { }

    // --- RESET ---
    public void ResetSync(Vector3 spawnPos, float health)
    {
        localSequenceNumber = 0;
        lastSentPosition = Vector3.zero;

        if (udpClient && udpClient.IsConnected)
        {
            udpClient.SendCubeMovement(spawnPos, 0);
            udpClient.SendPlayerHealth(health);
        }
    }

    // --- NUEVO PARA ARREGLAR RONDA 2 ---
    public void ForceRemoteReset()
    {
        stateBuffer.Clear();
        lastReceivedSequence = -1;
        latestRemotePos = transform.position;
    }

    public UDPClient GetUDPClient() => udpClient;
}