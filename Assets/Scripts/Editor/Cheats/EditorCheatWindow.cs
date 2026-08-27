using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public class EditorCheatWindow : EditorWindow
{
    EditorControls _controls;
    bool _bound;

    [MenuItem("Netologia/Windows/Editor Cheat Window")]
    public static void Open()
    {
        GetWindow<EditorCheatWindow>("Editor Cheat Window");
    }

    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        if (EditorApplication.isPlaying)
            BindPlayModeInput();
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnbindPlayModeInput();
    }

    void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            BindPlayModeInput();
        else if (state == PlayModeStateChange.ExitingPlayMode)
            UnbindPlayModeInput();
    }

    void BindPlayModeInput()
    {
        if (_bound)
            return;

        _controls ??= new EditorControls();
        var cheats = _controls.Cheats;
        cheats.NextTurn.performed += OnNextTurn;
        cheats.Kill.performed += OnKill;
        // Undo (Ctrl+Z) всегда слушает BattleController в Play Mode — здесь не дублируем.
        _controls.Enable();
        _bound = true;
    }

    void UnbindPlayModeInput()
    {
        if (!_bound || _controls == null)
            return;

        var cheats = _controls.Cheats;
        cheats.NextTurn.performed -= OnNextTurn;
        cheats.Kill.performed -= OnKill;
        _controls.Disable();
        _bound = false;
    }

    void OnDestroy()
    {
        UnbindPlayModeInput();
        if (_controls == null)
            return;
        _controls.Dispose();
        _controls = null;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1 и 2 — только пока это окно открыто и идёт Play Mode.\n" +
            "1 — NextTurn (передать ход)\n" +
            "2 — Kill (убить подсвеченного врага при выбранной своей фигуре)\n" +
            "Ctrl+Z — отмена хода, всегда в Play Mode (окно не нужно)",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("NextTurn"))
                TryRun(c => c.CheatNextTurn());
            if (GUILayout.Button("Kill"))
                TryRun(c => c.CheatKillSelectedEnemy());
            if (GUILayout.Button("Undo"))
                TryRun(c => c.CheatUndo());
        }

        if (EditorApplication.isPlaying
            && Event.current != null
            && Event.current.type == EventType.KeyDown
            && Event.current.keyCode == KeyCode.Z
            && Event.current.control)
        {
            Event.current.Use();
        }
    }

    void OnNextTurn(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;
        TryRun(c => c.CheatNextTurn());
    }

    void OnKill(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;
        TryRun(c => c.CheatKillSelectedEnemy());
    }

    static void TryRun(System.Action<ICheatCommands> action)
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            return;

        var cheats = ResolveCheats();
        if (cheats == null)
            return;
        if (cheats.IsBusy)
            return;

        action(cheats);
    }

    static ICheatCommands ResolveCheats()
    {
        try
        {
            var contexts = Object.FindObjectsByType<SceneContext>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ctx in contexts)
            {
                if (ctx == null || ctx.Container == null)
                    continue;
                var resolved = ctx.Container.TryResolve<ICheatCommands>();
                if (resolved != null)
                    return resolved;
            }

            // Fallback, если SceneContext ещё не поднялся.
            if (ProjectContext.HasInstance && ProjectContext.Instance.Container != null)
            {
                var resolved = ProjectContext.Instance.Container.TryResolve<ICheatCommands>();
                if (resolved != null)
                    return resolved;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EditorCheat] Не удалось резолвить ICheatCommands: " + e.Message);
            return null;
        }

        Debug.LogWarning(
            "[EditorCheat] ICheatCommands нет в Zenject SceneContext. " +
            "Открой GameScene в Play Mode.");
        return null;
    }
}
