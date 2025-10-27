using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [Header("References")]
    public GameObject udpServerObject;  // Referencia al objeto de servidor
    public GameObject udpClientObject;  // Referencia al objeto de cliente
    public GameObject cubePrefab;      // Prefab del cubo

    private UDPServer udpServer;
    private UDPClient udpClient;

    void Start()
    {
        udpServer = udpServerObject.GetComponent<UDPServer>();
        udpClient = udpClientObject.GetComponent<UDPClient>();

        if (GameManager.Instance.GetGameMode() == GameManager.GameMode.Server)
        {
            StartServer();
        }
        else if (GameManager.Instance.GetGameMode() == GameManager.GameMode.Client)
        {
            StartClient();
        }
    }

    private void StartServer()
    {
        udpServerObject.SetActive(true);
        udpServer.StartServer();

        udpClientObject.SetActive(true);
        udpClient.StartConnection();
    }

    private void StartClient()
    {
        udpClientObject.SetActive(true);
        udpClient.StartConnection();
    }
}
