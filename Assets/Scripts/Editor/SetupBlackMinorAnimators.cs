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
/// AnimatorControllers for Black minor pieces' child models in GameScene:
///   BlackPawn   → Peon_v2.fbx
///   BlackRook   → Grunt.fbx (+ optional Tauren_final sibling)
///   BlackKnigt / BlackKnight → RiderFinal.fbx
///   BlackBishop → Headhunter.fbx
///   (any root with Tauren child) → Tauren_final.fbx
/// Menu: Tools/Setup Black Minor Animators
/// Auto-runs once after compile if any controller is missing.
/// </summary>
public static class SetupBlackMinorAnimators
{
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    const string ReportPath = "Assets/Models/Orcs/BLACK_MINOR_ANIMATORS_REPORT.txt";
    const string AutoFlagKey = "SetupBlackMinorAnimators.AutoRan.v2";

    struct PieceDef
    {
        public string[] ParentNames;
        public string ChildContains;
        public string FbxPath;
        public string ControllerPath;
    }

    static readonly PieceDef[] Pieces =
    {
        new PieceDef
        {
            ParentNames = new[] { "BlackPawn" },
            ChildContains = "Peon",
            FbxPath = "Assets/Models/Orcs/Peon/Peon_v2.fbx",
            ControllerPath = "Assets/Models/Orcs/Peon/Peon_v2.controller"
        },
        new PieceDef
        {
            ParentNames = new[] { "BlackRook" },
            ChildContains = "Grunt",
            FbxPath = "Assets/Models/Orcs/Grunt/Grunt.fbx",
            ControllerPath = "Assets/Models/Orcs/Grunt/Grunt.controller"
        },
        new PieceDef
        {
            // Scene object is named BlackKnigt (typo)
            ParentNames = new[] { "BlackKnigt", "BlackKnight" },
            ChildContains = "Rider",
            FbxPath = "Assets/Models/Orcs/Raider/RiderFinal.fbx",
            ControllerPath = "Assets/Models/Orcs/Raider/RiderFinal.controller"
        },
        new PieceDef
        {
            ParentNames = new[] { "BlackBishop" },
            ChildContains = "Headhunter",
            FbxPath = "Assets/Models/Orcs/Headhunter/Headhunter.fbx",
            ControllerPath = "Assets/Models/Orcs/Headhunter/Headhunter.controller"
        },
        new PieceDef
        {
            // In GameScene Tauren_final is under BlackRook (sibling of Grunt); also match any parent.
            ParentNames = new[] { "BlackRook", "BlackTauren", "Tauren" },
            ChildContains = "Tauren",
            FbxPath = "Assets/Models/Orcs/Tauren/Tauren_final.fbx",
            ControllerPath = "Assets/Models/Orcs/Tauren/Tauren_final.controller"
        },
    };

    static readonly string[] DefaultStatePriority =
    {
        "Stand1", "Stand_1", "Stand", "Stand2", "Stand_2",
        "GruntStand", "WolfRider_Stand", "StandReady", "StandReady",
        "Idle1", "Idle2", "IdleReady", "Tauren_ Idle1", "Tauren_IdleReady",
        "Idle_02", "Idle", "Stand3", "Stand4",
        "Run", "Walk", "walk", "Tauren_ walk", "WolfRider_Run"
    };

    static readonly string[] AttackStatePriority =
    {
        "Attack", "Attack_1", "Attack1",
        "Tauren_ Attack", "Tauren_ Attack_Slam", "Attack_Slam",
        "WolfRider_Attack", "WolfRider_Attack2",
        "RangeAttack", "GruntSpell", "Spell",
        "AttackGold", "AttackLumber"
    };

    [InitializeOnLoadMethod]
    static void AutoRunIfNeeded()
    {
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

        bool missing = Pieces.Any(p => !File.Exists(p.ControllerPath));
        if (!missing)
            return;

        SessionState.SetBool(AutoFlagKey, true);
        Debug.Log("[SetupBlackMinorAnimators] Controllers missing — auto-running setup...");
        Run();
    }

