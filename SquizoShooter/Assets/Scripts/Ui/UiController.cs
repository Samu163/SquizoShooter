using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UiController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenu;
    public TextMeshProUGUI connectionStatusText;
    public TextMeshProUGUI notificationText;

    [Header("Connection Status Settings")]
    public string connectingMessage = "Connecting to server...";
    public string hostingMessage = "Starting server...";
    public string connectedMessage = "Connected!";
    public float connectedDisplayTime = 2f;

    [Header("Notification Settings")]
    public float notificationDuration = 3f;

    private bool isPaused = false;
    private Coroutine hideNotificationCoroutine;
    private Coroutine hideConnectionStatusCoroutine;

    void Start()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
        }
    }

    private void TogglePauseMenu()
    {
        isPaused = !isPaused;
        ShowPauseMenu(isPaused);

        if (isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ShowPauseMenu(bool show)
    {
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(show);
            isPaused = show;

            if (show)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    public void ShowConnectingStatus()
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = connectingMessage;
            connectionStatusText.gameObject.SetActive(true);

            if (hideConnectionStatusCoroutine != null)
            {
                StopCoroutine(hideConnectionStatusCoroutine);
                hideConnectionStatusCoroutine = null;
            }
        }
    }

    public void ShowHostingStatus()
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = hostingMessage;
            connectionStatusText.gameObject.SetActive(true);

            if (hideConnectionStatusCoroutine != null)
            {
                StopCoroutine(hideConnectionStatusCoroutine);
                hideConnectionStatusCoroutine = null;
            }
        }
    }

    public void ShowConnectedStatus()
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = connectedMessage;
            connectionStatusText.gameObject.SetActive(true);

            if (hideConnectionStatusCoroutine != null)
                StopCoroutine(hideConnectionStatusCoroutine);

            hideConnectionStatusCoroutine = StartCoroutine(HideConnectionStatusAfterDelay(connectedDisplayTime));
        }
    }

    public void HideConnectionStatus()
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.gameObject.SetActive(false);
        }

        if (hideConnectionStatusCoroutine != null)
        {
            StopCoroutine(hideConnectionStatusCoroutine);
            hideConnectionStatusCoroutine = null;
        }
    }
    public void ShowNotification(string message)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.gameObject.SetActive(true);

            if (hideNotificationCoroutine != null)
                StopCoroutine(hideNotificationCoroutine);

            hideNotificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
        }
    }

    public void ShowPlayerJoined(string playerName = "")
    {
        string message = string.IsNullOrEmpty(playerName)
            ? "A player has joined the game"
            : $"{playerName} has joined the game";
        ShowNotification(message);
    }

    public void ShowPlayerLeft(string playerName = "")
    {
        string message = string.IsNullOrEmpty(playerName)
            ? "A player has left the game"
            : $"{playerName} has left the game";
        ShowNotification(message);
    }

    public void ShowNotification(string message, Color color)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.color = color;
            notificationText.gameObject.SetActive(true);

            if (hideNotificationCoroutine != null)
                StopCoroutine(hideNotificationCoroutine);

            hideNotificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
        }
    }

    private IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSeconds(notificationDuration);

        if (notificationText != null)
        {
            notificationText.gameObject.SetActive(false);
        }

        hideNotificationCoroutine = null;
    }

    private IEnumerator HideConnectionStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (connectionStatusText != null)
        {
            connectionStatusText.gameObject.SetActive(false);
        }

        hideConnectionStatusCoroutine = null;
    }

    public void ResumeGame()
    {
        ShowPauseMenu(false);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}