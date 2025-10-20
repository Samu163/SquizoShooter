using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneController : MonoBehaviour
{
    [Header("Main Menu")]
    public Button tcpServerButton;
    public Button tcpClientButton;
    public Button udpServerButton;
    public Button udpClientButton;
    public Button quitButton;

    void Start()
    {
        if (tcpServerButton != null)
            tcpServerButton.onClick.AddListener(LoadTCPServer);

        if (tcpClientButton != null)
            tcpClientButton.onClick.AddListener(LoadTCPClient);

        if (udpServerButton != null)
            udpServerButton.onClick.AddListener(LoadUDPServer);

        if (udpClientButton != null)
            udpClientButton.onClick.AddListener(LoadUDPClient);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    public void LoadTCPServer()
    {
        SceneManager.LoadScene("TCPServer");
    }

    public void LoadTCPClient()
    {
        SceneManager.LoadScene("TCPClient");
    }

    public void LoadUDPServer()
    {
        SceneManager.LoadScene("UDPServer");
    }

    public void LoadUDPClient()
    {
        SceneManager.LoadScene("UDPClient");
    }

    public void LoadMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}