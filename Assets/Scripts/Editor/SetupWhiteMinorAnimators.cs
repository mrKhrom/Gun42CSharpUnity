using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates AnimatorControllers for White minor pieces' child models and assigns them in GameScene:
///   WhitePawn   → Militia_Final.fbx
///   WhiteRook   → Footman_Final.fbx
///   WhiteKnight → Knight.fbx (reuses existing Knight.controller)
///   WhiteBishop → Rifleman.fbx
/// Menu: Tools/Setup White Minor Animators
/// Auto-runs once after compile if any controller is missing.
/// </summary>
public static class SetupWhiteMinorAnimators
{
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    const string ReportPath = "Assets/Models/Humans/WHITE_MINOR_ANIMATORS_REPORT.txt";
    const string AutoFlagKey = "SetupWhiteMinorAnimators.AutoRan";

    struct PieceDef
    {
        public string ParentName;
        public string ChildContains; // substring match on child model name
        public string FbxPath;
        public string ControllerPath;
        public bool RebuildController; // false = use existing (Knight)
    }

    static readonly PieceDef[] Pieces =
    {
        new PieceDef
        {
            ParentName = "WhitePawn",
            ChildContains = "Militia",
            FbxPath = "Assets/Models/Humans/PeasantMilitia/Militia_Final.fbx",
            ControllerPath = "Assets/Models/Humans/PeasantMilitia/Militia.controller",
            RebuildController = true
        },
        new PieceDef
        {
            ParentName = "WhiteRook",
            ChildContains = "Footman",
            FbxPath = "Assets/Models/Humans/Footman/Footman_Final.fbx",
            ControllerPath = "Assets/Models/Humans/Footman/Footman.controller",
            RebuildController = true
        },
        new PieceDef
        {
            ParentName = "WhiteKnight",
            ChildContains = "Knight",
            FbxPath = "Assets/Models/Humans/Knight/Knight.fbx",
            ControllerPath = "Assets/Models/Humans/Knight/Knight.controller",
            RebuildController = false // already set up by KnightModelPostprocessor
        },
        new PieceDef
        {
            ParentName = "WhiteBishop",
            ChildContains = "Rifleman",
            FbxPath = "Assets/Models/Humans/Rifleman/Rifleman.fbx",
            ControllerPath = "Assets/Models/Humans/Rifleman/Rifleman.controller",
            RebuildController = true
        },
    };

    // Preferred default state names (first match wins)
    static readonly string[] DefaultStatePriority =
    {
        "Stand_1", "Stand_Ready", "Stand", "Stand_2",
        "Idle_02", "Idle_6", "Idle_5", "Idle_1", "Idle",
        "Militia_Walk", "FootmanWalk", "RiflemanWalk", "Walk"
    };

    // Preferred attack clip names for trigger wiring
    static readonly string[] AttackStatePriority =
    {
        "Attack", "Attack_1", "Militia_Attack1", "FootmanAttack1", "RiflemanAttack", "Attack1"
    };

    [InitializeOnLoadMethod]
    static void AutoRunIfNeeded()
    {
        // Also hook script reload so first import after adding this file is reliable.
        UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
        UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;

        EditorApplication.delayCall += TryAutoRun;
    }

    static void OnCompilationFinished(object _)
    {
        EditorApplication.delayCall += TryAutoRun;
    }

