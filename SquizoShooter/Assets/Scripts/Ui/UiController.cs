using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UiController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenu;
    public GameObject deathPanel;
    public Button respawnButton;
    public Button quitButton;
    //public TextMeshProUGUI connectionStatusText;
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

    // Cachear el jugador local para mejor rendimiento
    private PlayerController cachedLocalPlayer;

    void Start()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        if (deathPanel != null)
            deathPanel.SetActive(false);

        // Configurar listeners de botones de muerte
        if (respawnButton != null)
        {
            respawnButton.onClick.AddListener(OnRespawnClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        // Intentar encontrar el jugador local con un pequeño delay
        StartCoroutine(FindLocalPlayerDelayed());
    }

    private IEnumerator FindLocalPlayerDelayed()
    {
        // Esperar un frame para que todo se inicialice
        yield return new WaitForSeconds(0.5f);

        cachedLocalPlayer = FindLocalPlayer();
        if (cachedLocalPlayer != null)
        {
            Debug.Log($"[UiController] Jugador local encontrado y cacheado: {cachedLocalPlayer.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("[UiController] No se encontró jugador local en Start. Se buscará dinámicamente.");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Actualizar cache si es null
            if (cachedLocalPlayer == null)
                cachedLocalPlayer = FindLocalPlayer();

            // No permitir pausar si está muerto
            if (cachedLocalPlayer != null && cachedLocalPlayer.IsDead)
                return;

            TogglePauseMenu();
        }
    }

    private void TogglePauseMenu()
    {
        isPaused = !isPaused;
        ShowPauseMenu(isPaused);

        if (isPaused)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
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
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    // ===== DEATH SYSTEM =====

    public void ShowDeathScreen()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);

            // Desbloquear cursor para poder hacer clic en los botones
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("[UiController] Death screen shown");
        }
        else
        {
            Debug.LogError("[UiController] deathPanel no está asignado!");
        }
    }

    public void HideDeathScreen()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);

            // Volver a bloquear cursor para gameplay
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log("[UiController] Death screen hidden");
        }
    }

    private void OnRespawnClicked()
    {
        Debug.Log("[UiController] Respawn button clicked");

        // Intentar usar el cache primero
        PlayerController localPlayer = cachedLocalPlayer;

        // Si el cache es null o fue destruido, buscar de nuevo
        if (localPlayer == null)
        {
            Debug.Log("[UiController] Cache null, buscando jugador local...");
            localPlayer = FindLocalPlayer();
            cachedLocalPlayer = localPlayer; // Actualizar cache
        }

        if (localPlayer != null)
        {
            Debug.Log($"[UiController] Jugador encontrado: {localPlayer.gameObject.name}, IsDead: {localPlayer.IsDead}, IsLocal: {localPlayer.IsLocalPlayer}");

            localPlayer.Respawn();
            HideDeathScreen();
        }
        else
        {
            Debug.LogError("[UiController] No se encontró el jugador local para respawnear!");

            // Debug adicional: mostrar todos los PlayerControllers encontrados
            PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
            Debug.Log($"[UiController] Total PlayerControllers encontrados: {allPlayers.Length}");
            for (int i = 0; i < allPlayers.Length; i++)
            {
                Debug.Log($"  - Player {i}: {allPlayers[i].gameObject.name}, IsLocal: {allPlayers[i].IsLocalPlayer}, IsDead: {allPlayers[i].IsDead}, Enabled: {allPlayers[i].enabled}");
            }
        }
    }

    private void OnQuitClicked()
    {
        Debug.Log("[UiController] Quit button clicked");

        // Desconectar del servidor
        if (GameplayManager.Instance != null)
        {
            GameplayManager.Instance.Disconnect();
        }

        // Cerrar aplicación
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private PlayerController FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>(true); // Incluir inactivos
        foreach (PlayerController player in players)
        {
            if (player != null && player.IsLocalPlayer)
            {
                return player;
            }
        }
        return null;
    }

    // ===== CONNECTION STATUS =====

    //public void ShowConnectingStatus()
    //{
    //    if (connectionStatusText != null)
    //    {
    //        connectionStatusText.text = connectingMessage;
    //        connectionStatusText.gameObject.SetActive(true);

    //        if (hideConnectionStatusCoroutine != null)
    //        {
    //            StopCoroutine(hideConnectionStatusCoroutine);
    //            hideConnectionStatusCoroutine = null;
    //        }
    //    }
    //}

    //public void ShowHostingStatus()
    //{
    //    if (connectionStatusText != null)
    //    {
    //        connectionStatusText.text = hostingMessage;
    //        connectionStatusText.gameObject.SetActive(true);

    //        if (hideConnectionStatusCoroutine != null)
    //        {
    //            StopCoroutine(hideConnectionStatusCoroutine);
    //            hideConnectionStatusCoroutine = null;
    //        }
    //    }
    //}

    //public void ShowConnectedStatus()
    //{
    //    if (connectionStatusText != null)
    //    {
    //        connectionStatusText.text = connectedMessage;
    //        connectionStatusText.gameObject.SetActive(true);

    //        if (hideConnectionStatusCoroutine != null)
    //            StopCoroutine(hideConnectionStatusCoroutine);

    //        hideConnectionStatusCoroutine = StartCoroutine(HideConnectionStatusAfterDelay(connectedDisplayTime));
    //    }
    //}

    //public void HideConnectionStatus()
    //{
    //    if (connectionStatusText != null)
    //    {
    //        connectionStatusText.gameObject.SetActive(false);
    //    }

    //    if (hideConnectionStatusCoroutine != null)
    //    {
    //        StopCoroutine(hideConnectionStatusCoroutine);
    //        hideConnectionStatusCoroutine = null;
    //    }
    //}
    //private IEnumerator HideConnectionStatusAfterDelay(float delay)
    //{
    //    yield return new WaitForSeconds(delay);

    //    if (connectionStatusText != null)
    //    {
    //        connectionStatusText.gameObject.SetActive(false);
    //    }

    //    hideConnectionStatusCoroutine = null;
    //}

    // ===== NOTIFICATIONS =====

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

    void OnDestroy()
    {
        // Limpiar listeners
        if (respawnButton != null)
        {
            respawnButton.onClick.RemoveListener(OnRespawnClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
        }
    }
}