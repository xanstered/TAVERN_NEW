using UnityEngine;
using FMOD.Studio;
using FMODUnity;
using System.Collections;

public class spell_cast_new : MonoBehaviour
{
    // --- KONFIGURACJA ZAKLĘCIA (WIZUALNA I FIZYCZNA) ---
    public float chargeTime = 0.5f;
    public GameObject spellPrefab;
    public Transform spellSpawnPoint;
    public float spellShootForce = 20f;
    public Color chargeStartColor = Color.black;
    public Color chargeEndColor = new Color(1f, 0.6235f, 0f);
    public string chargedChildName = "ChargedFX";
    public AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // --- ZMIENNE PRYWATNE (STAN) ---
    private float chargeTimer = 0f;
    private float scaleValue = 0f;
    private SpellState currentState = SpellState.Idle;

    // Referencje do obiektu pocisku
    private GameObject currentSpellInstance;
    private Rigidbody currentSpellRb;
    private Transform currentVfxTransform;
    private ParticleSystem currentParticleSystem;
    private GameObject chargedChildInstance;

    // --- INTEGRACJA AUDIO FMOD ---
    private AudioSystem audioSystem;
    private PLAYBACK_STATE audioPbState;
    private const string STOPPED_STATE = "STOPPED";
    private bool isAudioInitialized = false;

    enum SpellState
    {
        Idle,
        Charging,
        Holding
    }

