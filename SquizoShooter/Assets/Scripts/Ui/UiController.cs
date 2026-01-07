using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text;


public class NetworkStats
{
    public int packetsSent = 0;
    public int packetsReceived = 0;
}
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
    public GameObject lobbyCamera;

    [Header("Spectator")]
    public GameObject spectatorObject;

    private bool isPaused = false;
    private bool isDebugPanelVisible = false;
    private Coroutine hideNotificationCoroutine;
    private Coroutine debugUpdateCoroutine;
    private PlayerController cachedLocalPlayer;

    private NetworkStats networkStats = new NetworkStats();

    void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (lobbyCamera) lobbyCamera.SetActive(true);
        if (pauseMenu != null) pauseMenu.SetActive(false);
        if (networkDebugPanel != null) networkDebugPanel.SetActive(false);

        if (respawnButton != null) respawnButton.onClick.AddListener(OnRespawnClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        if (hitMarkerImage != null) hitMarkerImage.SetActive(false);
        if (gameHUD != null) gameHUD.SetActive(false);
        if (lobbyController != null) lobbyController.gameObject.SetActive(true);

        StartCoroutine(FindLocalPlayerDelayed());
    }

    public void IncrementPacketsSent() => networkStats.packetsSent++;
    public void IncrementPacketsReceived() => networkStats.packetsReceived++;

    public void EnterLobbyMode()
    {
        SetCursorState(true);
        if (lobbyCamera) lobbyCamera.SetActive(true);
        if (gameHUD != null) gameHUD.SetActive(false);
        if (lobbyController != null)
        {
            lobbyController.gameObject.SetActive(true);
            lobbyController.Init();
        }
    }

    public void EnableGameHUD()
    {
        if (lobbyController != null) lobbyController.gameObject.SetActive(false);
        if (lobbyCamera != null) lobbyCamera.SetActive(false);
        if (gameHUD != null) gameHUD.SetActive(true);
        SetCursorState(false);
    }

    public void EnableSpectatorMode()
    {
        if (lobbyController != null) lobbyController.gameObject.SetActive(false);
        if (lobbyCamera != null) lobbyCamera.SetActive(false);
        if (gameHUD != null) gameHUD.SetActive(true);
        SetCursorState(false);
    }

    private void SetCursorState(bool visible)
    {
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
        Time.timeScale = 1f;
    }

    private IEnumerator FindLocalPlayerDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        cachedLocalPlayer = FindLocalPlayer();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (cachedLocalPlayer == null) cachedLocalPlayer = FindLocalPlayer();
            if (cachedLocalPlayer != null && cachedLocalPlayer.IsDead) return;
            TogglePauseMenu();
        }

        if (Input.GetKeyDown(KeyCode.L)) ToggleNetworkDebugPanel();
    }

    // ===== HITMARKER =====
    public void ShowHitMarker()
    {
        if (hitMarkerImage != null)
        {
            if (hitMarkerCoroutine != null) StopCoroutine(hitMarkerCoroutine);
            hitMarkerImage.SetActive(true);
            hitMarkerCoroutine = StartCoroutine(HideHitMarker());
        }
    }

    private IEnumerator HideHitMarker()
    {
        yield return new WaitForSeconds(hitMarkerDuration);
        if (hitMarkerImage != null) hitMarkerImage.SetActive(false);
        hitMarkerCoroutine = null;
    }

    // ===== PAUSE MENU =====
    private void TogglePauseMenu()
    {
        isPaused = !isPaused;
        ShowPauseMenu(isPaused);
    }

    public void ShowPauseMenu(bool show)
    {
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(show);
            isPaused = show;
            Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = show;
            Time.timeScale = show ? 0f : 1f;
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
                if (debugUpdateCoroutine != null) StopCoroutine(debugUpdateCoroutine);
                debugUpdateCoroutine = StartCoroutine(UpdateNetworkDebugInfo());
            }
            else if (debugUpdateCoroutine != null)
            {
                StopCoroutine(debugUpdateCoroutine);
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

        sb.AppendLine("<color=#FFA500><b>=== NETWORK STATISTICS ===</b></color>");

        // --- GENERAL ---
        string fps = $"{(int)(1f / Time.unscaledDeltaTime)} FPS";
        string ip = NetworkUtils.GetLocalIPAddress();
        string mode = GameManager.Instance ? GameManager.Instance.GetGameMode().ToString() : "NULL";

        sb.AppendLine($"SYS: {fps} | IP: {ip}");
        sb.AppendLine($"MODE: {mode}");

        // --- TRAFFIC ---
        sb.AppendLine("--------------------------------");
        sb.AppendLine($"| ==== TRAFFIC ==== |");
        sb.AppendLine($"Sent: <color=#00FF00>{networkStats.packetsSent}</color> | Recv: <color=#00FFFF>{networkStats.packetsReceived}</color> | Objs: {CountNetworkedObjects()}");

        // --- CLIENT ---
        var udpClient = FindObjectOfType<UDPClient>();
        if (udpClient != null)
        {
            sb.AppendLine("--------------------------------");
            sb.AppendLine("| ==== CLIENT STATUS ==== |");

            string status = udpClient.IsConnected ? "<color=green>CONNECTED</color>" : "<color=red>DISCONNECTED</color>";
            string key = string.IsNullOrEmpty(udpClient.ClientKey) ? "NULL" : udpClient.ClientKey.Substring(0, 6) + "..";

            sb.AppendLine($"State: {status}");
            sb.AppendLine($"Server: {udpClient.serverIP}:{udpClient.serverPort}");
            sb.AppendLine($"My Key: {key} | My Port: {udpClient.clientPort}");
        }

        // --- SERVER ---
        var udpServer = FindObjectOfType<UDPServer>();
        if (udpServer != null && udpServer.gameObject.activeInHierarchy)
        {
            sb.AppendLine("--------------------------------");
            sb.AppendLine("| ==== SERVER STATUS ==== |");
            sb.AppendLine($"Port: {udpServer.port} | Clients: <color=yellow>{udpServer.GetConnectedClientCount()}</color>");

            var clients = udpServer.GetConnectedClients();
            if (clients != null && clients.Count > 0)
            {
                sb.AppendLine(" -- Connected List -- ");
                foreach (var c in clients)
                {
                    string n = c.Name.Length > 8 ? c.Name.Substring(0, 8) : c.Name;
                    string k = c.ClientKey.Substring(0, 4);
                    float inactive = (System.DateTime.Now.Ticks - c.LastPacketTime) / 10000000f;
                    string pingColor = inactive > 2.0f ? "red" : "green";

                    sb.AppendLine($" > [{k}] {n,-9} | HP:{c.Health,3} | W:{c.WeaponID} | T:<color={pingColor}>{inactive:F1}s</color>");
                }
            }
        }

        // --- PLAYER ---
        if (cachedLocalPlayer != null)
        {
            sb.AppendLine("--------------------------------");
            sb.AppendLine("| ==== PLAYER STATE ==== |");

            var lc = cachedLocalPlayer.GetLifeComponent();
            float hp = lc ? lc.health : 0;
            string pos = cachedLocalPlayer.transform.position.ToString("F1");

            sb.AppendLine($"HP: {hp:F0}% | Pos: {pos}");
        }

        // --- SIMULATION ---
        if (NetworkSimulator.Instance != null && (NetworkSimulator.Instance.IsLatencyEnabled() || NetworkSimulator.Instance.IsPacketLossEnabled()))
        {
            sb.AppendLine("--------------------------------");
            sb.AppendLine("| ==== SIMULATION ==== |");

            if (NetworkSimulator.Instance.IsLatencyEnabled())
                sb.AppendLine($"Lat: <color=orange>{NetworkSimulator.Instance.GetCurrentLatency()}ms</color>");

            if (NetworkSimulator.Instance.IsPacketLossEnabled())
                sb.AppendLine($"Loss: <color=red>{NetworkSimulator.Instance.GetPacketLossRate() * 100}%</color>");
        }

        sb.AppendLine("================================");
        networkDebugText.text = sb.ToString();
    }

    private int CountNetworkedObjects()
    {
        return FindObjectsOfType<PlayerController>().Length +
               FindObjectsOfType<HealStation>().Length +
               FindObjectsOfType<WeaponStation>().Length;
    }

    // ===== DEATH & NOTIFICATIONS =====
    public void ShowDeathScreen() { if (spectatorObject) spectatorObject.SetActive(true); }
    public void HideDeathScreen() { if (spectatorObject) spectatorObject.SetActive(false); }

    private void OnRespawnClicked()
    {
        if (cachedLocalPlayer == null) cachedLocalPlayer = FindLocalPlayer();
        if (cachedLocalPlayer != null)
        {
            cachedLocalPlayer.Respawn();
            HideDeathScreen();
        }
    }

    private void OnQuitClicked()
    {
        if (GameplayManager.Instance != null) GameplayManager.Instance.Disconnect();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private PlayerController FindLocalPlayer()
    {
        var players = FindObjectsOfType<PlayerController>();
        foreach (var p in players) if (p.IsLocalPlayer) return p;
        return null;
    }

    public void ShowNotification(string msg) => ShowNotification(msg, Color.white);
    public void ShowPlayerJoined(string n = "") => ShowNotification(string.IsNullOrEmpty(n) ? "Player joined" : $"{n} joined");
    public void ShowPlayerLeft(string n = "") => ShowNotification(string.IsNullOrEmpty(n) ? "Player left" : $"{n} left");

    public void ShowNotification(string message, Color color)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
            notificationText.color = color;
            notificationText.gameObject.SetActive(true);
            if (hideNotificationCoroutine != null) StopCoroutine(hideNotificationCoroutine);
            hideNotificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
        }
    }

    private IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSeconds(notificationDuration);
        if (notificationText) notificationText.gameObject.SetActive(false);
    }

    public void ResumeGame() => ShowPauseMenu(false);
    public void Quit() => OnQuitClicked();

    void OnDestroy()
    {
        if (respawnButton) respawnButton.onClick.RemoveListener(OnRespawnClicked);
        if (quitButton) quitButton.onClick.RemoveListener(OnQuitClicked);
        if (debugUpdateCoroutine != null) StopCoroutine(debugUpdateCoroutine);
    }
}
