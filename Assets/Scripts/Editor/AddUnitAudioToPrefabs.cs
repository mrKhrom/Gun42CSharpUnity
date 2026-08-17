#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Добавляет UnitAudio на все префабы с Unit (клипы заполняются вручную).
public static class AddUnitAudioToPrefabs
{
    [MenuItem("Tools/Chess/Add UnitAudio to Unit Prefabs")]
    public static void Add()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        int added = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var unit = root.GetComponent<Unit>();
                if (unit == null)
                    continue;

                if (root.GetComponent<UnitAudio>() != null)
                    continue;

                root.AddComponent<UnitAudio>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                added++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[UnitAudio] Добавлен на префабов: {added}");
    }
}
#endif
