using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    [Header("Componentes")]
    public AudioSource audioSource;

    [Header("Configuración Pasos")]
    public LayerMask capasSuelo;
    public float distanciaRayoSuelo = 1.2f;
    public float velocidadMinimaParaPasos = 0.1f;
    public float intervaloCaminar = 0.5f;
    public float intervaloCorrer = 0.3f;

    [Header("Configuración Volumen")]
    public float volumenPasos = 0.6f;
    public float volumenDisparo = 1f;
    public float volumenGritos = 0.8f;

    private float _timerPasos;
    private Vector3 _posicionAnterior;
    private float _velocidadActual;

    void Start()
    {
        _posicionAnterior = transform.position;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        DetectarMovimientoYPasos();
    }

    void DetectarMovimientoYPasos()
    {
        // 1. Calcular velocidad
        float distanciaMovida = Vector3.Distance(transform.position, _posicionAnterior);
        _velocidadActual = distanciaMovida / Time.deltaTime;

        // 2. Comprobar suelo (Solo para saber si reproducir pasos)
        bool estaEnSuelo = Physics.Raycast(transform.position, Vector3.down, distanciaRayoSuelo, capasSuelo);

        // 3. Lógica de Pasos
        if (_velocidadActual > velocidadMinimaParaPasos && estaEnSuelo)
        {
            // Asumiendo que sprintSpeed es aprox 8, ponemos el corte en 6
            float intervaloActual = _velocidadActual > 6f ? intervaloCorrer : intervaloCaminar;
            _timerPasos += Time.deltaTime;

            if (_timerPasos >= intervaloActual)
            {
                ReproducirPaso();
                _timerPasos = 0f;
            }
        }
        else
        {
            _timerPasos = intervaloCaminar;
        }

        _posicionAnterior = transform.position;
    }

    void ReproducirPaso()
    {
        if (AudioManager.instance == null) return;
        AudioClip clip = AudioManager.instance.GetRandomFootstep();

        if (clip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip, volumenPasos);
        }
    }

    // --- FUNCIONES PÚBLICAS ---

    public void PlayJump() // Solo salto normal
    {
        if (AudioManager.instance && AudioManager.instance.salto)
            PlaySound(AudioManager.instance.salto, 0.7f);
    }

    public void PlayShoot(int weaponID)
    {
        if (AudioManager.instance)
        {
            AudioClip clip = AudioManager.instance.GetWeaponShot(weaponID);
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(clip, volumenDisparo);
            }
        }
    }

    public void PlayDamage()
    {
        if (AudioManager.instance && AudioManager.instance.recibirDano)
            PlaySound(AudioManager.instance.recibirDano, volumenGritos);
    }

    public void PlayHeal()
    {
        if (AudioManager.instance && AudioManager.instance.curarse)
            PlaySound(AudioManager.instance.curarse, volumenGritos);
    }

    public void PlayDeath()
    {
        if (AudioManager.instance != null && AudioManager.instance.morir != null)
        {
            // ERROR COMÚN: No uses 'audioSource.PlayOneShot' aquí.
            // SOLUCIÓN: Usar AudioSource.PlayClipAtPoint.

            // Esto crea un objeto "fantasma" temporal en la escena. 
            // Como es un objeto nuevo, NO está desactivado y el sonido sí suena.
            AudioSource.PlayClipAtPoint(AudioManager.instance.morir, transform.position, 1f);
        }
    }

    private void PlaySound(AudioClip clip, float vol)
    {
        audioSource.PlayOneShot(clip, vol);
    }
}