    [MenuItem("Tools/Setup Black Minor Animators")]
    public static void Run()
    {
        var log = new StringBuilder();
        void L(string s)
        {
            Debug.Log("[SetupBlackMinorAnimators] " + s);
            log.AppendLine(s);
        }

        try
        {
            var controllers = new Dictionary<string, AnimatorController>();

            foreach (var piece in Pieces)
            {
                var key = piece.ControllerPath;
                if (!File.Exists(piece.FbxPath))
                {
                    L($"ERROR: FBX missing: {piece.FbxPath}");
                    continue;
                }

                var ctrl = BuildController(piece.FbxPath, piece.ControllerPath, Path.GetFileNameWithoutExtension(piece.FbxPath), L);
                if (ctrl != null)
                    controllers[key] = ctrl;
            }

            if (controllers.Count == 0)
            {
                L("ERROR: no controllers built");
                WriteReport(log);
                return;
            }

            AssignInScene(controllers, L);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            L("DONE");
            WriteReport(log);
        }
        catch (System.Exception ex)
        {
            L("EXCEPTION: " + ex);
            WriteReport(log);
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

        // Unity often keeps take names with "Rig|Clip" prefix — normalize display in log
        L($"{label}: found {clips.Count} clips from {fbxPath}");
        foreach (var c in clips)
            L($"  - {c.name} ({c.length:F2}s)");

        if (clips.Count == 0)
        {
            L($"ERROR: no clips on {fbxPath}. Reimport FBX (Animation takes).");
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

        // State name = short name after last '|' if present (cleaner graph)
        var stateByName = new Dictionary<string, AnimatorState>();
        var clipByStateName = new Dictionary<string, AnimationClip>();
        float y = 0f;
        foreach (var clip in clips)
        {
            var stateName = ShortName(clip.name);
            // avoid collisions
            if (stateByName.ContainsKey(stateName))
                stateName = clip.name;

            var state = sm.AddState(stateName, new Vector3(280f, y, 0f));
            state.motion = clip;
            stateByName[stateName] = state;
            clipByStateName[stateName] = clip;
            // also index full clip name
            if (!stateByName.ContainsKey(clip.name))
                stateByName[clip.name] = state;
            y += 55f;
        }

        var idle = PickState(stateByName, DefaultStatePriority) ?? stateByName.Values.First();
        sm.defaultState = idle;
        L($"{label}: default state = {idle.name}");

        // Loop-ish stand/run clips: flag on clip assets if possible (import settings elsewhere)
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
            L($"{label}: {idle.name} -> {attack.name} (Attack) -> {idle.name}");
        }
        else
        {
            L($"{label}: WARN no Attack clip — states only");
        }

        EditorUtility.SetDirty(controller);
        L($"{label}: saved -> {controllerPath}");
        return controller;
    }

    static string ShortName(string clipName)
    {
        if (string.IsNullOrEmpty(clipName))
            return clipName;
        int i = clipName.LastIndexOf('|');
        return i >= 0 && i < clipName.Length - 1 ? clipName.Substring(i + 1) : clipName;
    }

    static AnimatorState PickState(Dictionary<string, AnimatorState> map, string[] priority)
    {
        foreach (var name in priority)
        {
            if (map.TryGetValue(name, out var s))
                return s;
        }

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
            L("ERROR: GameScene not found");
            return;
        }

        Scene scene;
        var active = SceneManager.GetActiveScene();
        if (active.path == ScenePath && active.isLoaded)
        {
            scene = active;
            L("Using open GameScene");
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
            if (!controllers.TryGetValue(piece.ControllerPath, out var ctrl) || ctrl == null)
            {
                L($"SKIP {piece.ControllerPath}: no controller");
                continue;
            }

            // Prefer named parents; else any root that has a matching child model
            GameObject parent = null;
            Transform model = null;

            foreach (var pname in piece.ParentNames)
            {
                var candidate = roots.FirstOrDefault(r => r.name == pname);
                if (candidate == null)
                    continue;
                var child = FindChildContaining(candidate.transform, piece.ChildContains);
                if (child != null)
                {
                    parent = candidate;
                    model = child;
                    break;
                }
            }

            if (model == null)
            {
                foreach (var root in roots)
                {
                    var child = FindChildContaining(root.transform, piece.ChildContains);
                    if (child == null)
                        continue;
                    parent = root;
                    model = child;
                    L($"Found {piece.ChildContains} under {root.name} (fallback search)");
                    break;
                }
            }

            if (parent == null || model == null)
            {
                L($"WARN: model containing '{piece.ChildContains}' not found for [{string.Join(", ", piece.ParentNames)}]");
                continue;
            }

            assigned += AssignOnAnimator(model.gameObject, ctrl, L);
            EnsureIdleDriver(parent, ctrl.layers[0].stateMachine.defaultState?.name, L);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        L($"Scene saved. Assign operations: {assigned}");
    }

    static Transform FindChildContaining(Transform root, string token)
    {
        if (root == null || string.IsNullOrEmpty(token))
            return null;

        // Prefer direct children first
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return c;
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root) continue;
            if (t.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return t;
        }

        return null;
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

        if (string.IsNullOrEmpty(idleHint))
            return;

        var so = new SerializedObject(driver);
        var prop = so.FindProperty("_idleStateName");
        if (prop != null)
        {
            prop.stringValue = idleHint;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
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
