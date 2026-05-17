using System.Collections.Generic;
using UnityEngine;

public class ObjectRandomSpawner : MonoBehaviour
{
    [Header("Prefabs")]

    [SerializeField] private GameObject fusePrefab;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameObject tapePrefab;

    [Header("Monster detector prefabs")]
    [Tooltip("Сюда нужно положить 2 разных prefab устройства обнаружения монстра.")]
    [SerializeField] private GameObject[] monsterDetectorPrefabs;

    [Header("Spawn roots")]

    [SerializeField] private Transform fuseSpawnsRoot;
    [SerializeField] private Transform buttonSpawnsRoot;
    [SerializeField] private Transform tapeSpawnsRoot;
    [SerializeField] private Transform monsterDetectorSpawnsRoot;

    [Header("Amounts")]

    [SerializeField] private int fuseCount = 4;
    [SerializeField] private int buttonCount = 1;
    [SerializeField] private int tapeCount = 1;

    [Header("Settings")]

    [SerializeField] private bool useSpawnPointRotation = true;
    [SerializeField] private bool printDebugToConsole = true;

    private static bool layoutGenerated = false;

    private static List<int> selectedFuseIndexes = new List<int>();
    private static List<int> selectedButtonIndexes = new List<int>();
    private static List<int> selectedTapeIndexes = new List<int>();
    private static List<int> selectedDetectorPointIndexes = new List<int>();

    private void Awake()
    {
        AutoFindSpawnRootsIfNeeded();
    }

    private void Start()
    {
        if (!layoutGenerated)
        {
            GenerateLayoutForThisPlaythrough();
            layoutGenerated = true;
        }

        SpawnSavedLayout();
    }

    private void AutoFindSpawnRootsIfNeeded()
    {
        if (fuseSpawnsRoot == null)
            fuseSpawnsRoot = transform.Find("FuseSpawns");

        if (buttonSpawnsRoot == null)
            buttonSpawnsRoot = transform.Find("ButtonSpawn");

        if (tapeSpawnsRoot == null)
            tapeSpawnsRoot = transform.Find("TapeSpawn");

        if (monsterDetectorSpawnsRoot == null)
            monsterDetectorSpawnsRoot = transform.Find("MonsterDetectorspawn");
    }

    private void GenerateLayoutForThisPlaythrough()
    {
        selectedFuseIndexes = PickRandomIndexes(GetChildPoints(fuseSpawnsRoot).Count, fuseCount);
        selectedButtonIndexes = PickRandomIndexes(GetChildPoints(buttonSpawnsRoot).Count, buttonCount);
        selectedTapeIndexes = PickRandomIndexes(GetChildPoints(tapeSpawnsRoot).Count, tapeCount);

        int detectorCount = monsterDetectorPrefabs == null ? 0 : monsterDetectorPrefabs.Length;
        selectedDetectorPointIndexes = PickRandomIndexes(
            GetChildPoints(monsterDetectorSpawnsRoot).Count,
            detectorCount
        );
    }

    private void SpawnSavedLayout()
    {
        SpawnRepeatedPrefabGroup(
            prefab: fusePrefab,
            root: fuseSpawnsRoot,
            selectedIndexes: selectedFuseIndexes,
            itemNameForDebug: "Предохранитель"
        );

        SpawnRepeatedPrefabGroup(
            prefab: buttonPrefab,
            root: buttonSpawnsRoot,
            selectedIndexes: selectedButtonIndexes,
            itemNameForDebug: "Кнопка"
        );

        SpawnRepeatedPrefabGroup(
            prefab: tapePrefab,
            root: tapeSpawnsRoot,
            selectedIndexes: selectedTapeIndexes,
            itemNameForDebug: "Изолента"
        );

        SpawnMonsterDetectors();
    }

