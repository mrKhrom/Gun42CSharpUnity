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
/// Ensures Sylvana / Thrall / Arthas / Jaina use looped Idle as default,
/// samples Idle pose in Edit Mode (via IdleAnimationDriver), and loops Idle in Play Mode.
/// Menu: Tools/Setup Character Idle Animations
/// </summary>
public static class SetupCharacterIdle
{
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    const string AutoFlagKey = "SetupCharacterIdle.AutoRan.v2";
    const string ReportPath = "Assets/Models/CHARACTER_IDLE_SETUP_REPORT.txt";

    static readonly (string fbx, string controller, string preferredIdle)[] Characters =
    {
        ("Assets/Models/Orcs/Sylvana/Sylvana.fbx", "Assets/Models/Orcs/Sylvana/Sylvana.controller", "Idle_6"),
        ("Assets/Models/Orcs/Thrall/Thrall.fbx", "Assets/Models/Orcs/Thrall/Thrall.controller", "Idle_02"),
        ("Assets/Models/Humans/Arthas/Arthas.fbx", "Assets/Models/Humans/Arthas/Arthas.controller", "Idle_02"),
        ("Assets/Models/Humans/Jaina/Jaina.fbx", "Assets/Models/Humans/Jaina/Jaina.controller", "Idle_02"),
    };

    static readonly string[] UnitPrefabs =
    {
        "Assets/Prefabs/WhiteKing.prefab",
        "Assets/Prefabs/Blackking.prefab",
        "Assets/Prefabs/WhiteQween.prefab",
        "Assets/Prefabs/BlackQween.prefab",
        "Assets/Prefabs/BlackQween 1.prefab",
    };

