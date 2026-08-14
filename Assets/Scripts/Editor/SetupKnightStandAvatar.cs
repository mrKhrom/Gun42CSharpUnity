using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a Generic Avatar from the Stand_1 animation pose and assigns it to Knight.fbx
/// so Scene view (Edit Mode) shows a standing rest pose instead of the first FBX take (Walk).
/// Also forces Knight.controller default state + IdleAnimationDriver (Stand_1).
/// Menu: Tools/Setup Knight Stand_1 Avatar
/// </summary>
public static class SetupKnightStandAvatar
{
    const string FbxPath = "Assets/Models/Humans/Knight/Knight.fbx";
    const string ControllerPath = "Assets/Models/Humans/Knight/Knight.controller";
    const string AvatarPath = "Assets/Models/Humans/Knight/Knight_Stand1.avatar";
    const string PrefabPath = "Assets/Prefabs/Knight.prefab";
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    const string StandState = "Stand_1";
    const string AutoFlagKey = "SetupKnightStandAvatar.AutoRan";

    static bool s_Busy;

    [InitializeOnLoadMethod]
    static void AutoRunIfNeeded()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (SessionState.GetBool(AutoFlagKey, false))
                return;
            if (!File.Exists(FbxPath))
                return;

            // Run once if avatar asset missing
            if (File.Exists(AvatarPath))
                return;

