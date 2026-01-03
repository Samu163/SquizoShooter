using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;

public class UiController : MonoBehaviour
{
    public static UiController Instance { get; private set; }
    
    [Header("UI References")]
    public GameObject pauseMenu;
    public Button respawnButton;
    public Button quitButton;
    public TextMeshProUGUI notificationText;

    [Header("Hitmarker")]
    public GameObject hitMarkerImage; 
    public float hitMarkerDuration = 0.1f;
    private Coroutine hitMarkerCoroutine;

    [Header("Network Debug Panel")]
    public GameObject networkDebugPanel;
    public TextMeshProUGUI networkDebugText;
    public float debugUpdateInterval = 0.5f;

    [Header("Notification Settings")]
    public float notificationDuration = 3f;

    [Header("Lobby & Game HUD")]
    public LobbyUiController lobbyController;
    public GameObject gameHUD;

    [Header("Spectator")]
    public GameObject spectatorObject;

    private bool isPaused = false;
    private bool isDebugPanelVisible = false;
    private Coroutine hideNotificationCoroutine;
    private Coroutine debugUpdateCoroutine;

    private PlayerController cachedLocalPlayer;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        if (networkDebugPanel != null)
            networkDebugPanel.SetActive(false);

        if (respawnButton != null)
        {
            respawnButton.onClick.AddListener(OnRespawnClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }
        if (hitMarkerImage != null) hitMarkerImage.SetActive(false);
        if (gameHUD != null) gameHUD.SetActive(false);
        if (lobbyController != null) lobbyController.gameObject.SetActive(true);

        StartCoroutine(FindLocalPlayerDelayed());
    }

