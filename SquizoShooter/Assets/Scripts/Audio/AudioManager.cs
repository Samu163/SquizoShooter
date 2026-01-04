using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("Movimiento")]
    public AudioClip[] pasosSuelo;
    public AudioClip salto;

    [Header("Armas (Orden ID)")]
    // Asegúrate de arrastrarlos en el mismo orden que tu WeaponManager (0:Pistola, 1:Escopeta, 2:Minigun)
    public AudioClip[] disparosArmas;

    [Header("Estado Player")]
    public AudioClip recibirDano;
    public AudioClip curarse;
    public AudioClip morir;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    public AudioClip GetRandomFootstep()
    {
        if (pasosSuelo.Length == 0) return null;
        return pasosSuelo[Random.Range(0, pasosSuelo.Length)];
    }

    public AudioClip GetWeaponShot(int weaponID)
    {
        if (weaponID >= 0 && weaponID < disparosArmas.Length)
            return disparosArmas[weaponID];
        return null;
    }
}