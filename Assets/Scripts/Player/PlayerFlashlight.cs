using UnityEngine;

/// <summary>
/// Фонарик игрока. Включается/выключается клавишей F.
/// Автоматически находит или создаёт Light-компонент в дочерних объектах.
/// </summary>
[AddComponentMenu("Game/Player/Player Flashlight")]
public class PlayerFlashlight : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;

    [Header("Light Settings")]
    [SerializeField] private Light flashlight;
    [SerializeField] private bool startEnabled = false;

    [SerializeField] private float intensity  = 1.8f;
    [SerializeField] private float range      = 18f;
    [SerializeField] private float spotAngle  = 55f;
    [SerializeField] private Color color      = Color.white;

    private void Awake()
    {
        EnsureLight();
        ApplyLightSettings();
        flashlight.enabled = startEnabled;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            flashlight.enabled = !flashlight.enabled;
    }

    private void EnsureLight()
    {
        if (flashlight != null)
            return;

        // Ищем существующий SpotLight среди дочерних
        flashlight = GetComponentInChildren<Light>(true);

        if (flashlight != null)
            return;

        // Создаём новый
        GameObject lightObject = new GameObject("FlashlightLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        lightObject.transform.localRotation = Quaternion.identity;

        flashlight = lightObject.AddComponent<Light>();
        flashlight.type = LightType.Spot;
    }

    private void ApplyLightSettings()
    {
        if (flashlight == null) return;

        flashlight.type      = LightType.Spot;
        flashlight.intensity = intensity;
        flashlight.range     = range;
        flashlight.spotAngle = spotAngle;
        flashlight.color     = color;
    }

    // Обновить настройки если изменили в Inspector во время Play Mode
    private void OnValidate()
    {
        if (flashlight == null) return;
        ApplyLightSettings();
    }
}
