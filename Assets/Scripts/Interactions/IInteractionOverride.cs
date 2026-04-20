using UnityEngine;

namespace Game.Interaction
{
    public interface IInteractionOverride
    {
        bool IsActiveFor(GameObject interactor);
        string GetPrompt();
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
