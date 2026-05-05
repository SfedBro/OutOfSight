using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Interaction
{
    [DisallowMultipleComponent]
    public class DialogueSequencePlayer : MonoBehaviour
    {
        private const string RuntimeObjectName = "DialogueSequencePlayer";

        private sealed class DialoguePlaybackRequest
        {
            public DialogueLine[] Lines;
            public Transform PlaybackSource;
            public float StartDelay;
            public Action OnCompleted;
        }

        private static DialogueSequencePlayer runtimeFallbackInstance;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlendWhenUsingSourceTransform = 1f;
        [SerializeField, Min(0f)] private float minDistance = 1f;
        [SerializeField, Min(0f)] private float maxDistance = 12f;

        [Header("Subtitles")]
        [SerializeField] private int fontSize = 28;
        [SerializeField] private Vector2 subtitleAnchorMin = new Vector2(0.12f, 0.05f);
        [SerializeField] private Vector2 subtitleAnchorMax = new Vector2(0.88f, 0.16f);

        private AudioSource voiceAudioSource;
        private Transform currentPlaybackSource;
        private Canvas subtitleCanvas;
        private Text subtitleText;
        private Coroutine playbackRoutine;
        private Action playbackCompleted;
        private readonly Queue<DialoguePlaybackRequest> queuedRequests = new Queue<DialoguePlaybackRequest>();

        public bool IsPlaying => playbackRoutine != null;

        public static DialogueSequencePlayer GetOrCreate(GameObject preferredHost)
        {
            if (preferredHost != null)
            {
                DialogueSequencePlayer player = preferredHost.GetComponent<DialogueSequencePlayer>();
                if (player == null)
                    player = preferredHost.AddComponent<DialogueSequencePlayer>();

                return player;
            }

            if (runtimeFallbackInstance != null)
                return runtimeFallbackInstance;

            runtimeFallbackInstance = FindFirstObjectByType<DialogueSequencePlayer>(FindObjectsInactive.Include);
            if (runtimeFallbackInstance != null)
                return runtimeFallbackInstance;

            GameObject runtimeObject = new GameObject(RuntimeObjectName);
            DontDestroyOnLoad(runtimeObject);
            runtimeFallbackInstance = runtimeObject.AddComponent<DialogueSequencePlayer>();
            return runtimeFallbackInstance;
        }

        private void Awake()
        {
            EnsurePresentation();
            ClearSubtitle();
        }

        private void OnEnable()
        {
            EnsurePresentation();
            ApplyAudioSettings();
        }

        public bool Play(DialogueLine[] lines, Transform playbackSource = null, bool interruptCurrentDialogue = true, bool queueIfBusy = true, float startDelay = 0f, Action onCompleted = null)
        {
            if (lines == null || lines.Length == 0)
                return false;

            DialoguePlaybackRequest request = new DialoguePlaybackRequest
            {
                Lines = lines,
                PlaybackSource = playbackSource,
                StartDelay = Mathf.Max(0f, startDelay),
                OnCompleted = onCompleted
            };

            if (IsPlaying)
            {
                if (interruptCurrentDialogue)
                {
                    StopPlayback();
                }
                else if (queueIfBusy)
                {
                    queuedRequests.Enqueue(request);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            StartPlayback(request);
            return true;
        }

        public void StopPlayback()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            if (voiceAudioSource != null)
                voiceAudioSource.Stop();

            playbackCompleted = null;
            currentPlaybackSource = null;
            queuedRequests.Clear();
            ClearSubtitle();
        }

        private void StartPlayback(DialoguePlaybackRequest request)
        {
            currentPlaybackSource = request.PlaybackSource;
            playbackCompleted = request.OnCompleted;
            EnsurePresentation();
            playbackRoutine = StartCoroutine(PlayRoutine(request.Lines, request.StartDelay));
        }

        private IEnumerator PlayRoutine(DialogueLine[] lines, float startDelay)
        {
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            foreach (DialogueLine line in lines)
            {
                if (line == null || line.IsEmpty())
                    continue;

                SetSubtitle(line.SubtitleText);

                if (voiceAudioSource != null)
                {
                    voiceAudioSource.Stop();
                    voiceAudioSource.clip = null;

                    if (line.VoiceClip != null)
                        voiceAudioSource.PlayOneShot(line.VoiceClip, voiceVolume * Mathf.Max(0.1f, line.VolumeMultiplier));
                }

                float playbackDuration = line.GetPlaybackDuration();
                if (playbackDuration > 0f)
                    yield return new WaitForSeconds(playbackDuration);

                if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                    voiceAudioSource.Stop();

                if (line.DelayAfter > 0f)
                    yield return new WaitForSeconds(line.DelayAfter);
            }

            playbackRoutine = null;
            ClearSubtitle();

            Action completed = playbackCompleted;
            playbackCompleted = null;
            currentPlaybackSource = null;
            completed?.Invoke();

            if (playbackRoutine == null)
                TryStartNextQueued();
        }

        private void TryStartNextQueued()
        {
            if (IsPlaying || queuedRequests.Count == 0)
                return;

            DialoguePlaybackRequest nextRequest = queuedRequests.Dequeue();
            StartPlayback(nextRequest);
        }

        private void EnsurePresentation()
        {
            EnsureAudioSource(currentPlaybackSource);
            EnsureSubtitleCanvas();
            ApplyAudioSettings();
        }

        private void EnsureAudioSource(Transform playbackSource)
        {
            GameObject sourceObject = playbackSource != null ? playbackSource.gameObject : gameObject;

            if (voiceAudioSource != null && voiceAudioSource.gameObject != sourceObject)
                voiceAudioSource = null;

            if (voiceAudioSource == null)
                voiceAudioSource = sourceObject.GetComponent<AudioSource>();

            if (voiceAudioSource == null)
                voiceAudioSource = sourceObject.AddComponent<AudioSource>();
        }

        private void ApplyAudioSettings()
        {
            if (voiceAudioSource == null)
                return;

            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.loop = false;
            voiceAudioSource.volume = voiceVolume;
            voiceAudioSource.spatialBlend = currentPlaybackSource != null ? spatialBlendWhenUsingSourceTransform : 0f;
            voiceAudioSource.minDistance = minDistance;
            voiceAudioSource.maxDistance = maxDistance;
        }

        private void EnsureSubtitleCanvas()
        {
            if (subtitleCanvas != null && subtitleText != null)
                return;

            GameObject canvasObject = new GameObject("DialogueSubtitleCanvas");
            canvasObject.transform.SetParent(transform, false);

            subtitleCanvas = canvasObject.AddComponent<Canvas>();
            subtitleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            subtitleCanvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject subtitleObject = new GameObject("SubtitleText");
            subtitleObject.transform.SetParent(canvasObject.transform, false);

            RectTransform subtitleRect = subtitleObject.AddComponent<RectTransform>();
            subtitleRect.anchorMin = subtitleAnchorMin;
            subtitleRect.anchorMax = subtitleAnchorMax;
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            subtitleText = subtitleObject.AddComponent<Text>();
            subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subtitleText.fontSize = fontSize;
            subtitleText.alignment = TextAnchor.LowerCenter;
            subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
            subtitleText.color = new Color(1f, 1f, 1f, 0.97f);
            subtitleText.text = string.Empty;
        }

        private void SetSubtitle(string text)
        {
            if (subtitleText == null)
                return;

            subtitleText.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }

        private void ClearSubtitle()
        {
            if (subtitleText != null)
                subtitleText.text = string.Empty;
        }
    }
}
