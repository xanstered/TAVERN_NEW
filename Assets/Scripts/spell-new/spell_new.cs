using UnityEngine;
using System.Collections;
using FMODUnity;
using FMOD.Studio;

public class spell_new : MonoBehaviour
{
    public GameObject chargeChild;
    public GameObject chargedChild;
    public GameObject groundChild;

    public float chargedChildActivationDelay = 0.5f;
    public float destroyDelay = 2f;
    public float colorChangeDelay = 2f;
    public float colorChangeDuration = 2f;

    // KONFIGURACJA AUDIO
    [Header("Audio Settings")]
    public EventReference spellImpactEvent;
    public float maxAudibleDistance = 50f; // Maksymalna odległość słyszalności

    private int environmentLayer;
    private bool hasHitGround = false;
    private Rigidbody rb;
    private AudioSystem audioSystem;
    private ParticleSystem chargePs;
    private ParticleSystem chargedPs;
    private Transform playerTransform;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSystem = FindObjectOfType<AudioSystem>();

        // POBIERANIE WARSTWY ENVIRONMENT
        environmentLayer = LayerMask.NameToLayer("Environment");

        if (environmentLayer == -1)
        {
            Debug.LogWarning("POMOCNIK: Warstwa 'Environment' nie istnieje. Używam domyślnego sprawdzania po tagu 'ground'.");
        }

        if (chargeChild != null)
            chargePs = chargeChild.GetComponent<ParticleSystem>();
        if (chargedChild != null)
            chargedPs = chargedChild.GetComponent<ParticleSystem>();

        if (groundChild != null)
            groundChild.SetActive(false);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHitGround) return;

        // SPRAWDZANIE WARSTWY LUB TAGU JAKO FALLBACK
        bool isEnvironment = false;

        if (environmentLayer != -1)
        {
            // Jeśli warstwa Environment istnieje, sprawdź ją
            isEnvironment = (collision.gameObject.layer == environmentLayer);
        }
        else
        {
            // Fallback: sprawdź tag "ground"
            isEnvironment = collision.gameObject.CompareTag("ground");
        }

        if (isEnvironment)
        {
            Debug.Log("Spell hit the ground.");
            hasHitGround = true;

            // ZATRZYMANIE WSZYSTKICH SYSTEMÓW CZĄSTECZEK
            ParticleSystem[] allParticleSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in allParticleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // AKTYWACJA EFEKTU NAZIEMNEGO
            if (groundChild != null)
                groundChild.SetActive(true);

            // ZATRZYMANIE FIZYKI
            if (rb != null)
            {
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // ODTWORZENIE DŹWIĘKU UDERZENIA
            if (audioSystem != null)
            {
                audioSystem.SpellImpactSound(transform.position);
            }

            // URUCHOMIENIE KORUTYNY ZNIKANIA
            StartCoroutine(ChangeParticleColorAndDestroy(groundChild));
        }
    }

    private IEnumerator ChangeParticleColorAndDestroy(GameObject parent)
    {
        ParticleSystem[] particleSystems = parent.GetComponentsInChildren<ParticleSystem>();

        yield return new WaitForSeconds(colorChangeDelay);

        float elapsedTime = 0f;

        while (elapsedTime < colorChangeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / colorChangeDuration;

            foreach (ParticleSystem ps in particleSystems)
            {
                var mainModule = ps.main;

                // POBIERZ AKTUALNY KOLOR
                Color startCol = mainModule.startColor.color;

                // ZDEFINIUJ KOLOR KOŃCOWY (PRZEZROCZYSTY)
                Color targetCol = new Color(startCol.r, startCol.g, startCol.b, 0f);

                // INTERPOLUJ DO PRZEZROCZYSTOŚCI
                mainModule.startColor = Color.Lerp(startCol, targetCol, t);
            }

            yield return null;
        }

        Destroy(gameObject, destroyDelay);
    }
}