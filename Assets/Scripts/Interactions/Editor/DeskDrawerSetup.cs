using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Interaction.Editor
{
    public static class DeskDrawerSetup
    {
        private const string DeskModelPath   = "Assets/Models/Props/furniture/metal_office_desk_2k.fbx";
        private const string Room2PrefabPath = "Assets/Prefabs/Rooms/Room 2.prefab";
        private const string SessionKey      = "OutOfSight.DeskDrawerSetup.RanV5";

        // ─── Меню ────────────────────────────────────────────────────────────────

        [MenuItem("Tools/OutOfSight/Desk Drawers/Setup In Open Scenes")]
        private static void MenuSetupOpenScenes()
        {
            int total = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                int n = ProcessRoots(s.GetRootGameObjects());
                if (n > 0) EditorSceneManager.MarkSceneDirty(s);
                total += n;
            }
            Debug.Log($"[DeskDrawerSetup] Processed {total} drawer(s) in open scenes.");
        }

        [MenuItem("Tools/OutOfSight/Desk Drawers/Setup In Room 2 Prefab")]
        private static void MenuSetupRoom2()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Room2PrefabPath);
            if (root == null) { Debug.LogError("[DeskDrawerSetup] Cannot load Room 2 prefab."); return; }
            int n = ProcessRoots(new[] { root });
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, Room2PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[DeskDrawerSetup] Processed {n} drawer(s) in Room 2.");
        }

        // ─── Авто-запуск ─────────────────────────────────────────────────────────

        [InitializeOnLoadMethod]
        private static void Init() => EditorApplication.delayCall += AutoRun;

        private static void AutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                int n = ProcessRoots(s.GetRootGameObjects());
                if (n > 0) EditorSceneManager.MarkSceneDirty(s);
            }
        }

        // ─── Логика ──────────────────────────────────────────────────────────────

        private static int ProcessRoots(IEnumerable<GameObject> roots)
        {
            int count = 0;
            foreach (var root in roots)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (IsDeskRoot(t))
                        count += SetupDesk(t);
            return count;
        }

        private static bool IsDeskRoot(Transform t)
        {
            if (!t.name.StartsWith("metal_office_desk_2k", System.StringComparison.OrdinalIgnoreCase))
                return false;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
            return src != null && AssetDatabase.GetAssetPath(src) == DeskModelPath;
        }

        private static int SetupDesk(Transform deskRoot)
        {
            int count = 0;
            foreach (Transform child in deskRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == deskRoot) continue;

                // Убираем коллайдеры с handle-узлов (ручки ящиков — не интерактивны)
                if (child.name.IndexOf("handle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (var col in child.GetComponents<Collider>())
                        Undo.DestroyObjectImmediate(col);
                    continue;
                }

                if (!child.name.StartsWith("metal_office_desk_drawer", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                SetupDrawer(child);
                count++;
            }
            return count;
        }

        private static void SetupDrawer(Transform drawer)
        {
            // 1. Удаляем все старые коллайдеры
            foreach (var col in drawer.GetComponents<Collider>())
                Undo.DestroyObjectImmediate(col);

            // 2. Берём bounds меша
            var meshFilter = drawer.GetComponent<MeshFilter>();
            Bounds bounds = meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.bounds
                : new Bounds(Vector3.zero, Vector3.one * 0.01f);

            // 3. Добавляем BoxCollider точно по мешу
            var box = Undo.AddComponent<BoxCollider>(drawer.gameObject);
            box.center = bounds.center;
            box.size   = bounds.size;

            // 4. Удаляем старый DrawerInteractable если есть
            var oldDrawer = drawer.GetComponent<DrawerInteractable>();
            if (oldDrawer != null)
                Undo.DestroyObjectImmediate(oldDrawer);

            // 5. Добавляем DrawerInteractable
            // openDistance — в мировых метрах (WorldToLocal пересчитает в Start)
            // speed — в локальных единицах FBX-узла (= openDistance / lossyScale / желаемое_время)
            const float openDistWorld = 0.8f;
            const float openTimeSec   = 0.8f; // ящик открывается за 0.8 секунды
            Vector3 ls = drawer.lossyScale;
            float avgScale = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
            float localDist  = avgScale > 0.0001f ? openDistWorld / avgScale : openDistWorld;
            float localSpeed = localDist / openTimeSec;

            var di = Undo.AddComponent<DrawerInteractable>(drawer.gameObject);
            using var so = new SerializedObject(di);
            so.FindProperty("openDistance").floatValue    = openDistWorld;
            so.FindProperty("openDirection").vector3Value = new Vector3(0, 0, 1);
            so.FindProperty("speed").floatValue           = localSpeed;
            so.FindProperty("openPrompt").stringValue     = "Open";
            so.FindProperty("closePrompt").stringValue    = "Close";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(drawer.gameObject);
        }
    }
}
