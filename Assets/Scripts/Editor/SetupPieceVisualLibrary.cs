#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Создаёт/заполняет PieceVisualLibrary префабами из Assets/Prefabs.
/// </summary>
public static class SetupPieceVisualLibrary
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Chess/Setup Piece Visual Library")]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var systems = GameObject.Find("Systems");
        if (systems == null)
        {
            systems = new GameObject("Systems");
            Undo.RegisterCreatedObjectUndo(systems, "Create Systems");
        }

        var lib = Object.FindObjectOfType<PieceVisualLibrary>(true);
        if (lib == null)
        {
            var go = new GameObject("PieceVisualLibrary");
            go.transform.SetParent(systems.transform, false);
            lib = go.AddComponent<PieceVisualLibrary>();
            Undo.RegisterCreatedObjectUndo(go, "Create PieceVisualLibrary");
        }

        var so = new SerializedObject(lib);
        Assign(so, "_whitePawn", "WhitePawn");
        Assign(so, "_whiteRook", "WhiteRook");
        Assign(so, "_whiteKnight", "WhiteKnight");
        Assign(so, "_whiteBishop", "WhiteBishop");
        Assign(so, "_whiteQueen", "WhiteQween");
        Assign(so, "_whiteKing", "WhiteKing");

        Assign(so, "_blackPawn", "BlackPawn");
        Assign(so, "_blackRook", "BlackRook");
        Assign(so, "_blackKnight", "BlackKnigt");
        Assign(so, "_blackBishop", "BlackBishop");
        Assign(so, "_blackQueen", "BlackQween");
        Assign(so, "_blackKing", "Blackking");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lib);

        var setup = Object.FindObjectOfType<ChessSetup>(true);
        if (setup != null)
        {
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
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[SetupPieceVisualLibrary] PieceVisualLibrary + ChessSetup prefab slots filled.");
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
            Debug.LogWarning($"[SetupPieceVisualLibrary] Not found: {path}");
            return;
        }

        prop.objectReferenceValue = unit;
    }
}
#endif
