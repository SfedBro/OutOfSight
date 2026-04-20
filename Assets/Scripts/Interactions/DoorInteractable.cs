using System.Collections.Generic;
using UnityEngine;

namespace Game.Interaction
{
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        private enum DoorRotationAxis
        {
            X,
            Y,
            Z
        }

        private enum MotionMode
        {
            SelfRotation,
            InferredHingeOrbit,
            ExternalPivotOrbit,
            ExternalPivotRotation,
            ChildPivotOrbit
        }

        [SerializeField] private Transform pivot;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float speed = 6f;
        [SerializeField] private DoorRotationAxis rotationAxis = DoorRotationAxis.Y;
        [SerializeField] private bool rotateOwnTransformDirectly = false;
        [SerializeField] private bool useDetachedRuntimeProxyForNonUniformScale = true;
        [SerializeField] private bool useVisualProxyForNonUniformScale = true;
        [SerializeField] private bool inferHingeFromColliderWhenPivotMissing = true;

        private bool isOpen;
        private MotionMode motionMode;
        private bool isUsingDetachedProxy;
        private bool isUsingVisualProxy;
        private Transform detachedHingeTransform;
        private GameObject detachedProxyRootObject;
        private Transform externalOrbitHingeTransform;
        private Transform originalParent;
        private int originalSiblingIndex;
        private Transform visualProxyTransform;
        private Vector3 inferredHingeLocalPoint;
        private Quaternion closedLocalRotation;
        private Quaternion openLocalRotation;
        private Vector3 closedWorldPosition;
        private Vector3 openWorldPosition;
        private Quaternion closedWorldRotation;
        private Quaternion openWorldRotation;
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private readonly List<Collider> hiddenColliders = new List<Collider>();

        private void Awake()
        {
            ResolveMotionMode();
            SetupExternalOrbitHingeIfNeeded();
            SetupVisualProxyIfNeeded();
            CacheRotations();
        }

        private void OnValidate()
        {
            ResolveMotionMode();
            CacheRotations();
        }

        private void OnDestroy()
        {
            if (detachedProxyRootObject != null)
                Destroy(detachedProxyRootObject);

            if (externalOrbitHingeTransform != null)
            {
                Destroy(externalOrbitHingeTransform.gameObject);
            }

            if (visualProxyTransform != null)
                Destroy(visualProxyTransform.gameObject);

            DoorVisualProxyUtility.RestoreHiddenRenderers(hiddenRenderers);
            DoorDetachedProxyUtility.RestoreHiddenColliders(hiddenColliders);
            hiddenRenderers.Clear();
            hiddenColliders.Clear();
        }

        private void Update()
        {
            if (isUsingDetachedProxy)
            {
                UpdateDetachedProxyMotion();
                return;
            }

            switch (motionMode)
            {
                case MotionMode.InferredHingeOrbit:
                case MotionMode.ChildPivotOrbit:
                    UpdateOrbitMotion();
                    break;
                case MotionMode.ExternalPivotOrbit:
                case MotionMode.ExternalPivotRotation:
                case MotionMode.SelfRotation:
                    UpdateRotationMotion();
                    break;
            }

            SyncVisualProxyToSource();
        }

        public string GetPrompt()
        {
            if (TryGetInteractionOverride(null, out var interactionOverride))
                return interactionOverride.GetPrompt();

            return isOpen ? "Close" : "Open";
        }

        public bool CanInteract(GameObject interactor)
        {
            if (TryGetInteractionOverride(interactor, out var interactionOverride))
                return interactionOverride.CanInteract(interactor);

            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (TryGetInteractionOverride(interactor, out var interactionOverride))
            {
                interactionOverride.Interact(interactor);
                return;
            }

            isOpen = !isOpen;
        }

        private bool TryGetInteractionOverride(GameObject interactor, out IInteractionOverride interactionOverride)
        {
            interactionOverride = GetComponentInParent<IInteractionOverride>();
            return interactionOverride != null && interactionOverride.IsActiveFor(interactor);
        }

