using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    [Header("Fuentes de Audio")]
    public AudioSource shortRangeSource;
    public AudioSource longRangeSource;

    [Header("Configuración de Distancias")]
    public float distanciaCorta = 30f;
    public float distanciaLarga = 100f;

    [Header("Ajuste Curva Disparos")]
    public float minDistanceDisparos = 2f;

    [Header("Configuración Pasos")]
    public LayerMask capasSuelo;
    public float distanciaRayoSuelo = 1.2f;
    public float velocidadMinimaParaPasos = 0.5f;
    public float intervaloCaminar = 0.5f;
    public float intervaloCorrer = 0.3f;

    [Header("Configuración Volumen")]
    public float volumenPasos = 0.6f;
    public float volumenDisparo = 1f;
    public float volumenGritos = 0.8f;

    private float _timerPasos;
    private Vector3 _posicionAnterior;
    private float _velocidadSuavizada;

    void Start()
    {
        if (shortRangeSource == null || longRangeSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length >= 1) shortRangeSource = sources[0];
            if (sources.Length >= 2) longRangeSource = sources[1];
            if (longRangeSource == null) longRangeSource = shortRangeSource;
        }


        ConfigurarSource(shortRangeSource, 2f, distanciaCorta, AudioRolloffMode.Linear);

        ConfigurarSource(longRangeSource, minDistanceDisparos, distanciaLarga, AudioRolloffMode.Logarithmic);

        _posicionAnterior = transform.position;
    }

    void ConfigurarSource(AudioSource source, float minDest, float maxDist, AudioRolloffMode mode)
    {
        if (source != null)
        {
            source.spatialBlend = 1.0f;
            source.minDistance = minDest;
            source.maxDistance = maxDist;
            source.rolloffMode = mode;
            source.dopplerLevel = 0f; 
        }
    }

    void OnEnable()
    {
        _posicionAnterior = transform.position;
        _velocidadSuavizada = 0f;
        _timerPasos = 0f;
    }

    void Update()
    {
        if (Time.deltaTime < 0.001f) return;
        DetectarMovimientoYPasos();
    }

    void DetectarMovimientoYPasos()
    {
        float distanciaMovida = Vector3.Distance(transform.position, _posicionAnterior);
        float velocidadInstantanea = 0f;
        if (Time.deltaTime > 0f) velocidadInstantanea = distanciaMovida / Time.deltaTime;

        _velocidadSuavizada = Mathf.Lerp(_velocidadSuavizada, velocidadInstantanea, Time.deltaTime * 5f);
        if (float.IsNaN(_velocidadSuavizada)) _velocidadSuavizada = 0f;

        bool estaEnSuelo = Physics.Raycast(transform.position, Vector3.down, distanciaRayoSuelo, capasSuelo);

        if (_velocidadSuavizada > velocidadMinimaParaPasos && estaEnSuelo)
        {
            float intervaloActual = _velocidadSuavizada > 6f ? intervaloCorrer : intervaloCaminar;
            _timerPasos += Time.deltaTime;

            if (_timerPasos >= intervaloActual)
            {
                ReproducirPaso();
                _timerPasos = 0f;
            }
        }
        else
        {
            _timerPasos = intervaloCaminar * 0.9f;
        }

        _posicionAnterior = transform.position;
    }

    void ReproducirPaso()
    {
        PlaySound(shortRangeSource, AudioManager.instance?.GetRandomFootstep(), volumenPasos, true);
    }

    public void PlayJump()
    {
        if (AudioManager.instance)
            PlaySound(shortRangeSource, AudioManager.instance.salto, 0.7f);
    }

    public void PlayShoot(int weaponID)
    {
        if (AudioManager.instance)
        {
            AudioClip clip = AudioManager.instance.GetWeaponShot(weaponID);
            // Variación ligera para disparos
            if (longRangeSource != null) longRangeSource.pitch = Random.Range(0.95f, 1.05f);

            PlaySound(longRangeSource, clip, volumenDisparo);
        }
    }

    public void PlayDamage()
    {
        if (AudioManager.instance)
            PlaySound(shortRangeSource, AudioManager.instance.recibirDano, volumenGritos);
    }

    public void PlayHeal()
    {
        if (AudioManager.instance)
            PlaySound(shortRangeSource, AudioManager.instance.curarse, volumenGritos);
    }

    public void PlayDeath()
    {
        if (AudioManager.instance && AudioManager.instance.morir)
        {
            PlayClipAtPointCustom(AudioManager.instance.morir, transform.position, 1f, distanciaCorta);
        }
    }

    private void PlaySound(AudioSource source, AudioClip clip, float vol, bool randomPitch = false)
    {
        if (source != null && clip != null)
        {
            if (randomPitch) source.pitch = Random.Range(0.9f, 1.1f);
            else source.pitch = 1f;

            source.PlayOneShot(clip, vol);
        }
    }

    private void PlayClipAtPointCustom(AudioClip clip, Vector3 pos, float volume, float maxDistance)
    {
        GameObject tempGO = new GameObject("TempAudio_Death");
        tempGO.transform.position = pos;

        AudioSource aSource = tempGO.AddComponent<AudioSource>();
        aSource.clip = clip;
        aSource.volume = volume;
        aSource.spatialBlend = 1f;
        aSource.rolloffMode = AudioRolloffMode.Linear; 
        aSource.minDistance = 2f;
        aSource.maxDistance = maxDistance;

        aSource.Play();
        Destroy(tempGO, clip.length + 0.1f);
    }
}