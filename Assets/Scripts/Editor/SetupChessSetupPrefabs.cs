#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupChessSetupPrefabs
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Chess/Fill ChessSetup Prefabs")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var setup = Object.FindObjectOfType<ChessSetup>(true);
        if (setup == null)
        {
            Debug.LogError("[SetupChessSetupPrefabs] ChessSetup не найден на сцене.");
            return;
        }

        var sso = new SerializedObject(setup);
        Assign(sso, "_whitePawn", "WhitePawn");
        Assign(sso, "_whiteRook", "WhiteRook");
        Assign(sso, "_whiteKnight", "WhiteKnight");
        Assign(sso, "_whiteBishop", "WhiteBishop");
        Assign(sso, "_whiteQueen", "WhiteQween");
        Assign(sso, "_whiteKing", "WhiteKing");
        Assign(sso, "_blackPawn", "BlackPawn");
        Assign(sso, "_blackRook", "BlackRook");
        Assign(sso, "_blackKnight", "BlackKnigt");
        Assign(sso, "_blackBishop", "BlackBishop");
        Assign(sso, "_blackQueen", "BlackQween");
        Assign(sso, "_blackKing", "Blackking");
        sso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(setup);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[SetupChessSetupPrefabs] ChessSetup prefab slots filled.");
    }

    private static void Assign(SerializedObject so, string property, string prefabName)
    {
        var prop = so.FindProperty(property);
        if (prop == null)
            return;

        var path = $"{PrefabFolder}/{prefabName}.prefab";
        var unit = AssetDatabase.LoadAssetAtPath<Unit>(path);
        if (unit == null)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
                unit = go.GetComponent<Unit>();
        }

        if (unit == null)
        {
            Debug.LogWarning($"[SetupChessSetupPrefabs] Not found: {path}");
            return;
        }

        prop.objectReferenceValue = unit;
    }
}
#endif
