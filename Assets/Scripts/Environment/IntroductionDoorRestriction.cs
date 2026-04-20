using Game.Interaction;
using UnityEngine;

namespace OutOfSight.Environment
{
    [DisallowMultipleComponent]
    public sealed class IntroductionDoorRestriction : MonoBehaviour, IInteractionOverride
    {
        [SerializeField] private string prompt = "Open";
        [SerializeField] private string blockedSubtitleText = "Сначала надо взять ключи из гостиной.";
        [SerializeField, Min(0.1f)] private float subtitleDuration = 2.5f;
        [SerializeField] private Transform playbackSource;
        [SerializeField] private bool isBlocking = true;

        public bool IsActiveFor(GameObject interactor)
        {
            return isActiveAndEnabled && isBlocking;
        }

        public string GetPrompt()
        {
            return prompt;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isBlocking;
        }

        public void Interact(GameObject interactor)
        {
            if (!isBlocking || string.IsNullOrWhiteSpace(blockedSubtitleText))
                return;

            var player = DialogueSequencePlayer.GetOrCreate(interactor);
            var source = playbackSource != null ? playbackSource : interactor != null ? interactor.transform : null;
            player.Play(
                new[] { new DialogueLine(blockedSubtitleText, null, 1f, subtitleDuration, 0f) },
                source,
                false,
                true,
                0f,
                null);
        }

        public void Configure(string overridePrompt, string subtitleText, float duration, Transform overridePlaybackSource = null)
        {
            prompt = string.IsNullOrWhiteSpace(overridePrompt) ? "Open" : overridePrompt;
            blockedSubtitleText = subtitleText;
            subtitleDuration = Mathf.Max(0.1f, duration);
            playbackSource = overridePlaybackSource;
        }

        public void SetBlocking(bool value)
        {
            isBlocking = value;
        }
    }
}
