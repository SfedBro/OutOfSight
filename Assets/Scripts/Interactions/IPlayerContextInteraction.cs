using UnityEngine;

namespace Game.Interaction
{
    public interface IPlayerContextInteraction
    {
        string GetPrompt(GameObject interactor);
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