            SessionState.SetBool(AutoFlagKey, true);
            Debug.Log("[SetupKnightStandAvatar] Avatar missing — auto-running...");
            Run();
        };
    }

    [MenuItem("Tools/Setup Knight Stand_1 Avatar")]
    public static void Run()
    {
        if (s_Busy)
            return;
        s_Busy = true;

        try
        {
            if (!File.Exists(FbxPath))
            {
                Debug.LogError("[SetupKnightStandAvatar] Missing FBX: " + FbxPath);
                return;
            }

            var standClip = FindClip(FbxPath, StandState);
            if (standClip == null)
            {
                Debug.LogError("[SetupKnightStandAvatar] Clip '" + StandState + "' not found on " + FbxPath);
                return;
            }

            EnsureControllerDefaultStand1();
            BuildAndAssignAvatarFromPose(standClip);
            EnsureIdleDrivers();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SetupKnightStandAvatar] DONE — Knight rest pose = " + StandState +
                      ". Click Knight in Scene if pose does not update immediately.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[SetupKnightStandAvatar] " + ex);
        }
        finally
        {
            s_Busy = false;
        }
    }

    static AnimationClip FindClip(string fbxPath, string clipName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c != null && c.name == clipName && !c.name.StartsWith("__preview__"));
    }

    /// <summary>
    /// Sample Stand_1 on a temp instance, build Generic Avatar from that hierarchy pose,
    /// save it, and point Knight.fbx importer to Copy From Other.
    /// </summary>
    static void BuildAndAssignAvatarFromPose(AnimationClip standClip)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
        {
            Debug.LogError("[SetupKnightStandAvatar] Cannot load model " + FbxPath);
            return;
        }

        var instance = Object.Instantiate(model);
        instance.name = "Knight_Stand1_PoseBake";
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            // Apply frame 0 of Stand_1 to the hierarchy (bind-style rest pose)
            standClip.SampleAnimation(instance, 0f);

            // Build Generic avatar from current (Stand_1) bone transforms
            var avatar = AvatarBuilder.BuildGenericAvatar(instance, string.Empty);
            if (avatar == null || !avatar.isValid)
            {
                Debug.LogError("[SetupKnightStandAvatar] AvatarBuilder failed to build a valid Generic avatar.");
                return;
            }

            avatar.name = "Knight_Stand1";

            // Save / replace avatar asset
            var existing = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(avatar, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(avatar);
                avatar = existing;
            }
            else
            {
                var dir = Path.GetDirectoryName(AvatarPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(avatar, AvatarPath);
            }

            AssetDatabase.SaveAssets();

            // Point FBX at this avatar so rest pose in Scene = Stand_1
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[SetupKnightStandAvatar] ModelImporter missing for " + FbxPath);
                return;
            }

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther
                || importer.sourceAvatar != avatar)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                changed = true;
            }

            // Prefer Stand_1 as first clip for FBX preview tabs (cosmetic)
            if (PreferStand1ClipOrder(importer))
                changed = true;

            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log("[SetupKnightStandAvatar] Knight.fbx → Copy Avatar From Other: " + AvatarPath);
            }
            else
            {
                Debug.Log("[SetupKnightStandAvatar] Avatar already assigned on FBX.");
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Put Stand_1 first among clipAnimations (helps some Editor previews that use take order).
    /// </summary>
    static bool PreferStand1ClipOrder(ModelImporter importer)
    {
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return false;

        int standIdx = -1;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].name == StandState || clips[i].takeName == StandState)
            {
                standIdx = i;
                break;
            }
        }

        if (standIdx <= 0)
            return false; // already first or missing

        var reordered = new ModelImporterClipAnimation[clips.Length];
        reordered[0] = clips[standIdx];
        int w = 1;
        for (int i = 0; i < clips.Length; i++)
        {
            if (i == standIdx) continue;
            reordered[w++] = clips[i];
        }

        // Ensure Stand_1 loops (idle)
        reordered[0].loopTime = true;
        reordered[0].loopPose = true;

        importer.clipAnimations = reordered;
        return true;
    }

    static void EnsureControllerDefaultStand1()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogWarning("[SetupKnightStandAvatar] No controller at " + ControllerPath);
            return;
        }

        var sm = ctrl.layers[0].stateMachine;
        AnimatorState stand = null;
        foreach (var cs in sm.states)
        {
            if (cs.state != null && cs.state.name == StandState)
            {
                stand = cs.state;
                break;
            }
        }

        if (stand == null)
        {
            Debug.LogWarning("[SetupKnightStandAvatar] Controller has no state " + StandState);
            return;
        }

        if (sm.defaultState != stand)
        {
            sm.defaultState = stand;
            EditorUtility.SetDirty(ctrl);
            Debug.Log("[SetupKnightStandAvatar] Controller default state → " + StandState);
        }
    }

    static void EnsureIdleDrivers()
    {
        // Prefab Knight root
        if (File.Exists(PrefabPath))
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyDriver(root, "prefab " + PrefabPath);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // GameScene WhiteKnight
        if (!File.Exists(ScenePath))
            return;

        Scene scene;
        var active = SceneManager.GetActiveScene();
        if (active.path == ScenePath && active.isLoaded)
            scene = active;
        else
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        bool dirty = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "WhiteKnight")
                continue;

            // Ensure child Animator has controller
            var anim = root.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                if (ctrl != null && anim.runtimeAnimatorController != ctrl)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    EditorUtility.SetDirty(anim);
                    dirty = true;
                }
            }

            if (ApplyDriver(root, "scene WhiteKnight"))
                dirty = true;
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static bool ApplyDriver(GameObject root, string label)
    {
        if (root == null)
            return false;

        var driver = root.GetComponent<IdleAnimationDriver>();
        if (driver == null)
            driver = root.AddComponent<IdleAnimationDriver>();

        var so = new SerializedObject(driver);
        var stateProp = so.FindProperty("_idleStateName");
        var sampleProp = so.FindProperty("_sampleInEditMode");
        var forceProp = so.FindProperty("_forceIdleOnStart");
        var animProp = so.FindProperty("_animator");

        bool changed = false;
        if (stateProp != null && stateProp.stringValue != StandState)
        {
            stateProp.stringValue = StandState;
            changed = true;
        }

        if (sampleProp != null && !sampleProp.boolValue)
        {
            sampleProp.boolValue = true;
            changed = true;
        }

        if (forceProp != null && !forceProp.boolValue)
        {
            forceProp.boolValue = true;
            changed = true;
        }

        var anim = root.GetComponentInChildren<Animator>(true);
        if (animProp != null && anim != null && animProp.objectReferenceValue != anim)
        {
            animProp.objectReferenceValue = anim;
            changed = true;
        }

        if (changed)
            so.ApplyModifiedPropertiesWithoutUndo();
        else
            so.Dispose();

        EditorUtility.SetDirty(driver);
        driver.SampleEditModePose();

        Debug.Log("[SetupKnightStandAvatar] IdleAnimationDriver (" + StandState + ") on " + label);
        return true;
    }
}
