using UnityEngine;
using System.Linq; // Necesario para ordenar si usas Array.Sort o Linq

public class PistolWeapon : BaseWeapon
{
    void Awake()
    {
        WeaponID = 1;

        // Stats
        shootDamage = 25f;
        fireRate = 4f;
        recoilPitch = 3f;
        recoilYaw = 0.5f;
        recoilBack = 0.15f;
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