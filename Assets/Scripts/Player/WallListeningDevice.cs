using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WallListeningDevice : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode activationKey = KeyCode.Q;
    [SerializeField] private float activationCooldown = 0.25f;

    [Header("References")]
    [SerializeField] private StateController monsterController;
    [SerializeField] private Transform scanOrigin;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Detection")]
    [SerializeField] private float scanRadius = 16f;
    [SerializeField] [Range(5f, 180f)] private float coneAngle = 70f;
    [SerializeField] [Range(0f, 1f)] private float transmissionPerObstacle = 0.7f;
    [SerializeField] private float minimumAudibleVolume = 0.03f;

    [Header("Signal")]
    [SerializeField] private float signalVolume = 1f;
    [SerializeField] private float signalDuration = 0.12f;
    [SerializeField] private float signalFrequency = 1250f;
    [SerializeField] private float minPitch = 0.85f;
    [SerializeField] private float maxPitch = 1.25f;

    private AudioSource audioSource;
    private AudioClip generatedSignalClip;
    private float lastActivationTime = -999f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
        generatedSignalClip = CreateSignalClip();

        if (scanOrigin == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>(true);
            scanOrigin = playerCamera != null ? playerCamera.transform : transform;
        }

        if (monsterController == null)
            monsterController = FindFirstObjectByType<StateController>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(activationKey))
            return;

        if (Time.time - lastActivationTime < activationCooldown)
            return;

        lastActivationTime = Time.time;
        EmitListeningSignal();
    }

    private void EmitListeningSignal()
    {
        if (audioSource == null || generatedSignalClip == null || !TryGetMonsterTransform(out Transform monsterTransform))
            return;

        Transform originTransform = scanOrigin != null ? scanOrigin : transform;
        Vector3 origin = originTransform.position;
        Vector3 forward = originTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        Vector3 toMonster = monsterTransform.position - origin;
        toMonster.y = 0f;

        float distanceToMonster = toMonster.magnitude;
        if (distanceToMonster <= 0.01f || distanceToMonster > scanRadius)
            return;

        float angleToMonster = Vector3.Angle(forward.normalized, toMonster.normalized);
        if (angleToMonster > coneAngle * 0.5f)
            return;

        Vector3 horizontalTargetPosition = new Vector3(monsterTransform.position.x, origin.y, monsterTransform.position.z);
        int obstacleCount = CountObstaclesBetween(origin, horizontalTargetPosition, monsterTransform);
        float wallTransmission = Mathf.Pow(transmissionPerObstacle, obstacleCount);
        float distanceFactor = 1f - Mathf.Clamp01(distanceToMonster / scanRadius);
        float finalVolume = signalVolume * wallTransmission * distanceFactor;

        if (finalVolume < minimumAudibleVolume)
            return;

        float pitchFactor = 1f - Mathf.Clamp01(distanceToMonster / scanRadius);
        audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, pitchFactor);
        audioSource.PlayOneShot(generatedSignalClip, finalVolume);
    }

    private int CountObstaclesBetween(Vector3 origin, Vector3 targetPosition, Transform monsterTransform)
    {
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
            return 0;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return 0;

        HashSet<Collider> uniqueObstacles = new HashSet<Collider>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if (hitCollider.transform.IsChildOf(transform))
                continue;

            if (monsterTransform != null && hitCollider.transform.IsChildOf(monsterTransform))
                continue;

            uniqueObstacles.Add(hitCollider);
        }

        return uniqueObstacles.Count;
    }

    private bool TryGetMonsterTransform(out Transform monsterTransform)
    {
        if (monsterController == null)
            monsterController = FindFirstObjectByType<StateController>();

        monsterTransform = monsterController != null ? monsterController.transform : null;
        return monsterTransform != null;
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
    }

    private AudioClip CreateSignalClip()
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.Max(32, Mathf.RoundToInt(signalDuration * sampleRate));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float envelope = Mathf.Clamp01(1f - (i / (float)sampleCount));
            samples[i] = Mathf.Sin(time * signalFrequency * Mathf.PI * 2f) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("WallListeningSignal", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform = scanOrigin != null ? scanOrigin : transform;
        Vector3 origin = originTransform.position;
        Vector3 forward = originTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.Normalize();

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(origin, scanRadius);

        Vector3 leftBoundary = Quaternion.Euler(0f, -coneAngle * 0.5f, 0f) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0f, coneAngle * 0.5f, 0f) * forward;

        Gizmos.DrawRay(origin, leftBoundary * scanRadius);
        Gizmos.DrawRay(origin, rightBoundary * scanRadius);
        Gizmos.DrawRay(origin, forward * scanRadius);
    }
}
