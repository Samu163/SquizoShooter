using UnityEngine;

public class MiniGunWeapon : BaseWeapon
{
    void Awake()
    {
        WeaponID = 2;

        // Stats 
        shootDamage = 20f;
        fireRate = 10f; 
        recoilPitch = 6f;
        recoilYaw = 1.5f;
        recoilBack = 0.25f;
    }

    protected override void OnShootLogic(Transform origin, UDPClient client, string myKey)
    {

        Ray ray = new Ray(origin.position, origin.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);

        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Wall"))
            {
                return;
            }

            Transform t = hit.collider.transform;
            while (t != null && !t.CompareTag("Player"))
            {
                t = t.parent;
            }

            if (t != null && client != null)
            {
                string targetKey = client.GetKeyForGameObject(t.gameObject);

                if (!string.IsNullOrEmpty(targetKey) && targetKey != myKey)
                {
                    SendDamage(client, targetKey, shootDamage);
                    return;
                }
            }
        }
    }
}
}