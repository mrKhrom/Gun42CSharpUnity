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
/// Creates AnimatorControllers for Arthas (WhiteKing child) and Thrall (Blackking child)
/// and assigns them in GameScene.
/// Menu: Tools/Setup King Animators
/// Batchmode: -executeMethod SetupKingAnimators.Run
/// Auto-runs once after compile if controllers are missing.
/// </summary>
public static class SetupKingAnimators
{
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    const string ArthasFbx = "Assets/Models/Humans/Arthas/Arthas.fbx";
    const string ThrallFbx = "Assets/Models/Orcs/Thrall/Thrall.fbx";
    const string ArthasControllerPath = "Assets/Models/Humans/Arthas/Arthas.controller";
    const string ThrallControllerPath = "Assets/Models/Orcs/Thrall/Thrall.controller";
    const string ReportPath = "Assets/Models/Humans/Arthas/KING_ANIMATORS_REPORT.txt";
    const string AutoFlagKey = "SetupKingAnimators.AutoRan";

    [InitializeOnLoadMethod]
    static void AutoRunIfNeeded()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(AutoFlagKey, false))
                return;

            bool missing = !File.Exists(ArthasControllerPath) || !File.Exists(ThrallControllerPath);
            if (!missing)
                return;

            SessionState.SetBool(AutoFlagKey, true);
            Debug.Log("[SetupKingAnimators] Controllers missing — auto-running setup...");
            Run();
        };
    }

    [MenuItem("Tools/Setup King Animators")]
    public static void Run()
    {
        var log = new StringBuilder();
        void L(string s)
        {
            Debug.Log("[SetupKingAnimators] " + s);
            log.AppendLine(s);
        }

        try
        {
            if (!File.Exists(ArthasFbx) || !File.Exists(ThrallFbx))
            {
                L("ERROR: FBX missing. Arthas=" + File.Exists(ArthasFbx) + " Thrall=" + File.Exists(ThrallFbx));
                WriteReport(log);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var arthasCtrl = BuildController(ArthasFbx, ArthasControllerPath, "Arthas", L);
            var thrallCtrl = BuildController(ThrallFbx, ThrallControllerPath, "Thrall", L);

            if (arthasCtrl == null || thrallCtrl == null)
            {
                L("ERROR: failed to build one or both controllers");
                WriteReport(log);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            AssignInScene(arthasCtrl, thrallCtrl, L);

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
            L($"ERROR: no clips on {fbxPath}. Open Unity and ensure FBX Animation tab lists takes.");
            return null;
        }

        if (File.Exists(controllerPath))
            AssetDatabase.DeleteAsset(controllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        // Remove default empty state layer content and rebuild cleanly
        var sm = controller.layers[0].stateMachine;

        // Clear auto-created default state
        var existing = sm.states.ToList();
        foreach (var s in existing)
            sm.RemoveState(s.state);

        // Attack trigger (same name as Sylvana / ArcheryWeapon)
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

        // Default idle
        AnimatorState idle = null;
        foreach (var candidate in new[] { "Idle_02", "Idle_5", "Idle", "Idle_6" })
        {
            if (stateByName.TryGetValue(candidate, out idle))
                break;
        }

        if (idle == null)
            idle = stateByName.Values.First();

        sm.defaultState = idle;
        L($"{label}: default state = {idle.name}");

        // Attack state + transitions Idle <-> Attack
        if (stateByName.TryGetValue("Attack", out var attack) && idle != null && attack != idle)
        {
            var toAttack = idle.AddTransition(attack);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            var back = attack.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.15f;
            L($"{label}: Idle -> Attack (trigger) -> Idle wired");
        }
        else
        {
            L($"{label}: WARN no Attack clip — only states listed, no Attack transition");
        }

        EditorUtility.SetDirty(controller);
        L($"{label}: controller saved -> {controllerPath}");
        return controller;
    }

    static void AssignInScene(AnimatorController arthasCtrl, AnimatorController thrallCtrl, System.Action<string> L)
    {
        if (!File.Exists(ScenePath))
        {
            L("ERROR: GameScene not found at " + ScenePath);
            return;
        }

        // Prefer already-open GameScene so we don't stomp the user's open scene if possible
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
        var seen = new HashSet<int>();

        foreach (var root in scene.GetRootGameObjects())
        {
            bool isWhiteKing = root.name == "WhiteKing" || root.name == "Whiteking";
            bool isBlackKing = root.name == "Blackking" || root.name == "BlackKing";

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Arthas" && (isWhiteKing || root.name.Contains("King") || t.parent != null))
                {
                    // Prefer under WhiteKing; still assign any Arthas in scene once
                    if (seen.Add(t.GetInstanceID()))
                        assigned += AssignOnAnimator(t.gameObject, arthasCtrl, L);
                }
                else if (t.name == "Thrall")
                {
                    if (seen.Add(t.GetInstanceID()))
                        assigned += AssignOnAnimator(t.gameObject, thrallCtrl, L);
                }
            }

            if (isWhiteKing && !HasChildNamed(root, "Arthas"))
                L("WARN: WhiteKing has no child named Arthas");
            if (isBlackKing && !HasChildNamed(root, "Thrall"))
                L("WARN: Blackking has no child named Thrall");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        L($"Scene saved. Assign operations counted: {assigned}");
    }

    static bool HasChildNamed(GameObject root, string childName)
    {
        return root.GetComponentsInChildren<Transform>(true).Any(t => t.name == childName);
    }

    static int AssignOnAnimator(GameObject go, AnimatorController ctrl, System.Action<string> L)
    {
        var anim = go.GetComponent<Animator>();
        if (anim == null)
            anim = go.GetComponentInChildren<Animator>(true);

        if (anim == null)
        {
            // FBX root should have Animator; add if missing
            anim = go.AddComponent<Animator>();
            L($"Added Animator on {GetPath(go.transform)}");
        }

        if (anim.runtimeAnimatorController == ctrl)
        {
            L($"Already assigned: {GetPath(anim.transform)} -> {ctrl.name}");
            return 0;
        }

        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        EditorUtility.SetDirty(anim);
        L($"Assigned {ctrl.name} -> {GetPath(anim.transform)}");
        return 1;
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
