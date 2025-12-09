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

    public void SendRotationToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            float yaw = playerController.transform.eulerAngles.y;

            float pitch = 0f;
            if (playerController.GetPlayerCamera() != null)
            {
                pitch = playerController.GetPlayerCamera().CameraTransform.eulerAngles.x;
            }
            Vector3 currentRotation = new Vector3(pitch, yaw, 0);

            bool pitchChanged = Mathf.Abs(Mathf.DeltaAngle(lastSentRotation.x, currentRotation.x)) > rotationThreshold;
            bool yawChanged = Mathf.Abs(Mathf.DeltaAngle(lastSentRotation.y, currentRotation.y)) > rotationThreshold;

            if (pitchChanged || yawChanged)
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

    public void SendWeaponChange(int weaponID)
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            const int ManualWeaponChangeStationID = 0;
            udpClient.SendWeaponRequest(ManualWeaponChangeStationID, weaponID);
            Debug.Log($"[WeaponManager] Solicitud de cambio a arma ID {weaponID} enviada al servidor.");
        }
    }

    public UDPClient GetUDPClient() => udpClient;
}