    void Awake()
    {
        // 1. POBIERANIE AUDIO SYSTEM
        audioSystem = FindObjectOfType<AudioSystem>();

        if (audioSystem == null)
        {
            Debug.LogWarning("POMOCNIK: Wymagany skrypt AudioSystem nie został znaleziony na scenie. Kontynuuję bez audio.");
            return;
        }

        // 2. TWORZENIE INSTANCJI DŹWIĘKU 
        if (!string.IsNullOrEmpty(audioSystem.spellEvent.Path))
        {
            audioSystem.SpellSound = RuntimeManager.CreateInstance(audioSystem.spellEvent);
            isAudioInitialized = true;
        }
        else
        {
            Debug.LogWarning("POMOCNIK: Brak przypisanego FMOD Eventu dla zaklęcia lub Event jest niepoprawny. Kontynuuję bez audio.");
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case SpellState.Idle:
                HandleIdleState();
                break;
            case SpellState.Charging:
                HandleChargingState();
                break;
            case SpellState.Holding:
                HandleHoldingState();
                break;
        }

        // Aktualizacja atrybutów 3D dla dźwięku
        if (isAudioInitialized && audioSystem.SpellSound.isValid())
        {
            audioSystem.SpellSound.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(spellSpawnPoint != null ? spellSpawnPoint : transform));
        }
    }

    void HandleIdleState()
    {
        if (Input.GetMouseButtonDown(0))
        {
            chargeTimer = 0f;
            currentState = SpellState.Charging;
            SpawnSpell();

            if (isAudioInitialized)
                audioSystem.SpellCast();
        }
    }

    void HandleChargingState()
    {
        if (Input.GetMouseButton(0))
        {
            chargeTimer += Time.deltaTime;
            FollowSpawnPoint();
            float t = Mathf.Clamp01(chargeTimer / chargeTime);
            scaleValue = chargeCurve.Evaluate(t);
            UpdateChargingVfx(t);

            if (chargeTimer >= chargeTime && currentState != SpellState.Holding)
            {
                currentState = SpellState.Holding;
                SetFullyChargedVfx();
            }
        }

        // PUSZCZENIE LPM - WYSTRZAŁ LUB ANULOWANIE
        if (Input.GetMouseButtonUp(0))
        {
            if (chargeTimer >= chargeTime)
            {
                FireSpell("released at end of CHARGING");
                if (isAudioInitialized)
                    audioSystem.SpellRelease();
            }
            else
            {
                CancelSpell();
                if (isAudioInitialized)
                    audioSystem.SpellCancel();
            }

            chargeTimer = 0f;
            currentState = SpellState.Idle;
        }

        // PPM - ANULOWANIE
        if (Input.GetMouseButtonDown(1))
        {
            CancelSpell();

            if (isAudioInitialized)
                audioSystem.SpellCancel();

            chargeTimer = 0f;
            currentState = SpellState.Idle;
        }
    }

    void HandleHoldingState()
    {
        FollowSpawnPoint();
        scaleValue = 1f;

        // PUSZCZENIE LPM - WYSTRZAŁ
        if (Input.GetMouseButtonUp(0))
        {
            FireSpell("released during HOLDING");

            if (isAudioInitialized)
                audioSystem.SpellRelease();

            chargeTimer = 0f;
            currentState = SpellState.Idle;
        }

        // PPM - ANULOWANIE
        if (Input.GetMouseButtonDown(1))
        {
            CancelSpell();

            if (isAudioInitialized)
                audioSystem.SpellCancel();

            chargeTimer = 0f;
            currentState = SpellState.Idle;
        }
    }

    void SpawnSpell()
    {
        if (spellPrefab == null || spellSpawnPoint == null) return;

        currentSpellInstance = Instantiate(spellPrefab, spellSpawnPoint.position, spellSpawnPoint.rotation);

        currentSpellRb = currentSpellInstance.GetComponent<Rigidbody>();
        if (currentSpellRb != null)
            currentSpellRb.isKinematic = true;

        currentParticleSystem = currentSpellInstance.GetComponentInChildren<ParticleSystem>();
        if (currentParticleSystem != null)
        {
            currentVfxTransform = currentParticleSystem.transform;
            currentVfxTransform.localScale = Vector3.zero;
            var main = currentParticleSystem.main;
            main.startColor = chargeStartColor;
        }

        chargedChildInstance = null;
        if (!string.IsNullOrEmpty(chargedChildName))
        {
            Transform child = currentSpellInstance.transform.Find(chargedChildName);
            if (child != null)
            {
                chargedChildInstance = child.gameObject;
                chargedChildInstance.SetActive(false);
            }
        }
    }

    void FollowSpawnPoint()
    {
        if (currentSpellInstance == null || spellSpawnPoint == null) return;

        currentSpellInstance.transform.position = spellSpawnPoint.position;
        currentSpellInstance.transform.rotation = spellSpawnPoint.rotation;
    }

    void UpdateChargingVfx(float t)
    {
        if (currentVfxTransform != null)
            currentVfxTransform.localScale = Vector3.one * scaleValue;

        if (currentParticleSystem != null)
        {
            var main = currentParticleSystem.main;
            main.startColor = Color.Lerp(chargeStartColor, chargeEndColor, t);
        }
    }

    void SetFullyChargedVfx()
    {
        if (currentVfxTransform != null)
            currentVfxTransform.localScale = Vector3.one;

        if (currentParticleSystem != null)
        {
            var main = currentParticleSystem.main;
            main.startColor = chargeEndColor;
        }

        if (chargedChildInstance != null)
            chargedChildInstance.SetActive(true);
    }

    void FireSpell(string reason)
    {
        if (currentSpellInstance == null) return;

        if (currentSpellRb != null)
        {
            currentSpellInstance.transform.SetParent(null);
            currentSpellRb.isKinematic = false;

            Vector3 shootDir = spellSpawnPoint != null ? spellSpawnPoint.forward : transform.forward;
            currentSpellRb.linearVelocity = shootDir * spellShootForce;
        }

        currentSpellInstance = null;
        currentSpellRb = null;
        currentParticleSystem = null;
        currentVfxTransform = null;
        chargedChildInstance = null;
    }

    void CancelSpell()
    {
        if (currentSpellInstance != null)
            Destroy(currentSpellInstance);

        currentSpellInstance = null;
        currentSpellRb = null;
        currentParticleSystem = null;
        currentVfxTransform = null;
        chargedChildInstance = null;
    }
}