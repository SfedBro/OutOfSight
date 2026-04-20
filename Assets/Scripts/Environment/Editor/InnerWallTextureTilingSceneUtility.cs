#if UNITY_EDITOR
using System.Collections.Generic;
using OutOfSight.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfSight.Environment.Editor
{
    public static class InnerWallTextureTilingSceneUtility
    {
        private const string SessionKey = "OutOfSight.InnerWallTextureTiling.OpenScenes.V1";

        [InitializeOnLoadMethod]
        private static void ApplyToKnownOpenScenesOnce()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            var changed = ApplyToLoadedScenes(scene =>
                scene.name == "Location" || scene.name == "Location Dev");

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }

            SessionState.SetBool(SessionKey, true);
        }

        [MenuItem("Tools/OutOfSight/Fix Inner Wall Tiling In Open Scenes")]
        public static void ApplyToOpenScenesMenu()
        {
            var changed = ApplyToLoadedScenes(_ => true);
            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }

        private static bool ApplyToLoadedScenes(System.Predicate<Scene> sceneFilter)
        {
            var anyChanged = false;

            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded || !sceneFilter(scene))
                {
                    continue;
                }

                if (ApplyToScene(scene))
                {
                    anyChanged = true;
                }
            }

            return anyChanged;
        }

        private static bool ApplyToScene(Scene scene)
        {
            var changed = false;
            var roots = scene.GetRootGameObjects();
            var allTransforms = new List<Transform>(256);

            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                allTransforms.Clear();
                root.GetComponentsInChildren(true, allTransforms);
                foreach (var transform in allTransforms)
                {
                    if (transform == null || transform.name != "Inner Walls")
                    {
                        continue;
                    }

                    var existing = transform.GetComponent<InnerWallTextureTiling>();
                    if (existing == null)
                    {
                        existing = Undo.AddComponent<InnerWallTextureTiling>(transform.gameObject);
                        changed = true;
                    }

                    existing.ApplyTiling();
                    EditorUtility.SetDirty(existing);
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return changed;
        }
    }
}
#endif
