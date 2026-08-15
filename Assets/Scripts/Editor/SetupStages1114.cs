#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Меню: добавляет на GameScene объекты этапов 11–14 и назначает GameSettings.
/// </summary>
public static class SetupStages1114
{
    private const string GameSettingsPath = "Assets/ScriptableObjects/GameSettings.asset";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Chess/Setup Stages 11-14 (Bootstrap, Settings, Turn UI)")]
    public static void Setup()
    {
        EnsureGameSettingsAsset();

        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var systems = GameObject.Find("Systems");
        if (systems == null)
        {
            systems = new GameObject("Systems");
            Undo.RegisterCreatedObjectUndo(systems, "Create Systems");
        }

        // GameBootstrap
        var bootstrap = Object.FindObjectOfType<GameBootstrap>();
        if (bootstrap == null)
        {
            var go = new GameObject("GameBootstrap");
            go.transform.SetParent(systems.transform, false);
            bootstrap = go.AddComponent<GameBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Create GameBootstrap");
        }

        // TurnInfoView under Canvas
        var turnView = Object.FindObjectOfType<TurnInfoView>();
        if (turnView == null)
        {
            var canvas = EnsureCanvas();
            var turnGo = new GameObject("TurnInfoView", typeof(RectTransform));
            turnGo.transform.SetParent(canvas.transform, false);
            var rt = turnGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            turnView = turnGo.AddComponent<TurnInfoView>();
            Undo.RegisterCreatedObjectUndo(turnGo, "Create TurnInfoView");
        }

        EnsurePromotionPanel();

        // SceneInstaller → GameSettings reference
        var installer = Object.FindObjectOfType<SceneInstaller>();
        if (installer != null)
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(GameSettingsPath);
            var so = new SerializedObject(installer);
            var prop = so.FindProperty("_gameSettings");
            if (prop != null)
            {
                prop.objectReferenceValue = settings;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(installer);
            }
        }
        else
        {
            Debug.LogWarning("[SetupStages1114] SceneInstaller не найден на сцене.");
        }

        // ChessSetup: по умолчанию не спавним (фигуры в сцене)
        var setup = Object.FindObjectOfType<ChessSetup>();
        if (setup != null)
        {
            var so = new SerializedObject(setup);
            var spawn = so.FindProperty("_spawnOnStart");
            var clear = so.FindProperty("_clearExistingUnits");
            if (spawn != null) spawn.boolValue = false;
            if (clear != null) clear.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setup);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[SetupStages1114] Готово: GameBootstrap, TurnInfoView, PromotionPanel, GameSettings → SceneInstaller. " +
            "Сцена сохранена.");
    }

    [MenuItem("Tools/Chess/Setup Promotion Panel (pawn UI)")]
    public static void SetupPromotionOnly()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        EnsurePromotionPanel();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupStages1114] PromotionPanel готов на GameScene.");
    }

    private static Canvas EnsureCanvas()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
            return canvas;

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        return canvas;
    }

    private static void EnsurePromotionPanel()
    {
        var panel = Object.FindObjectOfType<PromotionPanel>(true);
        if (panel != null)
            return;

        var canvas = EnsureCanvas();
        var go = new GameObject("PromotionPanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.AddComponent<PromotionPanel>();
        Undo.RegisterCreatedObjectUndo(go, "Create PromotionPanel");
    }

    private static void EnsureGameSettingsAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameSettings>(GameSettingsPath);
        if (existing != null)
            return;

        var dir = Path.GetDirectoryName(GameSettingsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var asset = ScriptableObject.CreateInstance<GameSettings>();
        asset.cellSize = 1f;
        asset.unitMoveSpeed = 3f;
        asset.randomFirstPlayer = false;
        asset.firstTeam = Team.White;
        asset.restartHoldDuration = 1.5f;
        asset.turnLabelFormat = "Ход: {0}";

        AssetDatabase.CreateAsset(asset, GameSettingsPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SetupStages1114] Создан {GameSettingsPath}");
    }
}
#endif
