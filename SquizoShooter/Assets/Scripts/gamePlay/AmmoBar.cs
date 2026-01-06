using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoBarUI : MonoBehaviour
{
    public static AmmoBarUI instance;

    [Header("UI")]
    public Image imagenRelleno;
    public TextMeshProUGUI textoAmmo;

    [Header("Auto-binding")]
    [Tooltip("If true, AmmoBar will auto-find the local player's WeaponManager and drive itself.")]
    public bool autoBindToLocalPlayer = true;

    private WeaponManager weaponManager;
    private PlayerController localPlayer;

    // Cache to avoid redundant UI updates
    private int lastShownAmmo = int.MinValue;
    private int lastShownMax = int.MinValue;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        TryBindToLocalPlayer();
        ForceRefresh(); // initial
    }

    void LateUpdate()
    {
        if (!autoBindToLocalPlayer) return;

        if (weaponManager == null || localPlayer == null)
        {
            // Try to (re)bind if references were lost
            TryBindToLocalPlayer();
        }

        if (weaponManager == null || localPlayer == null || !localPlayer.IsLocalPlayer) return;

        var weapon = weaponManager.GetCurrentWeapon();
        if (weapon == null)
        {
            SafeUpdateUI(0, 1);
            return;
        }

        int current = weapon.GetCurrentAmmo();
        int max = weapon.GetMaxAmmo();

        // Update only when values change
        if (current != lastShownAmmo || max != lastShownMax)
        {
            SafeUpdateUI(current, max);
        }
    }

    private void TryBindToLocalPlayer()
    {
        // Find any PlayerController marked as local
        var players = GameObject.FindObjectsOfType<PlayerController>();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsLocalPlayer)
            {
                localPlayer = players[i];
                break;
            }
        }

        if (localPlayer != null)
        {
            // WeaponManager could be on the player or a child
            weaponManager = localPlayer.GetComponentInChildren<WeaponManager>();
        }
    }

    public void UpdateUI(float ammoActual, float ammoMaximo)
    {
        SafeUpdateUI(ammoActual, ammoMaximo);
    }

    // Preferred manual API: push directly from a weapon reference
    public void UpdateFromWeapon(BaseWeapon weapon)
    {
        if (weapon == null)
        {
            SafeUpdateUI(0, 1);
            return;
        }
        SafeUpdateUI(weapon.GetCurrentAmmo(), weapon.GetMaxAmmo());
    }

    public void ForceRefresh()
    {
        // Force the next LateUpdate to redraw by invalidating cache
        lastShownAmmo = int.MinValue;
        lastShownMax = int.MinValue;

        // Also perform an immediate refresh if we have a weaponManager
        if (weaponManager != null && localPlayer != null && localPlayer.IsLocalPlayer)
        {
            var weapon = weaponManager.GetCurrentWeapon();
            if (weapon != null)
            {
                SafeUpdateUI(weapon.GetCurrentAmmo(), weapon.GetMaxAmmo());
            }
            else
            {
                SafeUpdateUI(0, 1);
            }
        }
    }

    // Centralized, safe UI update with sanitization and caching
    private void SafeUpdateUI(float ammoActual, float ammoMaximo)
    {
        // Sanitize
        if (ammoMaximo <= 0f) ammoMaximo = 1f;
        ammoActual = Mathf.Clamp(ammoActual, 0f, ammoMaximo);

        float fill = ammoActual / ammoMaximo;
        if (float.IsNaN(fill) || float.IsInfinity(fill)) fill = 0f;

        // Apply
        if (imagenRelleno != null)
        {
            imagenRelleno.fillAmount = Mathf.Clamp01(fill);
        }

        if (textoAmmo != null)
        {
            textoAmmo.text = Mathf.RoundToInt(ammoActual).ToString();
        }

        // Cache
        lastShownAmmo = Mathf.RoundToInt(ammoActual);
        lastShownMax = Mathf.RoundToInt(ammoMaximo);
    }
}
