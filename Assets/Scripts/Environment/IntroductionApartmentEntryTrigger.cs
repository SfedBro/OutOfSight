using Game.Interaction;
using UnityEngine;

namespace OutOfSight.Environment
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class IntroductionApartmentEntryTrigger : MonoBehaviour
    {
        [SerializeField] private IntroductionSequenceController controller;
        [SerializeField] private Transform playbackSource;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private DialogueLine[] entryDialogueLines =
        {
            new DialogueLine("Надо осмотреться тихо.", null, 1f, 2f, 0f)
        };

        private bool hasTriggered;

        private void Reset()
        {
            controller = GetComponentInParent<IntroductionSequenceController>();
            EnsureTriggerCollider();
        }

        private void Awake()
        {
            if (controller == null)
                controller = GetComponentInParent<IntroductionSequenceController>();

            EnsureTriggerCollider();
        }

        private void OnEnable()
        {
            hasTriggered = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((triggerOnce && hasTriggered) || controller == null || !IsPlayer(other))
                return;

            hasTriggered = true;
            controller.MarkApartmentEntered(other.transform.root.gameObject, entryDialogueLines, playbackSource);
        }

        private void EnsureTriggerCollider()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        private static bool IsPlayer(Collider other)
        {
            if (other == null)
                return false;

            return other.GetComponentInParent<PlayerMovement>() != null ||
                   other.GetComponentInParent<PlayerHiding>() != null;
        }
    }
}
