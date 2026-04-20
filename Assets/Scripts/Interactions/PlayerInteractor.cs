using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float distance = 2.5f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private GameObject interactorRoot;

        [Header("UI (optional)")]
        [SerializeField] private Text promptText;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private bool hidePromptWhenEmpty = true;

        [Header("Input")]
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private const int MaxRaycastHits = 16;

        private IInteractable current;
        private IInteractionOverride currentOverride;
        private IPlayerContextInteraction currentContextInteraction;
        private Transform playerCameraTransform;
        private GameObject resolvedInteractorRoot;
        private string lastPromptValue = string.Empty;
        private bool promptVisible = true;
        private readonly List<DialogueSequenceTrigger> interactionDialogueTriggers = new List<DialogueSequenceTrigger>();
        private readonly RaycastHit[] raycastHits = new RaycastHit[MaxRaycastHits];

        private void Awake()
        {
            ResolveReferences();
            ClearPrompt();
        }

        private void Reset()
        {
            EnsureDefaults();
            ResolveReferences();
        }

        private void OnValidate()
        {
            EnsureDefaults();

            if (promptText != null && promptRoot == null)
                promptRoot = promptText.gameObject;
        }

        private void OnEnable()
        {
            ResolveReferences();
            ClearPrompt();
        }

        private void OnDisable()
        {
            current = null;
            currentOverride = null;
            currentContextInteraction = null;
            ClearPrompt();
        }

        private void Update()
        {
            UpdateTarget();

            if (!Input.GetKeyDown(interactKey))
                return;

            GameObject interactorObject = GetInteractorObject();

            if (currentOverride != null)
            {
                if (!currentOverride.CanInteract(interactorObject))
                    return;

                currentOverride.Interact(interactorObject);
                return;
            }

            if (current == null || !current.CanInteract(interactorObject))
            {
                if (currentContextInteraction == null || !currentContextInteraction.CanInteract(interactorObject))
                    return;

                currentContextInteraction.Interact(interactorObject);
                return;
            }

            current.Interact(interactorObject);
            NotifyInteractionDialogue(current, interactorObject);
        }

        private void UpdateTarget()
        {
            current = null;
            currentOverride = null;
            currentContextInteraction = null;

            if (playerCameraTransform == null)
            {
                SetPrompt(string.Empty);
                return;
            }

            Ray ray = new Ray(playerCameraTransform.position, playerCameraTransform.forward);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                distance,
                interactionMask,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                System.Array.Sort(raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

                for (int i = 0; i < hitCount; i++)
                {
                    Collider hitCollider = raycastHits[i].collider;
                if (hitCollider == null)
                    continue;

                GameObject interactorObject = GetInteractorObject();
                IInteractionOverride interactionOverride = hitCollider.GetComponentInParent<IInteractionOverride>();
                if (interactionOverride != null && interactionOverride.IsActiveFor(interactorObject))
                {
                    currentOverride = interactionOverride;
                    string overridePrompt = interactionOverride.GetPrompt();
                    SetPrompt(string.IsNullOrWhiteSpace(overridePrompt) ? string.Empty : $"{interactKey}: {overridePrompt}");
                    return;
                }

                IInteractable interactable = hitCollider.GetComponentInParent<IInteractable>();
                if (interactable == null)
                    continue;

                    current = interactable;
                    string prompt = interactable.GetPrompt();
                    if (!string.IsNullOrWhiteSpace(prompt))
                    {
                        SetPrompt($"{interactKey}: {prompt}");
                        return;
                    }

                    return;
                }
            }

            if (TryResolveContextInteraction())
                return;

            SetPrompt(string.Empty);
        }

        private bool TryResolveContextInteraction()
        {
            GameObject interactorObject = GetInteractorObject();
            if (interactorObject == null)
                return false;

            MonoBehaviour[] behaviours = interactorObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled)
                    continue;

                if (behaviour is not IPlayerContextInteraction contextInteraction)
                    continue;

                if (!contextInteraction.CanInteract(interactorObject))
                    continue;

                currentContextInteraction = contextInteraction;
                string prompt = contextInteraction.GetPrompt(interactorObject);
                if (!string.IsNullOrWhiteSpace(prompt))
                    SetPrompt($"{interactKey}: {prompt}");

                return true;
            }

            return false;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }

        private void ResolveReferences()
        {
            EnsureDefaults();

            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>(true) ?? Camera.main;

            playerCameraTransform = playerCamera != null ? playerCamera.transform : null;

            if (promptText != null && promptRoot == null)
                promptRoot = promptText.gameObject;

            resolvedInteractorRoot = interactorRoot != null ? interactorRoot : ResolveInteractorRoot();
            if (promptRoot != null)
                promptVisible = promptRoot.activeSelf;
        }

        private void EnsureDefaults()
        {
            if (interactionMask.value == 0)
                interactionMask = Physics.DefaultRaycastLayers;
        }

        private GameObject ResolveInteractorRoot()
        {
            SimpleInventory inventory = GetComponentInParent<SimpleInventory>();
            if (inventory != null)
                return inventory.gameObject;

            PlayerHiding playerHiding = GetComponentInParent<PlayerHiding>();
            if (playerHiding != null)
                return playerHiding.gameObject;

            return transform.root.gameObject;
        }

        private GameObject GetInteractorObject()
        {
            if (resolvedInteractorRoot == null)
                resolvedInteractorRoot = interactorRoot != null ? interactorRoot : ResolveInteractorRoot();

            return resolvedInteractorRoot;
        }

        private void ClearPrompt()
        {
            SetPrompt(string.Empty);
        }

        private void NotifyInteractionDialogue(IInteractable interactable, GameObject interactorObject)
        {
            Component interactableComponent = interactable as Component;
            if (interactableComponent is IInteractableSourceProxy sourceProxy && sourceProxy.SourceComponent != null)
                interactableComponent = sourceProxy.SourceComponent;

            if (interactableComponent == null)
                return;

            interactionDialogueTriggers.Clear();
            CollectInteractionDialogueTriggers(interactableComponent.GetComponents<DialogueSequenceTrigger>());
            CollectInteractionDialogueTriggers(interactableComponent.GetComponentsInParent<DialogueSequenceTrigger>(true));
            CollectInteractionDialogueTriggers(interactableComponent.GetComponentsInChildren<DialogueSequenceTrigger>(true));

            foreach (DialogueSequenceTrigger trigger in interactionDialogueTriggers)
                trigger.TryPlayFromInteraction(interactorObject);
        }

        private void CollectInteractionDialogueTriggers(DialogueSequenceTrigger[] triggers)
        {
            if (triggers == null)
                return;

            foreach (DialogueSequenceTrigger trigger in triggers)
            {
                if (trigger == null || interactionDialogueTriggers.Contains(trigger))
                    continue;

                interactionDialogueTriggers.Add(trigger);
            }
        }

        private void SetPrompt(string value)
        {
            bool shouldShow = !hidePromptWhenEmpty || !string.IsNullOrEmpty(value);

            if (promptRoot != null && promptVisible != shouldShow)
            {
                promptRoot.SetActive(shouldShow);
                promptVisible = shouldShow;
            }

            if (promptText != null && lastPromptValue != value)
            {
                promptText.text = value;
                lastPromptValue = value;
            }
        }
    }
}
