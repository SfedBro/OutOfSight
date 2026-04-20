using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Interaction
{
    internal static class DoorDetachedProxyUtility
    {
        public static bool TryCreate(
            Transform sourceRoot,
            Vector3 hingeLocalOffset,
            MonoBehaviour interactionSource,
            List<Renderer> hiddenRenderers,
            List<Collider> hiddenColliders,
            out Transform hingeRoot,
            out GameObject detachedRootObject)
        {
            hingeRoot = null;
            detachedRootObject = null;

            if (sourceRoot == null || interactionSource == null)
                return false;

            detachedRootObject = new GameObject($"{sourceRoot.name}_DetachedDoorProxy");
            SceneManager.MoveGameObjectToScene(detachedRootObject, sourceRoot.gameObject.scene);

            Vector3 hingeWorldPosition = sourceRoot.TransformPoint(hingeLocalOffset);
            detachedRootObject.transform.SetPositionAndRotation(hingeWorldPosition, sourceRoot.rotation);
            detachedRootObject.transform.localScale = Vector3.one;

            GameObject bodyRoot = new GameObject(sourceRoot.name);
            bodyRoot.layer = sourceRoot.gameObject.layer;
            bodyRoot.transform.SetPositionAndRotation(sourceRoot.position, sourceRoot.rotation);
            bodyRoot.transform.localScale = sourceRoot.lossyScale;
            bodyRoot.transform.SetParent(detachedRootObject.transform, true);

            DoorInteractableForwarder forwarder = bodyRoot.AddComponent<DoorInteractableForwarder>();
            forwarder.Initialize(interactionSource);

            CloneNodeComponents(sourceRoot, bodyRoot, hiddenRenderers, hiddenColliders);

            for (int i = 0; i < sourceRoot.childCount; i++)
                CloneHierarchyRecursive(sourceRoot.GetChild(i), bodyRoot.transform, hiddenRenderers, hiddenColliders);

            hingeRoot = detachedRootObject.transform;
            return true;
        }

        public static void RestoreHiddenColliders(List<Collider> hiddenColliders)
        {
            if (hiddenColliders == null)
                return;

            for (int i = 0; i < hiddenColliders.Count; i++)
            {
                if (hiddenColliders[i] != null)
                    hiddenColliders[i].enabled = true;
            }
        }

        private static void CloneHierarchyRecursive(
            Transform source,
            Transform targetParent,
            List<Renderer> hiddenRenderers,
            List<Collider> hiddenColliders)
        {
            GameObject clone = new GameObject(source.name);
            clone.layer = source.gameObject.layer;
            clone.transform.SetParent(targetParent, false);
            clone.transform.localPosition = source.localPosition;
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;

            CloneNodeComponents(source, clone, hiddenRenderers, hiddenColliders);

            for (int i = 0; i < source.childCount; i++)
                CloneHierarchyRecursive(source.GetChild(i), clone.transform, hiddenRenderers, hiddenColliders);
        }

        private static void CloneNodeComponents(
            Transform source,
            GameObject clone,
            List<Renderer> hiddenRenderers,
            List<Collider> hiddenColliders)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter != null && sourceFilter.sharedMesh != null)
            {
                MeshFilter cloneFilter = clone.AddComponent<MeshFilter>();
                cloneFilter.sharedMesh = sourceFilter.sharedMesh;
            }

            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceRenderer != null)
            {
                MeshRenderer cloneRenderer = clone.AddComponent<MeshRenderer>();
                cloneRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                cloneRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                cloneRenderer.receiveShadows = sourceRenderer.receiveShadows;
                cloneRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                cloneRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                cloneRenderer.motionVectorGenerationMode = sourceRenderer.motionVectorGenerationMode;
                cloneRenderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;

                hiddenRenderers?.Add(sourceRenderer);
                sourceRenderer.enabled = false;
            }

            CopyCollider<BoxCollider>(source, clone, hiddenColliders, (src, dst) =>
            {
                dst.center = src.center;
                dst.size = src.size;
            });

            CopyCollider<SphereCollider>(source, clone, hiddenColliders, (src, dst) =>
            {
                dst.center = src.center;
                dst.radius = src.radius;
            });

            CopyCollider<CapsuleCollider>(source, clone, hiddenColliders, (src, dst) =>
            {
                dst.center = src.center;
                dst.radius = src.radius;
                dst.height = src.height;
                dst.direction = src.direction;
            });

            CopyCollider<MeshCollider>(source, clone, hiddenColliders, (src, dst) =>
            {
                dst.sharedMesh = src.sharedMesh;
                dst.convex = src.convex;
                dst.cookingOptions = src.cookingOptions;
            });
        }

        private static void CopyCollider<TCollider>(
            Transform source,
            GameObject clone,
            List<Collider> hiddenColliders,
            System.Action<TCollider, TCollider> copyValues)
            where TCollider : Collider
        {
            TCollider sourceCollider = source.GetComponent<TCollider>();
            if (sourceCollider == null)
                return;

            TCollider cloneCollider = clone.AddComponent<TCollider>();
            cloneCollider.isTrigger = sourceCollider.isTrigger;
            cloneCollider.contactOffset = sourceCollider.contactOffset;
            cloneCollider.sharedMaterial = sourceCollider.sharedMaterial;
            cloneCollider.enabled = sourceCollider.enabled;

            copyValues(sourceCollider, cloneCollider);

            hiddenColliders?.Add(sourceCollider);
            sourceCollider.enabled = false;
        }
    }
}
