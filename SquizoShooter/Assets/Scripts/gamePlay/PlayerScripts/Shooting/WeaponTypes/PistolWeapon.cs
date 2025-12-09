using UnityEngine;

public class PistolWeapon : BaseWeapon
{
    void Awake()
    {
        WeaponID = 1;

        // Pistol stats
        shootDamage = 25f;
        fireRate = 4f;
        recoilPitch = 3f;
        recoilYaw = 0.5f;
        recoilBack = 0.15f;
    }

    protected override void Shoot()
    {
        Transform cameraTransform = playerCamera?.CameraTransform;
        if (cameraTransform == null) return;

        PlayShootAnimation();

        Vector3 origin = cameraTransform.position;
        Vector3 dir = cameraTransform.forward;

        Ray ray = new Ray(origin, dir);
        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);

        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Block shot if hits a wall
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                Debug.Log("[PistolWeapon] Shot blocked by Wall at " + hit.point);
                return;
            }

            // Check for player hit
            string targetKey = GetTargetKeyFromHit(hit);
            if (!string.IsNullOrEmpty(targetKey))
            {
                SendDamageToServer(targetKey, shootDamage);
                return;
            }
        }
    }
}