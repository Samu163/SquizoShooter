using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private float shootRange = 100f;
    [SerializeField] private float shootDamage = 25f;
    [SerializeField] private float fireRate = 4f;
    [SerializeField] private float recoilPitch = 5f;
    [SerializeField] private float recoilYaw = 1f;
    [SerializeField] private float recoilBack = 0.2f;

    [Header("Gizmos (editor)")]
    [SerializeField] private bool showShootGizmos = true;
    [SerializeField] private Color gizmoLineColor = Color.red;
    [SerializeField] private Color gizmoHitColor = Color.yellow;
    [SerializeField] private float gizmoHitRadius = 0.25f;

    private PlayerController playerController;
    private PlayerSync playerSync;
    private PlayerCamera playerCamera;
    private float lastFireTime = 0f;

    public void Initialize(PlayerController pc, PlayerSync sync, PlayerCamera cam)
    {
        playerController = pc;
        playerSync = sync;
        playerCamera = cam;
    }

    public void TryShoot()
    {
        float cooldown = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastFireTime < cooldown) return;
        lastFireTime = Time.time;

        Shoot();
        ApplyRecoil();
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

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Block shot if hits a wall
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                Debug.Log("[PlayerShooting] Shot blocked by Wall at " + hit.point);
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

                if (udpClient == null)
                {
                    Debug.LogWarning("[PlayerShooting] No UDPClient to send SHOT.");
                    return;
                }

                string targetKey = udpClient.GetKeyForGameObject(hitPlayerGO);
                if (string.IsNullOrEmpty(targetKey))
                {
                    Debug.LogWarning("[PlayerShooting] Shot: no key for hit GameObject.");
                    return;
                }

                // Avoid self-damage
                if (udpClient.ClientKey == targetKey)
                {
                    Debug.Log("[PlayerShooting] Shot ignored: self-hit.");
                    return;
                }

                // Send shot to server
                udpClient.SendShotToServer(targetKey, shootDamage);
                return;
            }
        }
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

    void OnDrawGizmos()
    {
        if (!showShootGizmos) return;
        if (Application.isPlaying && playerController != null && !playerController.IsLocalPlayer) return;

        Transform camTransform = playerCamera != null ? playerCamera.CameraTransform : null;
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
    }
}