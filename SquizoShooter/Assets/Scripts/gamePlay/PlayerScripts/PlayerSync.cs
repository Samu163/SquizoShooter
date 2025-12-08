using UnityEngine;

public class PlayerSync : MonoBehaviour
{
    private PlayerController playerController;
    private UDPClient udpClient;

    private Vector3 lastSentPosition;
    private Vector3 lastSentRotation;
    private float lastSentHealth;

    private const float positionThreshold = 0.01f;
    private const float rotationThreshold = 0.5f;
    private const float healthThreshold = 0.01f;

    public void Initialize(PlayerController pc)
    {
        playerController = pc;
        udpClient = FindObjectOfType<UDPClient>();
    }

    public void SendPositionToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            if (Vector3.Distance(playerController.transform.position, lastSentPosition) > positionThreshold)
            {
                udpClient.SendCubeMovement(playerController.transform.position);
                lastSentPosition = playerController.transform.position;
            }
        }
    }
    //TODO: Pillar rotacion Eje vertical (probablemente el Z)
    public void SendRotationToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            float yaw = playerController.transform.eulerAngles.y;
            Vector3 currentRotation = new Vector3(0, yaw, 0);

            if (Mathf.Abs(Mathf.DeltaAngle(lastSentRotation.y, currentRotation.y)) > rotationThreshold)
            {
                udpClient.SendCubeRotation(currentRotation);
                lastSentRotation = currentRotation;
            }
        }
    }

    public void SendPlayerDataToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            LifeComponent life = playerController.GetLifeComponent();
            if (life != null)
            {
                if (Mathf.Abs(life.Health - lastSentHealth) > healthThreshold)
                {
                    udpClient.SendPlayerHealth(life.Health);
                    lastSentHealth = life.Health;
                }
            }
        }
    }

    public void ResetSync(Vector3 spawnPos, float health)
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            lastSentPosition = Vector3.zero;
            udpClient.SendCubeMovement(spawnPos);
            udpClient.SendPlayerHealth(health);
        }
    }

    public UDPClient GetUDPClient() => udpClient;
}