using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MonsterEcholocationDevice : MonoBehaviour
{
    private const int AuraTextureSize = 128;

    [Header("Input")]
    [SerializeField] private KeyCode activationKey = KeyCode.R;
    [SerializeField] private float activationCooldown = 6f;

    [Header("References")]
    [SerializeField] private StateController monsterController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform playerPositionSource;

    [Header("Reveal")]
    [SerializeField] private float revealRadius = 30f;
    [SerializeField] private float revealDuration = 2f;
    [SerializeField] private Vector3 auraWorldOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private float auraScreenSize = 180f;
    [SerializeField] private Color auraColor = new Color(0.3f, 0.9f, 1f, 0.8f);
    [SerializeField] private float auraPulseSpeed = 4f;
    [SerializeField] private float auraPulseAmount = 0.12f;

    [Header("Monster Alert")]
    [SerializeField] private bool useDistanceBasedAlertDelay = true;
    [SerializeField] private float fixedAlertDelay = 1.5f;
    [SerializeField] private float minDistanceAlertDelay = 0.5f;
    [SerializeField] private float maxDistanceAlertDelay = 2.5f;
    [SerializeField] private float maxDistanceForDelay = 25f;
    [SerializeField] private float approximatePositionRadius = 2.5f;
    [SerializeField] private float additionalErrorAtMaxDistance = 2f;

    private Canvas auraCanvas;
    private RectTransform auraRectTransform;
    private Image auraImage;
    private float revealTimer;
    private float lastActivationTime = -999f;
    private Coroutine pendingAlertCoroutine;

    private static Sprite sharedAuraSprite;

    private void Awake()
    {
        if (monsterController == null)
            monsterController = FindFirstObjectByType<StateController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerPositionSource == null)
            playerPositionSource = transform;

        EnsureAuraOverlay();
    }

    private void Update()
    {
        if (Input.GetKeyDown(activationKey) && Time.time - lastActivationTime >= activationCooldown)
            ActivateEcholocation();

        UpdateAuraOverlay();
    }

    private void OnDisable()
    {
        if (pendingAlertCoroutine != null)
        {
            StopCoroutine(pendingAlertCoroutine);
            pendingAlertCoroutine = null;
        }

        if (auraImage != null)
            auraImage.enabled = false;
    }

    private void ActivateEcholocation()
    {
        if (!TryGetMonsterTransform(out Transform monsterTransform))
            return;

        float distanceToMonster = Vector3.Distance(playerPositionSource.position, monsterTransform.position);
        if (distanceToMonster > revealRadius)
            return;

        lastActivationTime = Time.time;
        revealTimer = revealDuration;

        if (pendingAlertCoroutine != null)
            StopCoroutine(pendingAlertCoroutine);

        Vector3 playerSnapshot = playerPositionSource.position;
        float alertDelay = GetAlertDelay(distanceToMonster);
        pendingAlertCoroutine = StartCoroutine(DelayMonsterAlert(playerSnapshot, distanceToMonster, alertDelay));
    }

    private IEnumerator DelayMonsterAlert(Vector3 playerSnapshot, float distanceToMonster, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        pendingAlertCoroutine = null;

        if (monsterController == null)
            yield break;

        if (monsterController.CurrentState is ChasingState)
            yield break;

        if (monsterController.Behaviour != null && monsterController.Behaviour.CanSeeTarget())
            yield break;

        Vector2 randomOffset2D = Random.insideUnitCircle * GetErrorRadius(distanceToMonster);
        Vector3 approximatePosition = playerSnapshot + new Vector3(randomOffset2D.x, 0f, randomOffset2D.y);

        monsterController.StartInvestigationFromNoise(approximatePosition, shouldInspectHidingSpots: true);
    }

    private float GetAlertDelay(float distanceToMonster)
    {
        if (!useDistanceBasedAlertDelay)
            return fixedAlertDelay;

        float normalizedDistance = maxDistanceForDelay > 0.01f
            ? Mathf.Clamp01(distanceToMonster / maxDistanceForDelay)
            : 0f;

        return Mathf.Lerp(minDistanceAlertDelay, maxDistanceAlertDelay, normalizedDistance);
    }

    private float GetErrorRadius(float distanceToMonster)
    {
        float normalizedDistance = maxDistanceForDelay > 0.01f
            ? Mathf.Clamp01(distanceToMonster / maxDistanceForDelay)
            : 0f;

        return approximatePositionRadius + additionalErrorAtMaxDistance * normalizedDistance;
    }

    private void EnsureAuraOverlay()
    {
        if (auraCanvas != null && auraImage != null && auraRectTransform != null)
            return;

        GameObject canvasObject = new GameObject("MonsterEchoAuraCanvas");
        canvasObject.transform.SetParent(transform, false);

        auraCanvas = canvasObject.AddComponent<Canvas>();
        auraCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        auraCanvas.sortingOrder = 2000;

        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("MonsterEchoAura");
        imageObject.transform.SetParent(canvasObject.transform, false);

        auraRectTransform = imageObject.AddComponent<RectTransform>();
        auraRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        auraRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        auraRectTransform.pivot = new Vector2(0.5f, 0.5f);
        auraRectTransform.sizeDelta = Vector2.one * auraScreenSize;

        auraImage = imageObject.AddComponent<Image>();
        auraImage.raycastTarget = false;
        auraImage.sprite = GetOrCreateAuraSprite();
        auraImage.enabled = false;
    }

    private void UpdateAuraOverlay()
    {
        if (auraImage == null || auraRectTransform == null || !TryGetMonsterTransform(out Transform monsterTransform) || playerCamera == null)
            return;

        if (revealTimer <= 0f)
        {
            auraImage.enabled = false;
            return;
        }

        revealTimer -= Time.deltaTime;

        Vector3 worldPosition = monsterTransform.position + auraWorldOffset;
        Vector3 screenPosition = playerCamera.WorldToScreenPoint(worldPosition);

        if (screenPosition.z <= 0f)
        {
            auraImage.enabled = false;
            return;
        }

        float distanceToMonster = Vector3.Distance(playerPositionSource.position, monsterTransform.position);
        if (distanceToMonster > revealRadius)
        {
            auraImage.enabled = false;
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * auraPulseSpeed) * auraPulseAmount;
        float fade = Mathf.Clamp01(revealTimer / Mathf.Max(0.01f, revealDuration));

        auraRectTransform.position = screenPosition;
        auraRectTransform.sizeDelta = Vector2.one * auraScreenSize * pulse;

        Color currentColor = auraColor;
        currentColor.a *= fade;
        auraImage.color = currentColor;
        auraImage.enabled = true;
    }

    private static Sprite GetOrCreateAuraSprite()
    {
        if (sharedAuraSprite != null)
            return sharedAuraSprite;

        Texture2D texture = new Texture2D(AuraTextureSize, AuraTextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((AuraTextureSize - 1) * 0.5f, (AuraTextureSize - 1) * 0.5f);
        float radius = AuraTextureSize * 0.5f;

        for (int y = 0; y < AuraTextureSize; y++)
        {
            for (int x = 0; x < AuraTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        sharedAuraSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, AuraTextureSize, AuraTextureSize),
            new Vector2(0.5f, 0.5f),
            AuraTextureSize);

        return sharedAuraSprite;
    }

    private bool TryGetMonsterTransform(out Transform monsterTransform)
    {
        if (monsterController == null)
            monsterController = FindFirstObjectByType<StateController>();

        monsterTransform = monsterController != null ? monsterController.transform : null;
        return monsterTransform != null;
    }
}
