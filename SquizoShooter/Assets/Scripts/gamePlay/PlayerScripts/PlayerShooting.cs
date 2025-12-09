using UnityEngine;
using System.Collections.Generic;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting (base defaults)")]
    [SerializeField] private float shootRange = 100f;
    [SerializeField] private float shootDamage = 25f;
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private float recoilPitch = 5f;
    [SerializeField] private float recoilYaw = 1f;
    [SerializeField] private float recoilBack = 0.2f;

    [Header("Weapon Selection")]
    [Tooltip("1 = Pistol, 2 = AK-47, 3 = Shotgun")]
    [SerializeField] private int currentWeaponId = 1;

    [Header("Shotgun Settings")]
    [SerializeField] private int shotgunPellets = 6;
    [SerializeField] private float shotgunSpreadAngle = 6f;
    [SerializeField] private float shotgunPerPelletDamage = 9f;

    [Header("Gizmos (editor)")]
    [SerializeField] private bool showShootGizmos = true;
    [SerializeField] private Color gizmoLineColor = Color.red;
    [SerializeField] private Color gizmoHitColor = Color.yellow;
    [SerializeField] private float gizmoHitRadius = 0.25f;
    [SerializeField] private float gizmoPersistSeconds = 0.6f; // NEW: how long to keep shot gizmos

    private PlayerController playerController;
    private PlayerSync playerSync;
    private PlayerCamera playerCamera;
    private float lastFireTime = 0f;

    [Header("Gun models")]
    [SerializeField] private GameObject pistolModel;
    [SerializeField] private GameObject ak47Model;
    [SerializeField] private GameObject shotgunModel;

    // NEW: shot gizmo history
    private struct ShotGizmo
    {
        public Vector3 origin;
        public Vector3 end;
        public bool hasHit;
        public Vector3 hitPoint;
        public float expireTime;
        public Color lineColor;
        public Color markerColor;
    }
    private readonly List<ShotGizmo> shotGizmos = new List<ShotGizmo>(64);

    
    public void UpdateWeaponModelPistol()
    {
        if (pistolModel != null)
            pistolModel.SetActive(true);
        ak47Model.SetActive(false);
        shotgunModel.SetActive(false);

     
    }
    public void UpdateWeaponModelAK47()
    {
        if (ak47Model != null)
            ak47Model.SetActive(true);
        pistolModel.SetActive(false);
        shotgunModel.SetActive(false);
    }
    public void UpdateWeaponModelShotgun()
    {
        if (shotgunModel != null)
            shotgunModel.SetActive(true);
        pistolModel.SetActive(false);
        ak47Model.SetActive(false);
    }

    public void Initialize(PlayerController pc, PlayerSync sync, PlayerCamera cam)
    {
        playerController = pc;
        playerSync = sync;
        playerCamera = cam;
    }
    public void SetCurrentWeapon(int weaponId)
    {
        currentWeaponId = weaponId;
    }

    public void TryShoot()
    {
        // Apply weapon-specific stats
        ApplyWeaponStats(currentWeaponId);

        float cooldown = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastFireTime < cooldown) return;
        lastFireTime = Time.time;

        if (currentWeaponId == 3)
        {
            ShootShotgun();
        }
        else
        {
            Shoot();
        }

        ApplyRecoil();
    }

    private void ApplyWeaponStats(int weaponId)
    {
        // Switch-case to select weapon stats
        switch (weaponId)
        {
            case 1: // Pistol
                shootDamage = 25f;
                fireRate = 4f;
                recoilPitch = 3f;
                recoilYaw = 0.5f;
                recoilBack = 0.15f;
                UpdateWeaponModelPistol();
                break;
            case 2: // AK-47
                shootDamage = 20f;
                fireRate = 10f;
                recoilPitch = 6f;
                recoilYaw = 1.5f;
                recoilBack = 0.25f;
                UpdateWeaponModelAK47();
                break;
            case 3: // Shotgun
                // Shotgun uses per-pellet damage; shootDamage not used by pellets
                fireRate = 1.2f;
                recoilPitch = 8f;
                recoilYaw = 2.2f;
                recoilBack = 0.35f;
                UpdateWeaponModelShotgun();
                break;
            default:
                UpdateWeaponModelPistol();
                break;
        }
    }

    void Shoot()
    {
        Transform cameraTransform = playerCamera != null ? playerCamera.CameraTransform : null;
        if (cameraTransform == null) return;

        // Local animation
        PlayerVisuals visuals = playerController.GetVisuals();
        if (visuals != null)
        {
            visuals.PlayShootAnimation();
        }

        // Notify others about the shoot animation
        UDPClient udpClient = playerSync.GetUDPClient();
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.SendShootAnim();
        }

        Vector3 origin = cameraTransform.position;
        Vector3 dir = cameraTransform.forward;

        Ray ray = new Ray(origin, dir);
        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);
        if (hits == null || hits.Length == 0)
        {
            // record straight line when no hit
            RecordShotGizmo(origin, origin + dir * shootRange, false, Vector3.zero);
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Block shot if hits a wall
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                Debug.Log("[PlayerShooting] Shot blocked by Wall at " + hit.point);
                RecordShotGizmo(origin, hit.point, true, hit.point, lineColorOverride: gizmoLineColor, markerColorOverride: Color.black);
                return;
            }

            // Search for player in hierarchy
            Transform t = hit.collider.transform;
            while (t != null && !t.CompareTag("Player"))
            {
                t = t.parent;
            }

            if (t != null)
            {
                GameObject hitPlayerGO = t.gameObject;

                UDPClient udpClient2 = playerSync.GetUDPClient();
                if (udpClient2 == null)
                {
                    Debug.LogWarning("[PlayerShooting] No UDPClient to send SHOT.");
                    RecordShotGizmo(origin, hit.point, true, hit.point);
                    return;
                }

                string targetKey = udpClient2.GetKeyForGameObject(hitPlayerGO);
                if (string.IsNullOrEmpty(targetKey))
                {
                    Debug.LogWarning("[PlayerShooting] Shot: no key for hit GameObject.");
                    RecordShotGizmo(origin, hit.point, true, hit.point);
                    return;
                }

                // Avoid self-damage
                if (udpClient2.ClientKey == targetKey)
                {
                    Debug.Log("[PlayerShooting] Shot ignored: self-hit.");
                    RecordShotGizmo(origin, hit.point, true, hit.point);
                    return;
                }

                // Send shot to server
                udpClient2.SendShotToServer(targetKey, shootDamage);
                RecordShotGizmo(origin, hit.point, true, hit.point);
                return;
            }
        }

        // If we sorted hits and found none matching Wall/Player, just draw to max range
        RecordShotGizmo(origin, origin + dir * shootRange, false, Vector3.zero);
    }

    void ShootShotgun()
    {
        Transform cameraTransform = playerCamera != null ? playerCamera.CameraTransform : null;
        if (cameraTransform == null) return;

        // Local animation
        PlayerVisuals visuals = playerController.GetVisuals();
        if (visuals != null)
        {
            visuals.PlayShootAnimation();
        }

        // Notify others about the shoot animation
        UDPClient udpClient = playerSync.GetUDPClient();
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.SendShootAnim();
        }

        Vector3 origin = cameraTransform.position;

        // Fire multiple pellets with spread
        for (int i = 0; i < shotgunPellets; i++)
        {
            Vector3 dir = ApplySpread(cameraTransform.forward, shotgunSpreadAngle);
            Ray ray = new Ray(origin, dir);
            RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);
            if (hits == null || hits.Length == 0)
            {
                RecordShotGizmo(origin, origin + dir * shootRange, false, Vector3.zero);
                continue;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool pelletConsumed = false;
            foreach (var hit in hits)
            {
                // Block pellet if hits a wall
                if (hit.collider != null && hit.collider.CompareTag("Wall"))
                {
                    Debug.Log("[PlayerShooting] Shotgun pellet blocked by Wall at " + hit.point);
                    RecordShotGizmo(origin, hit.point, true, hit.point, lineColorOverride: gizmoLineColor, markerColorOverride: Color.black);
                    pelletConsumed = true;
                    break; // pellet stops here
                }

                // Search for player in hierarchy
                Transform t = hit.collider.transform;
                while (t != null && !t.CompareTag("Player"))
                {
                    t = t.parent;
                }

                if (t != null)
                {
                    GameObject hitPlayerGO = t.gameObject;

                    UDPClient udpClient2 = playerSync.GetUDPClient();
                    if (udpClient2 == null)
                    {
                        Debug.LogWarning("[PlayerShooting] No UDPClient to send SHOT.");
                        RecordShotGizmo(origin, hit.point, true, hit.point);
                        pelletConsumed = true;
                        break;
                    }

                    string targetKey = udpClient2.GetKeyForGameObject(hitPlayerGO);
                    if (string.IsNullOrEmpty(targetKey))
                    {
                        Debug.LogWarning("[PlayerShooting] Shot: no key for hit GameObject.");
                        RecordShotGizmo(origin, hit.point, true, hit.point);
                        pelletConsumed = true;
                        break;
                    }

                    // Avoid self-damage
                    if (udpClient2.ClientKey == targetKey)
                    {
                        Debug.Log("[PlayerShooting] Shot ignored: self-hit.");
                        RecordShotGizmo(origin, hit.point, true, hit.point);
                        pelletConsumed = true;
                        break;
                    }

                    // Send pellet damage to server
                    udpClient2.SendShotToServer(targetKey, shotgunPerPelletDamage);
                    RecordShotGizmo(origin, hit.point, true, hit.point);
                    pelletConsumed = true; // pellet consumed on first player hit
                    break;
                }
            }

            if (!pelletConsumed)
            {
                // no relevant hits; draw to max range
                RecordShotGizmo(origin, origin + dir * shootRange, false, Vector3.zero);
            }
        }
    }

    Vector3 ApplySpread(Vector3 forward, float angleDeg)
    {
        // Random small rotation within a cone defined by angleDeg
        float yaw = Random.Range(-angleDeg, angleDeg);
        float pitch = Random.Range(-angleDeg, angleDeg);
        Quaternion spreadRot = Quaternion.Euler(pitch, yaw, 0f);
        return spreadRot * forward;
    }

    void ApplyRecoil()
    {
        if (playerCamera != null)
        {
            playerCamera.ApplyRecoil(recoilPitch, recoilYaw);
        }

        // Apply physical recoil through movement component
        PlayerMovement movement = playerController.GetMovement();
        if (movement != null)
        {
            movement.ApplyPhysicalRecoil(recoilBack);
        }
    }

    // NEW: record shot gizmo data to draw over time
    private void RecordShotGizmo(Vector3 origin, Vector3 end, bool hasHit, Vector3 hitPoint, Color? lineColorOverride = null, Color? markerColorOverride = null)
    {
        if (!showShootGizmos) return;

        ShotGizmo gizmo = new ShotGizmo
        {
            origin = origin,
            end = end,
            hasHit = hasHit,
            hitPoint = hitPoint,
            expireTime = Time.time + gizmoPersistSeconds,
            lineColor = lineColorOverride ?? gizmoLineColor,
            markerColor = markerColorOverride ?? gizmoHitColor
        };
        shotGizmos.Add(gizmo);

        // Optional: prevent unbounded growth
        if (shotGizmos.Count > 256)
        {
            shotGizmos.RemoveRange(0, shotGizmos.Count - 256);
        }
    }

    void OnDrawGizmos()
    {
        if (!showShootGizmos) return;
        if (Application.isPlaying && playerController != null && !playerController.IsLocalPlayer) return;

        // When not playing, draw the instantaneous preview ray as before
        Transform camTransform = playerCamera != null ? playerCamera.CameraTransform : null;
        if (!Application.isPlaying)
        {
            if (camTransform == null) return;

            Vector3 origin = camTransform.position;
            Vector3 dir = camTransform.forward;

            RaycastHit[] hits = Physics.RaycastAll(origin, dir, shootRange);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.collider != null && hit.collider.CompareTag("Wall"))
                    {
                        Gizmos.color = gizmoLineColor;
                        Gizmos.DrawLine(origin, hit.point);
                        Gizmos.color = Color.black;
                        Gizmos.DrawWireSphere(hit.point, gizmoHitRadius);
                        return;
                    }

                    Transform t = hit.collider.transform;
                    while (t != null && !t.CompareTag("Player"))
                        t = t.parent;
                    if (t != null)
                    {
                        Gizmos.color = gizmoLineColor;
                        Gizmos.DrawLine(origin, hit.point);
                        Gizmos.color = gizmoHitColor;
                        Gizmos.DrawWireSphere(hit.point, gizmoHitRadius);
                        return;
                    }
                }
            }

            Gizmos.color = gizmoLineColor;
            Gizmos.DrawLine(origin, origin + dir * shootRange);
            return;
        }

        // Playing: draw persisted shot gizmos
        float now = Time.time;
        // Clean up expired first
        for (int i = shotGizmos.Count - 1; i >= 0; i--)
        {
            if (shotGizmos[i].expireTime <= now)
                shotGizmos.RemoveAt(i);
        }

        // Draw all remaining
        for (int i = 0; i < shotGizmos.Count; i++)
        {
            var g = shotGizmos[i];
            Gizmos.color = g.lineColor;
            Gizmos.DrawLine(g.origin, g.end);
            if (g.hasHit)
            {
                Gizmos.color = g.markerColor;
                Gizmos.DrawWireSphere(g.hitPoint, gizmoHitRadius);
            }
        }
    }
}