    [InitializeOnLoadMethod]
    static void AutoRunOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(AutoFlagKey, false))
                return;

            SessionState.SetBool(AutoFlagKey, true);
            // Soft auto-run: only if drivers missing on at least one known prefab
            bool need = false;
            foreach (var path in UnitPrefabs)
            {
                if (!File.Exists(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponentInChildren<Animator>(true) != null &&
                        root.GetComponent<IdleAnimationDriver>() == null)
                    {
                        need = true;
                        break;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (need)
            {
                Debug.Log("[SetupCharacterIdle] Idle drivers missing — auto setup...");
                Run();
            }
        };
    }

    [MenuItem("Tools/Setup Character Idle Animations")]
    public static void Run()
    {
        var log = new StringBuilder();
        void L(string s)
        {
            Debug.Log("[SetupCharacterIdle] " + s);
            log.AppendLine(s);
        }

        try
        {
            foreach (var (fbx, controller, preferredIdle) in Characters)
            {
                EnsureIdleLoopsOnFbx(fbx, L);
                EnsureControllerDefaultIdle(controller, preferredIdle, L);
            }

            foreach (var prefabPath in UnitPrefabs)
                AddDriverToPrefab(prefabPath, L);

            ApplyToOpenOrGameScene(L);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            L("DONE");
            File.WriteAllText(ReportPath, log.ToString());
            AssetDatabase.ImportAsset(ReportPath);
        }
        catch (System.Exception ex)
        {
            L("EXCEPTION: " + ex);
            File.WriteAllText(ReportPath, log.ToString());
        }
    }

    static void EnsureIdleLoopsOnFbx(string fbxPath, System.Action<string> L)
    {
        if (!File.Exists(fbxPath))
        {
            L($"SKIP fbx missing: {fbxPath}");
            return;
        }

        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            L($"SKIP not ModelImporter: {fbxPath}");
            return;
        }

        // Use explicit clip list if present, otherwise clone defaults
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        if (clips == null || clips.Length == 0)
        {
            L($"WARN no clips on {fbxPath}");
            return;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            var c = clips[i];
            bool isIdle = !string.IsNullOrEmpty(c.name) &&
                          c.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isIdle)
                continue;

            if (!c.loopTime)
            {
                c.loopTime = true;
                c.loopPose = true;
                clips[i] = c;
                changed = true;
                L($"{fbxPath}: loopTime ON for {c.name}");
            }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            L($"Reimported {fbxPath}");
        }
        else
        {
            L($"{fbxPath}: Idle loop flags already OK (or no Idle clip names)");
        }
    }

    static void EnsureControllerDefaultIdle(string controllerPath, string preferredIdle, System.Action<string> L)
    {
        if (!File.Exists(controllerPath))
        {
            L($"SKIP controller missing: {controllerPath}");
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null || controller.layers.Length == 0)
        {
            L($"SKIP bad controller: {controllerPath}");
            return;
        }

        var sm = controller.layers[0].stateMachine;
        AnimatorState idle = null;

        foreach (var candidate in new[] { preferredIdle, "Idle_02", "Idle_6", "Idle_5", "Idle" })
        {
            idle = sm.states.Select(s => s.state).FirstOrDefault(s => s != null && s.name == candidate);
            if (idle != null)
                break;
        }

        if (idle == null)
        {
            idle = sm.states
                .Select(s => s.state)
                .FirstOrDefault(s => s != null && s.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (idle == null)
        {
            L($"WARN no Idle state in {controllerPath}");
            return;
        }

        if (sm.defaultState != idle)
        {
            sm.defaultState = idle;
            EditorUtility.SetDirty(controller);
            L($"{controllerPath}: default state -> {idle.name}");
        }
        else
        {
            L($"{controllerPath}: default already {idle.name}");
        }

        // Ensure motion is loopable if clip asset allows (clip loop is import flag)
        if (idle.motion is AnimationClip clip && !clip.isLooping)
            L($"NOTE {idle.name} clip '{clip.name}' isLooping={clip.isLooping} (set loop on FBX import)");
    }

    static void AddDriverToPrefab(string prefabPath, System.Action<string> L)
    {
        if (!File.Exists(prefabPath))
        {
            L($"SKIP prefab missing: {prefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                L($"{prefabPath}: no Animator — skip");
                return;
            }

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var driver = root.GetComponent<IdleAnimationDriver>();
            if (driver == null)
                driver = root.AddComponent<IdleAnimationDriver>();

            // Prefer matching idle from child controller
            string idleName = GuessIdleName(animator);
            var so = new SerializedObject(driver);
            so.FindProperty("_animator").objectReferenceValue = animator;
            if (!string.IsNullOrEmpty(idleName))
                so.FindProperty("_idleStateName").stringValue = idleName;
            so.FindProperty("_sampleInEditMode").boolValue = true;
            so.FindProperty("_forceIdleOnStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Sample once inside prefab stage
            driver.SampleEditModePose();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            L($"{prefabPath}: IdleAnimationDriver OK (idle={idleName}, animator={animator.name})");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string GuessIdleName(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return null;

#if UNITY_EDITOR
        var ctrl = animator.runtimeAnimatorController as AnimatorController;
        if (ctrl != null && ctrl.layers.Length > 0)
        {
            var def = ctrl.layers[0].stateMachine.defaultState;
            if (def != null && def.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return def.name;
        }
#endif

        foreach (var n in new[] { "Idle_02", "Idle_6", "Idle_5", "Idle" })
        {
            if (animator.HasState(0, Animator.StringToHash(n)))
                return n;
        }

        return null;
    }

    static void ApplyToOpenOrGameScene(System.Action<string> L)
    {
        Scene scene;
        var active = SceneManager.GetActiveScene();
        if (active.path == ScenePath && active.isLoaded)
        {
            scene = active;
            L("Using open GameScene");
        }
        else if (File.Exists(ScenePath))
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            L("Opened GameScene");
        }
        else
        {
            L("GameScene not found — prefab-only setup");
            return;
        }

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var anim in root.GetComponentsInChildren<Animator>(true))
            {
                if (anim.runtimeAnimatorController == null)
                    continue;

                // Only our four characters / unit roots with controller named known
                string ctrlName = anim.runtimeAnimatorController.name;
                bool known =
                    ctrlName.IndexOf("Sylvana", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ctrlName.IndexOf("Thrall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ctrlName.IndexOf("Arthas", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ctrlName.IndexOf("Jaina", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ctrlName.IndexOf("Silvana", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (!known)
                    continue;

                // Prefer Unit root for the driver
                var unit = anim.GetComponentInParent<Unit>();
                var host = unit != null ? unit.gameObject : anim.gameObject;

                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var driver = host.GetComponent<IdleAnimationDriver>();
                if (driver == null)
                    driver = host.AddComponent<IdleAnimationDriver>();

                var so = new SerializedObject(driver);
                so.FindProperty("_animator").objectReferenceValue = anim;
                string idleName = GuessIdleName(anim);
                if (!string.IsNullOrEmpty(idleName))
                    so.FindProperty("_idleStateName").stringValue = idleName;
                so.FindProperty("_sampleInEditMode").boolValue = true;
                so.FindProperty("_forceIdleOnStart").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                driver.SampleEditModePose();
                EditorUtility.SetDirty(host);
                count++;
                L($"Scene: {host.name} <- IdleAnimationDriver ({ctrlName} / {idleName})");
            }
        }

        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            L($"Saved scene, drivers={count}");
        }
        else
        {
            L("No character Animators found in scene (prefabs still updated)");
        }
    }
}