    static void TryAutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SessionState.GetBool(AutoFlagKey, false))
            return;

        // Run if any expected controller asset is missing (Knight may already exist).
        bool missing = Pieces.Any(p => !File.Exists(p.ControllerPath));
        if (!missing)
            return;

        SessionState.SetBool(AutoFlagKey, true);
        Debug.Log("[SetupWhiteMinorAnimators] Controllers missing — auto-running setup...");
        Run();
    }

    [MenuItem("Tools/Setup White Minor Animators")]
    public static void Run()
    {
        var log = new StringBuilder();
        void L(string s)
        {
            Debug.Log("[SetupWhiteMinorAnimators] " + s);
            log.AppendLine(s);
        }

        try
        {
            var controllers = new Dictionary<string, AnimatorController>();

            foreach (var piece in Pieces)
            {
                if (!File.Exists(piece.FbxPath))
                {
                    L($"ERROR: FBX missing for {piece.ParentName}: {piece.FbxPath}");
                    continue;
                }

                AnimatorController ctrl;
                if (!piece.RebuildController && File.Exists(piece.ControllerPath))
                {
                    ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(piece.ControllerPath);
                    L($"{piece.ParentName}: reusing existing controller {piece.ControllerPath}");
                }
                else
                {
                    ctrl = BuildController(piece.FbxPath, piece.ControllerPath, piece.ParentName, L);
                }

                if (ctrl != null)
                    controllers[piece.ParentName] = ctrl;
            }

            if (controllers.Count == 0)
            {
                L("ERROR: no controllers built/loaded");
                WriteReport(log);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            AssignInScene(controllers, L);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            L("DONE");
            WriteReport(log);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            L("EXCEPTION: " + ex);
            WriteReport(log);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static AnimatorController BuildController(string fbxPath, string controllerPath, string label, System.Action<string> L)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__") && !c.name.Contains("preview"))
            .GroupBy(c => c.name)
            .Select(g => g.First())
            .OrderBy(c => c.name)
            .ToList();

        L($"{label}: found {clips.Count} clips from {fbxPath}");
        foreach (var c in clips)
            L($"  - {c.name} ({c.length:F2}s)");

        if (clips.Count == 0)
        {
            L($"ERROR: no clips on {fbxPath}. Reimport FBX and ensure Animation takes are listed.");
            return null;
        }

        if (File.Exists(controllerPath))
            AssetDatabase.DeleteAsset(controllerPath);

        var dir = Path.GetDirectoryName(controllerPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;

        foreach (var s in sm.states.ToList())
            sm.RemoveState(s.state);

        if (!controller.parameters.Any(p => p.name == "Attack"))
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var stateByName = new Dictionary<string, AnimatorState>();
        float y = 0f;
        foreach (var clip in clips)
        {
            var state = sm.AddState(clip.name, new Vector3(280f, y, 0f));
            state.motion = clip;
            stateByName[clip.name] = state;
            y += 55f;
        }

        var idle = PickState(stateByName, DefaultStatePriority) ?? stateByName.Values.First();
        sm.defaultState = idle;
        L($"{label}: default state = {idle.name}");

        var attack = PickState(stateByName, AttackStatePriority);
        if (attack != null && attack != idle)
        {
            var toAttack = idle.AddTransition(attack);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            var back = attack.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.15f;
            L($"{label}: {idle.name} -> {attack.name} (Attack trigger) -> {idle.name}");
        }
        else
        {
            L($"{label}: WARN no Attack clip found — states only, no Attack transition");
        }

        EditorUtility.SetDirty(controller);
        L($"{label}: controller saved -> {controllerPath}");
        return controller;
    }

    static AnimatorState PickState(Dictionary<string, AnimatorState> map, string[] priority)
    {
        foreach (var name in priority)
        {
            if (map.TryGetValue(name, out var s))
                return s;
        }

        // fuzzy: contains
        foreach (var name in priority)
        {
            var hit = map.FirstOrDefault(kv =>
                kv.Key.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0);
            if (hit.Value != null)
                return hit.Value;
        }

        return null;
    }

    static void AssignInScene(Dictionary<string, AnimatorController> controllers, System.Action<string> L)
    {
        if (!File.Exists(ScenePath))
        {
            L("ERROR: GameScene not found at " + ScenePath);
            return;
        }

        Scene scene;
        var active = SceneManager.GetActiveScene();
        if (active.path == ScenePath && active.isLoaded)
        {
            scene = active;
            L("Using already open GameScene");
        }
        else
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            L("Opened GameScene");
        }

        int assigned = 0;
        var roots = scene.GetRootGameObjects();

        foreach (var piece in Pieces)
        {
            if (!controllers.TryGetValue(piece.ParentName, out var ctrl) || ctrl == null)
            {
                L($"SKIP assign {piece.ParentName}: no controller");
                continue;
            }

            var parent = roots.FirstOrDefault(r => r.name == piece.ParentName);
            if (parent == null)
            {
                L($"WARN: scene root '{piece.ParentName}' not found");
                continue;
            }

            // Prefer direct child whose name contains model token
            Transform model = null;
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                var c = parent.transform.GetChild(i);
                if (c.name.IndexOf(piece.ChildContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    model = c;
                    break;
                }
            }

            // Fallback: any child with Animator, or first child
            if (model == null)
            {
                var animInChildren = parent.GetComponentInChildren<Animator>(true);
                if (animInChildren != null)
                    model = animInChildren.transform;
                else if (parent.transform.childCount > 0)
                    model = parent.transform.GetChild(0);
            }

            if (model == null)
            {
                L($"WARN: {piece.ParentName} has no child model to assign");
                continue;
            }

            assigned += AssignOnAnimator(model.gameObject, ctrl, L);

            // Idle driver on unit root for edit-mode pose + play idle
            EnsureIdleDriver(parent, idleHint: ctrl.layers[0].stateMachine.defaultState?.name, L);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        L($"Scene saved. Assign operations: {assigned}");
    }

    static int AssignOnAnimator(GameObject go, AnimatorController ctrl, System.Action<string> L)
    {
        var anim = go.GetComponent<Animator>();
        if (anim == null)
            anim = go.GetComponentInChildren<Animator>(true);

        if (anim == null)
        {
            anim = go.AddComponent<Animator>();
            L($"Added Animator on {GetPath(go.transform)}");
        }

        if (anim.runtimeAnimatorController == ctrl)
        {
            anim.applyRootMotion = false;
            L($"Already assigned: {GetPath(anim.transform)} -> {ctrl.name}");
            return 0;
        }

        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        EditorUtility.SetDirty(anim);
        L($"Assigned {ctrl.name} -> {GetPath(anim.transform)}");
        return 1;
    }

    static void EnsureIdleDriver(GameObject unitRoot, string idleHint, System.Action<string> L)
    {
        var driver = unitRoot.GetComponent<IdleAnimationDriver>();
        if (driver == null)
        {
            driver = unitRoot.AddComponent<IdleAnimationDriver>();
            L($"Added IdleAnimationDriver on {unitRoot.name}");
        }

        if (!string.IsNullOrEmpty(idleHint))
        {
            var so = new SerializedObject(driver);
            var prop = so.FindProperty("_idleStateName");
            if (prop != null && string.IsNullOrEmpty(prop.stringValue))
            {
                prop.stringValue = idleHint;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(driver);
            }
        }
    }

    static string GetPath(Transform t)
    {
        var parts = new List<string>();
        while (t != null)
        {
            parts.Add(t.name);
            t = t.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    static void WriteReport(StringBuilder log)
    {
        try
        {
            var dir = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(ReportPath, log.ToString());
        }
        catch
        {
            /* ignore */
        }
    }
}
