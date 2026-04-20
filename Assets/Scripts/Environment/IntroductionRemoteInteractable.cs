using Game.Interaction;
using UnityEngine;

namespace OutOfSight.Environment
{
    [DisallowMultipleComponent]
    public sealed class IntroductionRemoteInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private IntroductionSequenceController controller;
        [SerializeField] private Transform playbackSource;
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private string prompt = "Взять пульт";
        [SerializeField] private bool disableObjectOnPickup = true;
        [SerializeField] private DialogueLine[] unavailableDialogueLines =
        {
            new DialogueLine("Мне не хочется смотреть телевизор. Надо узнать, что происходит на кухне.", null, 1f, 3f, 0f)
        };
        [SerializeField] private DialogueLine[] pickupDialogueLines;

        private bool isCollected;

        private void Awake()
        {
            if (controller == null)
                controller = FindFirstObjectByType<IntroductionSequenceController>(FindObjectsInactive.Include);

            if (visualRoot == null)
                visualRoot = gameObject;
        }

        public string GetPrompt()
        {
            return isCollected ? string.Empty : prompt;
        }

        public bool CanInteract(GameObject interactor)
        {
            return !isCollected;
        }

        public void Interact(GameObject interactor)
        {
            if (isCollected || controller == null)
                return;

            if (!controller.HasSeenMonster)
            {
                TryPlayDialogue(interactor, unavailableDialogueLines);
                return;
            }

            isCollected = true;
            controller.MarkRemoteCollected(interactor, pickupDialogueLines, playbackSource);

            if (disableObjectOnPickup && visualRoot != null)
                visualRoot.SetActive(false);
        }

        private static void TryPlayDialogue(GameObject interactor, DialogueLine[] lines)
        {
            if (lines == null || lines.Length == 0)
                return;

            DialogueSequencePlayer.GetOrCreate(interactor).Play(
                lines,
                interactor != null ? interactor.transform : null,
                false,
                true,
                0f,
                null);
        }
    }
}