    private void SpawnRepeatedPrefabGroup(
        GameObject prefab,
        Transform root,
        List<int> selectedIndexes,
        string itemNameForDebug
    )
    {
        if (prefab == null)
        {
            Debug.LogWarning($"{itemNameForDebug}: prefab не указан.");
            return;
        }

        List<Transform> points = GetChildPoints(root);

        if (points.Count == 0)
        {
            Debug.LogWarning($"{itemNameForDebug}: нет точек спавна.");
            return;
        }

        for (int i = 0; i < selectedIndexes.Count; i++)
        {
            int pointIndex = selectedIndexes[i];

            if (pointIndex < 0 || pointIndex >= points.Count)
                continue;

            Transform point = points[pointIndex];

            SpawnPrefab(prefab, point);

            if (printDebugToConsole)
            {
                Debug.Log(
                    $"{itemNameForDebug} {i + 1} -> {point.name} " +
                    $"на позиции {GetPointNumber(point.name)} | " +
                    $"координаты: {FormatVector3(point.position)}"
                );
            }
        }
    }

    private void SpawnMonsterDetectors()
    {
        if (monsterDetectorPrefabs == null || monsterDetectorPrefabs.Length == 0)
        {
            Debug.LogWarning("Устройства обнаружения монстра: prefab'ы не указаны.");
            return;
        }

        List<Transform> points = GetChildPoints(monsterDetectorSpawnsRoot);

        if (points.Count == 0)
        {
            Debug.LogWarning("Устройства обнаружения монстра: нет точек спавна.");
            return;
        }

        int count = Mathf.Min(monsterDetectorPrefabs.Length, selectedDetectorPointIndexes.Count);

        for (int i = 0; i < count; i++)
        {
            GameObject detectorPrefab = monsterDetectorPrefabs[i];

            if (detectorPrefab == null)
            {
                Debug.LogWarning($"Устройство обнаружения монстра {i + 1}: prefab не указан.");
                continue;
            }

            int pointIndex = selectedDetectorPointIndexes[i];

            if (pointIndex < 0 || pointIndex >= points.Count)
                continue;

            Transform point = points[pointIndex];

            SpawnPrefab(detectorPrefab, point);

            if (printDebugToConsole)
            {
                Debug.Log(
                    $"Устройство обнаружения монстра {i + 1} [{detectorPrefab.name}] -> {point.name} " +
                    $"на позиции {GetPointNumber(point.name)} | " +
                    $"координаты: {FormatVector3(point.position)}"
                );
            }
        }
    }

    private void SpawnPrefab(GameObject prefab, Transform point)
    {
        Quaternion rotation = useSpawnPointRotation ? point.rotation : Quaternion.identity;

        Instantiate(
            prefab,
            point.position,
            rotation
        );
    }

    private List<Transform> GetChildPoints(Transform root)
    {
        List<Transform> points = new List<Transform>();

        if (root == null)
            return points;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child != null)
                points.Add(child);
        }

        return points;
    }

    private List<int> PickRandomIndexes(int totalCount, int amount)
    {
        List<int> indexes = new List<int>();

        for (int i = 0; i < totalCount; i++)
        {
            indexes.Add(i);
        }

        Shuffle(indexes);

        int count = Mathf.Min(amount, indexes.Count);

        List<int> result = new List<int>();

        for (int i = 0; i < count; i++)
        {
            result.Add(indexes[i]);
        }

        return result;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);

            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private string GetPointNumber(string pointName)
    {
        int underscoreIndex = pointName.LastIndexOf('_');

        if (underscoreIndex < 0 || underscoreIndex >= pointName.Length - 1)
            return pointName;

        return pointName.Substring(underscoreIndex + 1);
    }

    private string FormatVector3(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }

    public static void ResetForNewPlaythrough()
    {
        layoutGenerated = false;

        selectedFuseIndexes.Clear();
        selectedButtonIndexes.Clear();
        selectedTapeIndexes.Clear();
        selectedDetectorPointIndexes.Clear();
    }
}