    public void EnterLobbyMode()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (gameHUD != null) gameHUD.SetActive(false);
        if (lobbyController != null) lobbyController.gameObject.SetActive(true);
        if (lobbyController != null) lobbyController.Init();
    }

    public void EnableGameHUD()
    {
        if (lobbyController != null) lobbyController.gameObject.SetActive(false);
        if (gameHUD != null) gameHUD.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator FindLocalPlayerDelayed()
    {
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
            if (cachedLocalPlayer == null)
                cachedLocalPlayer = FindLocalPlayer();

            if (cachedLocalPlayer != null && cachedLocalPlayer.IsDead)
                return;

            TogglePauseMenu();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            ToggleNetworkDebugPanel();
        }
    }

    public void ShowHitMarker()
    {
        if (hitMarkerImage != null)
        {
          
            if (hitMarkerCoroutine != null)
            {
                StopCoroutine(hitMarkerCoroutine);
            }

            hitMarkerImage.SetActive(true);
            hitMarkerCoroutine = StartCoroutine(HideHitMarker());
        }
    }

    private IEnumerator HideHitMarker()
    {
        yield return new WaitForSeconds(hitMarkerDuration);

        if (hitMarkerImage != null)
        {
            hitMarkerImage.SetActive(false);
        }
        hitMarkerCoroutine = null;
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

    // ===== NETWORK DEBUG PANEL =====
    private void ToggleNetworkDebugPanel()
    {
        isDebugPanelVisible = !isDebugPanelVisible;

        if (networkDebugPanel != null)
        {
            networkDebugPanel.SetActive(isDebugPanelVisible);

            if (isDebugPanelVisible)
            {
                // Iniciar actualización continua
                if (debugUpdateCoroutine != null)
                    StopCoroutine(debugUpdateCoroutine);
                debugUpdateCoroutine = StartCoroutine(UpdateNetworkDebugInfo());
            }
            else
            {
                // Detener actualización
                if (debugUpdateCoroutine != null)
                {
                    StopCoroutine(debugUpdateCoroutine);
                    debugUpdateCoroutine = null;
                }
            }
        }
    }

    private IEnumerator UpdateNetworkDebugInfo()
    {
        while (isDebugPanelVisible)
        {
            UpdateNetworkDebugText();
            yield return new WaitForSeconds(debugUpdateInterval);
        }
    }

    private void UpdateNetworkDebugText()
    {
        if (networkDebugText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== NETWORK DEBUG INFO ===\n");

        // Game Mode
        if (GameManager.Instance != null)
        {
            GameManager.GameMode mode = GameManager.Instance.GetGameMode();
            sb.AppendLine($"<color=yellow>MODE:</color> {mode}");
        }
        else
        {
            sb.AppendLine("<color=red>GameManager: NULL</color>");
        }

        sb.AppendLine();

        // GameplayManager Info
        if (GameplayManager.Instance != null)
        {
            var gm = GameplayManager.Instance;

            var discovery = gm.GetComponent<ServerDiscoveryManager>();
            if (discovery != null)
            {
                sb.AppendLine("SERVER DISCOVERY:");
                sb.AppendLine($"  Searching: {discovery.IsSearching()}");
                sb.AppendLine($"  Port: {discovery.discoveryPort}");
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("GameplayManager: NULL\n");
        }

        // UDP Client Info
        var udpClient = FindObjectOfType<UDPClient>();
        if (udpClient != null)
        {
            sb.AppendLine("UDP CLIENT:");
            sb.AppendLine($"  Connected: {udpClient.IsConnected}");
            sb.AppendLine($"  Server IP: {udpClient.serverIP}");
            sb.AppendLine($"  Server Port: {udpClient.serverPort}");
            sb.AppendLine($"  Client Port: {udpClient.clientPort}");
            sb.AppendLine($"  Client Key: {(string.IsNullOrEmpty(udpClient.ClientKey) ? "NULL" : udpClient.ClientKey.Substring(0, 8) + "...")}");
        }
        else
        {
            sb.AppendLine("UDP CLIENT: NOT FOUND");
        }

        sb.AppendLine();

        // UDP Server Info
        var udpServer = FindObjectOfType<UDPServer>();
        if (udpServer != null && udpServer.gameObject.activeInHierarchy)
        {
            sb.AppendLine("UDP SERVER:");
            sb.AppendLine($"  Port: {udpServer.port}");
            sb.AppendLine($"  Active: {udpServer.enabled}");
        }
        else
        {
            sb.AppendLine("UDP SERVER: INACTIVE");
        }

        sb.AppendLine();

        // Local Player Info
        if (cachedLocalPlayer != null)
        {
            sb.AppendLine("LOCAL PLAYER:");
            sb.AppendLine($"  Position: {cachedLocalPlayer.transform.position}");
            sb.AppendLine($"  Health: {cachedLocalPlayer.GetLifeComponent().health:F1}");
            sb.AppendLine($"  Is Dead: {cachedLocalPlayer.IsDead}");
        }
        else
        {
            sb.AppendLine("LOCAL PLAYER: NULL");
        }

        sb.AppendLine();

        // Network Stats
        sb.AppendLine("NETWORK STATS:");
        PlayerController[] allPlayers = FindObjectsOfType<PlayerController>();
        sb.AppendLine($"  Total Players: {allPlayers.Length}");
        int remotePlayers = 0;
        foreach (var p in allPlayers)
        {
            if (!p.IsLocalPlayer) remotePlayers++;
        }
        sb.AppendLine($"  Remote Players: {remotePlayers}");

        sb.AppendLine();

        // System Info
        sb.AppendLine("SYSTEM:");
        sb.AppendLine($"  Local IP: {NetworkUtils.GetLocalIPAddress()}");
        sb.AppendLine($"  FPS: {(int)(1f / Time.unscaledDeltaTime)}");
        sb.AppendLine($"  Time Scale: {Time.timeScale}");

        networkDebugText.text = sb.ToString();
    }

    // ===== DEATH SYSTEM =====
    public void ShowDeathScreen()
    {
        if (spectatorObject != null)
        {
            spectatorObject.SetActive(true);
        }
    }

    public void HideDeathScreen()
    {   
        if (spectatorObject != null)
        {
            spectatorObject.SetActive(false);
        }
    }

    private void OnRespawnClicked()
    {
        Debug.Log("[UiController] Respawn button clicked");
        PlayerController localPlayer = cachedLocalPlayer;

        if (localPlayer == null)
        {
            Debug.Log("[UiController] Cache null, buscando jugador local...");
            localPlayer = FindLocalPlayer();
            cachedLocalPlayer = localPlayer; 
        }

        if (localPlayer != null)
        {
            Debug.Log($"[UiController] Jugador encontrado: {localPlayer.gameObject.name}, IsDead: {localPlayer.IsDead}, IsLocal: {localPlayer.IsLocalPlayer}");

            localPlayer.Respawn();
            HideDeathScreen();
        }
        else
        {
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

        if (GameplayManager.Instance != null)
        {
            GameplayManager.Instance.Disconnect();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private PlayerController FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>(true);
        foreach (PlayerController player in players)
        {
            if (player != null && player.IsLocalPlayer)
            {
                return player;
            }
        }
        return null;
    }

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
        if (respawnButton != null)
        {
            respawnButton.onClick.RemoveListener(OnRespawnClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        if (debugUpdateCoroutine != null)
        {
            StopCoroutine(debugUpdateCoroutine);
        }
    }
}