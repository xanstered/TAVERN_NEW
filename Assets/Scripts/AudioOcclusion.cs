using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AudioOcclusion : MonoBehaviour
{
    [Header("FMOD Event")]
    [SerializeField]
    [Tooltip("StudioEventEmitter źródła dźwięku (np. muzyka, ambient)")]
    private StudioEventEmitter eventEmitter;
    private EventInstance eventInstance;
    private EventDescription eventDes;
    private StudioListener listener;
    private PLAYBACK_STATE pb;

    [Header("Occlusion Options")]
    [SerializeField]
    [Range(0f, 10f)]
    [Tooltip("Szerokość testowania od źródła (dla muzyki: 1-2)")]
    private float SoundOcclusionWidening = 1f;

    [SerializeField]
    [Range(0f, 10f)]
    [Tooltip("Szerokość testowania od gracza (0.5-1.5)")]
    private float PlayerOcclusionWidening = 1f;

    [SerializeField]
    [Tooltip("Warstwa ścian/przeszkód (np. Environment)")]
    private LayerMask OcclusionLayer;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugRays = true;

    [SerializeField]
    private bool showDebugLogs = false;

    private bool audioIsVirtual;
    private float minDistance;
    private float maxDistance;
    private float listenerDistance;
    private float lineCastHitCount = 0f;
    private Color colour;

    private void Start()
    {
        if (eventEmitter == null)
        {
            Debug.LogError("AudioOcclusion: Brak przypisanego StudioEventEmitter!", this);
            enabled = false;
            return;
        }

        eventInstance = eventEmitter.EventInstance;

        if (!eventInstance.isValid())
        {
            Debug.LogError("AudioOcclusion: EventInstance jest nieprawidłowy!", this);
            enabled = false;
            return;
        }

        eventInstance.getDescription(out eventDes);
        eventDes.getMinMaxDistance(out minDistance, out maxDistance);

        if (showDebugLogs)
            Debug.Log($"AudioOcclusion inicjalizacja: Min={minDistance}, Max={maxDistance}", this);

        listener = FindObjectOfType<StudioListener>();

        if (listener == null)
        {
            Debug.LogError("AudioOcclusion: Nie znaleziono StudioListener na scenie!", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (!eventInstance.isValid() || listener == null)
            return;

        eventInstance.isVirtual(out audioIsVirtual);
        eventInstance.getPlaybackState(out pb);
        listenerDistance = Vector3.Distance(transform.position, listener.transform.position);

        if (!audioIsVirtual && pb == PLAYBACK_STATE.PLAYING)
        {
            OccludeBetween(transform.position, listener.transform.position);
        }
        else
        {
            eventInstance.setParameterByName("Occlusion", 0f);
        }

        lineCastHitCount = 0f;
    }

    void OnDestroy()
    {
        if (eventInstance.isValid())
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
        }
    }

    private void OccludeBetween(Vector3 sound, Vector3 listener)
    {
        Vector3 SoundLeft = CalculatePoint(sound, listener, SoundOcclusionWidening, true);
        Vector3 SoundRight = CalculatePoint(sound, listener, SoundOcclusionWidening, false);
        Vector3 SoundAbove = new Vector3(sound.x, sound.y + SoundOcclusionWidening, sound.z);
        Vector3 SoundBelow = new Vector3(sound.x, sound.y - SoundOcclusionWidening, sound.z);

        Vector3 ListenerLeft = CalculatePoint(listener, sound, PlayerOcclusionWidening, true);
        Vector3 ListenerRight = CalculatePoint(listener, sound, PlayerOcclusionWidening, false);
        Vector3 ListenerAbove = new Vector3(listener.x, listener.y + PlayerOcclusionWidening * 0.5f, listener.z);
        Vector3 ListenerBelow = new Vector3(listener.x, listener.y - PlayerOcclusionWidening * 0.5f, listener.z);

        CastLine(SoundLeft, ListenerLeft);
        CastLine(SoundLeft, listener);
        CastLine(SoundLeft, ListenerRight);

        CastLine(sound, ListenerLeft);
        CastLine(sound, listener);
        CastLine(sound, ListenerRight);

        CastLine(SoundRight, ListenerLeft);
        CastLine(SoundRight, listener);
        CastLine(SoundRight, ListenerRight);

        CastLine(SoundAbove, ListenerAbove);
        CastLine(SoundBelow, ListenerBelow);

        // Kolor debug rays
        if (PlayerOcclusionWidening == 0f || SoundOcclusionWidening == 0f)
            colour = Color.blue;
        else
            colour = Color.green;

        SetParameter();
    }

    private Vector3 CalculatePoint(Vector3 a, Vector3 b, float m, bool posOrneg)
    {
        float x;
        float z;
        float n = Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
        float mn = (m / n);

        if (posOrneg)
        {
            x = a.x + (mn * (a.z - b.z));
            z = a.z - (mn * (a.x - b.x));
        }
        else
        {
            x = a.x - (mn * (a.z - b.z));
            z = a.z + (mn * (a.x - b.x));
        }

        return new Vector3(x, a.y, z);
    }

    private void CastLine(Vector3 Start, Vector3 End)
    {
        RaycastHit hit;
        Physics.Linecast(Start, End, out hit, OcclusionLayer);

        if (hit.collider)
        {
            lineCastHitCount++;
            if (showDebugRays)
                Debug.DrawLine(Start, End, Color.red);
        }
        else
        {
            if (showDebugRays)
                Debug.DrawLine(Start, End, colour);
        }
    }

    private void SetParameter()
    {
        float occlusionValue = lineCastHitCount / 11f;
        eventInstance.setParameterByName("Occlusion", occlusionValue);

        if (showDebugLogs && lineCastHitCount > 0)
        {
            Debug.Log($"Occlusion: {occlusionValue:F2} ({lineCastHitCount}/11 promieni zablokowanych)");
        }
    }
}