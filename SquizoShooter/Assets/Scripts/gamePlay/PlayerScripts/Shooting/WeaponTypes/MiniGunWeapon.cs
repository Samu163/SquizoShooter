using UnityEngine;

public class MiniGunWeapon : BaseWeapon
{
    void Awake()
    {
        WeaponID = 2;
        shootDamage = 20f;
        fireRate = 10f;
        recoilPitch = 6f;
        recoilYaw = 1.5f;
        recoilBack = 0.25f;
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
                Debug.Log("[AK47Weapon] Shot blocked by Wall at " + hit.point);
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