        private void CacheRotations()
        {
            if (isUsingDetachedProxy && detachedHingeTransform != null)
            {
                closedLocalRotation = detachedHingeTransform.localRotation;
                openLocalRotation = closedLocalRotation * GetOpenDeltaRotation();
                return;
            }

            if (motionMode == MotionMode.ExternalPivotOrbit && externalOrbitHingeTransform != null)
            {
                closedLocalRotation = externalOrbitHingeTransform.localRotation;
                openLocalRotation = closedLocalRotation * GetOpenDeltaRotation();
                return;
            }

            if (motionMode == MotionMode.ChildPivotOrbit ||
                motionMode == MotionMode.InferredHingeOrbit ||
                motionMode == MotionMode.ExternalPivotOrbit)
            {
                closedWorldPosition = transform.position;
                closedWorldRotation = transform.rotation;

                Vector3 hingeLocalOffset = motionMode == MotionMode.InferredHingeOrbit
                    ? inferredHingeLocalPoint
                    : transform.InverseTransformPoint(pivot.position);
                Vector3 hingeWorldPosition = motionMode == MotionMode.InferredHingeOrbit
                    ? transform.TransformPoint(inferredHingeLocalPoint)
                    : pivot.position;

                openWorldRotation = closedWorldRotation * GetOpenDeltaRotation();
                openWorldPosition = hingeWorldPosition - (openWorldRotation * hingeLocalOffset);
                return;
            }

            Transform animatedTransform = motionMode == MotionMode.ExternalPivotRotation ? pivot : transform;
            if (animatedTransform == null)
                return;

            closedLocalRotation = animatedTransform.localRotation;
            openLocalRotation = closedLocalRotation * GetOpenDeltaRotation();
        }

        private void ResolveMotionMode()
        {
            if (rotateOwnTransformDirectly)
            {
                pivot = transform;
                motionMode = MotionMode.SelfRotation;
                return;
            }

            if (pivot == null || pivot == transform)
            {
                pivot = transform;
                motionMode = inferHingeFromColliderWhenPivotMissing && TryGetInferredHingeLocalPoint(out inferredHingeLocalPoint)
                    ? MotionMode.InferredHingeOrbit
                    : MotionMode.SelfRotation;
                return;
            }

            if (pivot.IsChildOf(transform))
            {
                motionMode = MotionMode.ChildPivotOrbit;
                return;
            }

            motionMode = transform.IsChildOf(pivot)
                ? MotionMode.ExternalPivotRotation
                : MotionMode.ExternalPivotOrbit;
        }

        private void SetupExternalOrbitHingeIfNeeded()
        {
            if (!Application.isPlaying || motionMode != MotionMode.ExternalPivotOrbit || pivot == null)
                return;

            if (externalOrbitHingeTransform != null)
                return;

            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();

            GameObject hingeObject = new GameObject($"{transform.name}_ExternalHinge");
            externalOrbitHingeTransform = hingeObject.transform;
            externalOrbitHingeTransform.SetPositionAndRotation(pivot.position, pivot.rotation);
            externalOrbitHingeTransform.SetParent(pivot.parent, true);

            transform.SetParent(externalOrbitHingeTransform, true);
        }

        private void SetupVisualProxyIfNeeded()
        {
            if (!Application.isPlaying || !DoorVisualProxyUtility.ShouldUseVisualProxy(transform))
            {
                return;
            }

            if (!rotateOwnTransformDirectly &&
                useDetachedRuntimeProxyForNonUniformScale &&
                DoorDetachedProxyUtility.TryCreate(
                    transform,
                    GetHingeLocalOffset(),
                    this,
                    hiddenRenderers,
                    hiddenColliders,
                    out detachedHingeTransform,
                    out detachedProxyRootObject))
            {
                isUsingDetachedProxy = detachedHingeTransform != null;
                return;
            }

            if (!useVisualProxyForNonUniformScale)
                return;

            visualProxyTransform = DoorVisualProxyUtility.CreateVisualProxy(transform, hiddenRenderers);
            isUsingVisualProxy = visualProxyTransform != null;

            SyncVisualProxyToSource();
        }

        private Vector3 GetHingeLocalOffset()
        {
            return motionMode == MotionMode.ChildPivotOrbit
                ? transform.InverseTransformPoint(pivot.position)
                : motionMode == MotionMode.ExternalPivotOrbit
                    ? transform.InverseTransformPoint(pivot.position)
                : motionMode == MotionMode.InferredHingeOrbit
                    ? inferredHingeLocalPoint
                    : Vector3.zero;
        }

