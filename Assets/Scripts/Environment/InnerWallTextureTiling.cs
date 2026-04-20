using UnityEngine;

namespace OutOfSight.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class InnerWallTextureTiling : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float horizontalMetersPerTile = 1f;
        [SerializeField, Min(0.01f)] private float verticalMetersPerTile = 1f;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private Vector2 tilingMultiplier = Vector2.one;

        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int BumpMapStId = Shader.PropertyToID("_BumpMap_ST");

        private void OnEnable()
        {
            ApplyTiling();
        }

        private void OnValidate()
        {
            ApplyTiling();
        }

        [ContextMenu("Refresh Inner Wall Tiling")]
        public void ApplyTiling()
        {
            var renderers = GetComponentsInChildren<MeshRenderer>(includeInactiveRenderers);
            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                ApplyTiling(renderer);
            }
        }

        private void ApplyTiling(MeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            var meshSize = meshFilter.sharedMesh.bounds.size;
            var lossyScale = renderer.transform.lossyScale;
            var worldSize = new Vector3(
                Mathf.Abs(meshSize.x * lossyScale.x),
                Mathf.Abs(meshSize.y * lossyScale.y),
                Mathf.Abs(meshSize.z * lossyScale.z));

            var horizontalSize = Mathf.Max(worldSize.x, worldSize.z);
            var verticalSize = worldSize.y;
            var tiling = new Vector2(
                (horizontalSize / Mathf.Max(0.01f, horizontalMetersPerTile)) * Mathf.Max(0.01f, tilingMultiplier.x),
                (verticalSize / Mathf.Max(0.01f, verticalMetersPerTile)) * Mathf.Max(0.01f, tilingMultiplier.y));

            var sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                return;
            }

            for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                var sharedMaterial = sharedMaterials[materialIndex];
                if (sharedMaterial == null)
                {
                    continue;
                }

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock, materialIndex);

                ApplyTextureTransform(sharedMaterial, "_BaseMap", BaseMapStId, tiling, propertyBlock);
                ApplyTextureTransform(sharedMaterial, "_MainTex", MainTexStId, tiling, propertyBlock);
                ApplyTextureTransform(sharedMaterial, "_BumpMap", BumpMapStId, tiling, propertyBlock);

                renderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }

        private static void ApplyTextureTransform(
            Material material,
            string texturePropertyName,
            int textureStPropertyId,
            Vector2 tiling,
            MaterialPropertyBlock propertyBlock)
        {
            if (!material.HasProperty(texturePropertyName))
            {
                return;
            }

            var baseScale = material.GetTextureScale(texturePropertyName);
            var baseOffset = material.GetTextureOffset(texturePropertyName);
            propertyBlock.SetVector(
                textureStPropertyId,
                new Vector4(baseScale.x * tiling.x, baseScale.y * tiling.y, baseOffset.x, baseOffset.y));
        }
    }
}
