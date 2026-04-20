using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Interaction.Editor
{
    public static class GothicCabinetOpenableGenerator
    {
        private const string CabinetModelPath = "Assets/Models/Props/furniture/GothicCabinet_01_2k.fbx";
        private const string ReplacementCabinetPrefabPath = "Assets/Prefabs/Objects/Furniture/Cabinet.prefab";
        private const string Room2PrefabPath = "Assets/Prefabs/Rooms/Room 2.prefab";
        private const string LocationDevScenePath = "Assets/Scenes/Location Dev.unity";
        private const string SessionKey = "OutOfSight.CabinetPrefabReplacement.RanV2";

        [InitializeOnLoadMethod]
        private static void EnsureCabinetReplacementOnLoad()
        {
            EditorApplication.delayCall += RunOncePerSession;
        }

        [MenuItem("Tools/OutOfSight/Replace Gothic Cabinets With Furniture Cabinet")]
        private static void ReplaceCabinetsMenu()
        {
            ReplaceCabinetsInRoom2Prefab();
            ReplaceCabinetsInLocationDev(forceSave: true);
        }

        private static void RunOncePerSession()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            ReplaceCabinetsInRoom2Prefab();
            ReplaceCabinetsInLocationDev(forceSave: false);
        }

        private static void ReplaceCabinetsInRoom2Prefab()
        {
            GameObject replacementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReplacementCabinetPrefabPath);
            if (replacementPrefab == null)
            {
                Debug.LogWarning($"Replacement cabinet prefab not found at {ReplacementCabinetPrefabPath}");
                return;
            }

            GameObject roomRoot = PrefabUtility.LoadPrefabContents(Room2PrefabPath);
            if (roomRoot == null)
                return;

            List<Transform> rawCabinets = FindRawCabinets(new[] { roomRoot });
            int replacedCount = ReplaceCabinets(rawCabinets, replacementPrefab, roomRoot.scene);
            int normalizedCount = NormalizeReplacementCabinetScales(new[] { roomRoot });

            if (replacedCount > 0 || normalizedCount > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(roomRoot, Room2PrefabPath);
                Debug.Log($"Updated cabinets in {Room2PrefabPath}: replaced {replacedCount}, normalized {normalizedCount}");
            }

            PrefabUtility.UnloadPrefabContents(roomRoot);
        }

        private static void ReplaceCabinetsInLocationDev(bool forceSave)
        {
            GameObject replacementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReplacementCabinetPrefabPath);
            if (replacementPrefab == null)
                return;

            bool sceneWasOpen = false;
            Scene scene = default;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.path == LocationDevScenePath)
                {
                    scene = loadedScene;
                    sceneWasOpen = true;
                    break;
                }
            }

            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(LocationDevScenePath, OpenSceneMode.Additive);

            List<Transform> rawCabinets = FindRawCabinets(scene.GetRootGameObjects());
            int replacedCount = ReplaceCabinets(rawCabinets, replacementPrefab, scene);
            int normalizedCount = NormalizeReplacementCabinetScales(scene.GetRootGameObjects());

            if (replacedCount > 0 || normalizedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (forceSave || !sceneWasOpen)
                    EditorSceneManager.SaveScene(scene);

                Debug.Log($"Updated cabinets in {LocationDevScenePath}: replaced {replacedCount}, normalized {normalizedCount}");
            }

            if (!sceneWasOpen)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static int ReplaceCabinets(IEnumerable<Transform> rawCabinets, GameObject replacementPrefab, Scene targetScene)
        {
            int replacedCount = 0;

            foreach (Transform oldCabinet in rawCabinets)
            {
                if (oldCabinet == null)
                    continue;

                Transform parent = oldCabinet.parent;
                int siblingIndex = oldCabinet.GetSiblingIndex();
                Vector3 localPosition = oldCabinet.localPosition;
                Quaternion localRotation = oldCabinet.localRotation;
                string oldName = oldCabinet.name;

                GameObject newCabinet = PrefabUtility.InstantiatePrefab(replacementPrefab, targetScene) as GameObject;
                if (newCabinet == null)
                    continue;

                Transform newTransform = newCabinet.transform;
                newCabinet.name = oldName;
                newTransform.SetParent(parent, false);
                newTransform.SetSiblingIndex(siblingIndex);
                newTransform.localPosition = localPosition;
                newTransform.localRotation = localRotation;
                newTransform.localScale = Vector3.one;

                Object.DestroyImmediate(oldCabinet.gameObject);
                replacedCount++;
            }

            return replacedCount;
        }

        private static List<Transform> FindRawCabinets(IEnumerable<GameObject> rootObjects)
        {
            List<Transform> cabinets = new List<Transform>();
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CabinetModelPath);
            if (modelAsset == null)
                return cabinets;

            foreach (GameObject rootObject in rootObjects)
            {
                Transform[] transforms = rootObject.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null)
                        continue;

                    if (!candidate.name.StartsWith("GothicCabinet_01_2k"))
                        continue;

                    if (IsInsideReplacementCabinet(candidate))
                        continue;

                    Object sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject);
                    if (sourceObject == null)
                        continue;

                    string sourcePath = AssetDatabase.GetAssetPath(sourceObject);
                    if (sourcePath != CabinetModelPath)
                        continue;

                    if (candidate.parent != null &&
                        candidate.parent.name.StartsWith("GothicCabinet_01_2k") &&
                        PrefabUtility.GetCorrespondingObjectFromSource(candidate.parent.gameObject) == modelAsset)
                    {
                        continue;
                    }

                    cabinets.Add(candidate);
                }
            }

            return cabinets;
        }

        private static int NormalizeReplacementCabinetScales(IEnumerable<GameObject> rootObjects)
        {
            int normalizedCount = 0;
            HashSet<GameObject> visitedRoots = new HashSet<GameObject>();

            foreach (GameObject rootObject in rootObjects)
            {
                Transform[] transforms = rootObject.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate == null)
                        continue;

                    GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(candidate.gameObject);
                    if (instanceRoot == null || !visitedRoots.Add(instanceRoot))
                        continue;

                    Object sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                    if (sourceRoot == null || AssetDatabase.GetAssetPath(sourceRoot) != ReplacementCabinetPrefabPath)
                        continue;

                    if (instanceRoot.transform.localScale != Vector3.one)
                    {
                        instanceRoot.transform.localScale = Vector3.one;
                        normalizedCount++;
                    }
                }
            }

            return normalizedCount;
        }

        private static bool IsInsideReplacementCabinet(Transform candidate)
        {
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(candidate.gameObject);
            if (instanceRoot == null)
                return false;

            Object sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            if (sourceRoot == null)
                return false;

            return AssetDatabase.GetAssetPath(sourceRoot) == ReplacementCabinetPrefabPath;
        }
    }
}