        private void UpdateRotationMotion()
        {
            Transform animatedTransform = GetAnimatedTransform();
            if (animatedTransform == null)
                return;

            Quaternion targetRotation = isOpen ? openLocalRotation : closedLocalRotation;
            if (Quaternion.Angle(animatedTransform.localRotation, targetRotation) <= 0.01f)
                return;

            animatedTransform.localRotation = Quaternion.Slerp(
                animatedTransform.localRotation,
                targetRotation,
                Time.deltaTime * speed);
        }

        private void UpdateDetachedProxyMotion()
        {
            if (detachedHingeTransform == null)
                return;

            Quaternion targetRotation = isOpen ? openLocalRotation : closedLocalRotation;
            if (Quaternion.Angle(detachedHingeTransform.localRotation, targetRotation) <= 0.01f)
                return;

            detachedHingeTransform.localRotation = Quaternion.Slerp(
                detachedHingeTransform.localRotation,
                targetRotation,
                Time.deltaTime * speed);
        }

        private void SyncVisualProxyToSource()
        {
            if (!isUsingVisualProxy || visualProxyTransform == null)
                return;

            visualProxyTransform.SetPositionAndRotation(transform.position, transform.rotation);
        }

        private void UpdateOrbitMotion()
        {
            if ((motionMode == MotionMode.ChildPivotOrbit || motionMode == MotionMode.ExternalPivotOrbit) && pivot == null)
                return;

            Vector3 targetPosition = isOpen ? openWorldPosition : closedWorldPosition;
            Quaternion targetRotation = isOpen ? openWorldRotation : closedWorldRotation;

            if (Vector3.Distance(transform.position, targetPosition) <= 0.0001f &&
                Quaternion.Angle(transform.rotation, targetRotation) <= 0.01f)
                return;

            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * speed),
                Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed));
        }

        private Transform GetAnimatedTransform()
        {
            if (motionMode == MotionMode.ExternalPivotRotation)
                return pivot;

            if (motionMode == MotionMode.ExternalPivotOrbit && externalOrbitHingeTransform != null)
                return externalOrbitHingeTransform;

            return transform;
        }

        private bool TryGetInferredHingeLocalPoint(out Vector3 hingeLocalPoint)
        {
            BoxCollider boxCollider = GetComponent<Collider>() as BoxCollider;
            if (boxCollider == null)
            {
                hingeLocalPoint = default;
                return false;
            }

            Vector3 center = boxCollider.center;
            Vector3 size = boxCollider.size;

            switch (rotationAxis)
            {
                case DoorRotationAxis.X:
                    float minY = center.y - size.y * 0.5f;
                    float maxY = center.y + size.y * 0.5f;
                    float hingeY = Mathf.Abs(minY) <= Mathf.Abs(maxY) ? minY : maxY;
                    hingeLocalPoint = new Vector3(center.x, hingeY, center.z);
                    break;
                case DoorRotationAxis.Z:
                    float minXForZ = center.x - size.x * 0.5f;
                    float maxXForZ = center.x + size.x * 0.5f;
                    float hingeXForZ = Mathf.Abs(minXForZ) <= Mathf.Abs(maxXForZ) ? minXForZ : maxXForZ;
                    hingeLocalPoint = new Vector3(hingeXForZ, center.y, center.z);
                    break;
                default:
                    float minX = center.x - size.x * 0.5f;
                    float maxX = center.x + size.x * 0.5f;
                    float hingeX = Mathf.Abs(minX) <= Mathf.Abs(maxX) ? minX : maxX;
                    hingeLocalPoint = new Vector3(hingeX, center.y, center.z);
                    break;
            }

            return true;
        }

        private Quaternion GetOpenDeltaRotation()
        {
            return Quaternion.AngleAxis(openAngle, GetLocalRotationAxisVector());
        }

        private Vector3 GetLocalRotationAxisVector()
        {
            return rotationAxis switch
            {
                DoorRotationAxis.X => Vector3.right,
                DoorRotationAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }
    }
}
