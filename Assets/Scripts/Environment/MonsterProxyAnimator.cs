using UnityEngine;

namespace OutOfSight.Environment
{
    /// <summary>
    /// Добавляется на объект прокси-монстра (Capsule).
    /// - Воспроизводит звук при активации телевизора.
    /// - Проигрывает процедурную анимацию покачивания пока объект движется.
    /// </summary>
    [AddComponentMenu("Game/Environment/Monster Proxy Animator")]
    [DisallowMultipleComponent]
    public sealed class MonsterProxyAnimator : MonoBehaviour
    {
        [Header("Sound")]
        [Tooltip("AudioSource на этом объекте или рядом")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Звук который играет при активации телевизора (задаётся в Inspector)")]
        [SerializeField] private AudioClip activationClip;
        [SerializeField, Range(0f, 1f)] private float activationVolume = 1f;

        [Header("Walk Animation")]
        [Tooltip("Амплитуда покачивания вверх-вниз (в локальных единицах)")]
        [SerializeField] private float bobAmplitude = 0.12f;
        [Tooltip("Частота покачивания (Гц)")]
        [SerializeField] private float bobFrequency = 2.5f;
        [Tooltip("Амплитуда наклона вперёд-назад (градусы)")]
        [SerializeField] private float tiltAmplitude = 4f;
        [Tooltip("Порог скорости движения для включения анимации")]
        [SerializeField] private float moveThreshold = 0.05f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private Vector3 previousWorldPosition;
        private float bobPhase;
        private bool isAnimating;
        private bool initialized;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        private void OnEnable()
        {
            // Запоминаем базовую позицию/ротацию при активации
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            previousWorldPosition = transform.position;
            bobPhase = 0f;
            initialized = true;
        }

        private void OnDisable()
        {
            // Сбрасываем трансформ при выключении
            if (initialized)
            {
                transform.localPosition = baseLocalPosition;
                transform.localRotation = baseLocalRotation;
            }
            isAnimating = false;
        }

        private void Update()
        {
            if (!initialized) return;

            float movedDistance = Vector3.Distance(transform.position, previousWorldPosition);
            float speed = movedDistance / Mathf.Max(Time.deltaTime, 0.0001f);
            previousWorldPosition = transform.position;

            bool shouldAnimate = speed > moveThreshold;

            if (shouldAnimate)
            {
                bobPhase += bobFrequency * Time.deltaTime * Mathf.PI * 2f;

                float bobOffset = Mathf.Sin(bobPhase) * bobAmplitude;
                float tiltAngle = Mathf.Sin(bobPhase) * tiltAmplitude;

                transform.localPosition = baseLocalPosition + transform.up * bobOffset;
                transform.localRotation = baseLocalRotation * Quaternion.Euler(tiltAngle, 0f, 0f);

                isAnimating = true;
            }
            else if (isAnimating)
            {
                // Плавно возвращаемся в исходную позу
                transform.localPosition = Vector3.Lerp(
                    transform.localPosition, baseLocalPosition, Time.deltaTime * 8f);
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation, baseLocalRotation, Time.deltaTime * 8f);

                if (Vector3.Distance(transform.localPosition, baseLocalPosition) < 0.001f)
                {
                    transform.localPosition = baseLocalPosition;
                    transform.localRotation = baseLocalRotation;
                    isAnimating = false;
                    bobPhase = 0f;
                }
            }
        }

        /// <summary>
        /// Вызывается из IntroductionSequenceController при активации телевизора.
        /// </summary>
        public void PlayActivationSound()
        {
            if (audioSource == null || activationClip == null) return;
            audioSource.PlayOneShot(activationClip, activationVolume);
        }

        /// <summary>
        /// Обновляет базовую позицию (вызывать если объект телепортирован).
        /// </summary>
        public void ResetBase()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            previousWorldPosition = transform.position;
            bobPhase = 0f;
        }
    }
}
