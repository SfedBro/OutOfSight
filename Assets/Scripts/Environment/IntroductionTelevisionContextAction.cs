using Game.Interaction;
using UnityEngine;

namespace OutOfSight.Environment
{
    [DisallowMultipleComponent]
    public sealed class IntroductionTelevisionContextAction : MonoBehaviour, IPlayerContextInteraction
    {
        [SerializeField] private IntroductionSequenceController controller;
        [SerializeField] private string prompt = "Включить телевизор";

        private void Awake()
        {
            if (controller == null)
                controller = FindFirstObjectByType<IntroductionSequenceController>(FindObjectsInactive.Include);
        }

        public string GetPrompt(GameObject interactor)
        {
            return prompt;
        }

        public bool CanInteract(GameObject interactor)
        {
            return controller != null && controller.CanActivateTelevision(interactor);
        }

        public void Interact(GameObject interactor)
        {
            controller?.ActivateTelevision(interactor);
        }
    }
}
