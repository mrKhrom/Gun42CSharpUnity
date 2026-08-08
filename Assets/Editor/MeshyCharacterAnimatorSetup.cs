using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Combines Meshy multi-FBX animation packs into Animator Controllers.
/// Menu: Tools/Setup Character Animators (Silvana + Jaina)
/// </summary>
public static class MeshyCharacterAnimatorSetup
{
    private static readonly string[] Characters =
    {
        "Assets/Models/Orcs/Silvana",
        "Assets/Models/Humans/Jaina"
    };

    private static readonly string[] States = { "Idle", "Walk", "Attack", "Dead" };

    [MenuItem("Tools/Setup Character Animators (Silvana + Jaina)")]
    public static void Setup()
    {
        foreach (var folder in Characters)
        {
            SetupCharacter(folder);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MeshyCharacterAnimatorSetup] Done. Assign controller to Animator on character prefab/model.");
    }

    private static void SetupCharacter(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning("Folder not found: " + folder);
            return;
        }

        var charName = Path.GetFileName(folder);
        var baseModelPath = folder + "/" + charName + ".fbx";
        var controllerPath = folder + "/" + charName + ".controller";
        var animFolder = folder + "/Animations";

        var baseModel = AssetDatabase.LoadAssetAtPath<GameObject>(baseModelPath);
        if (baseModel == null)
        {
            Debug.LogError("Base model missing: " + baseModelPath);
            return;
        }

        // Ensure animation import + Generic on all related FBX
        ConfigureModelImporter(baseModelPath, true);
        foreach (var state in States)
        {
            var animPath = animFolder + "/" + charName + "_" + state + ".fbx";
            ConfigureModelImporter(animPath, true);
        }
        AssetDatabase.ImportAsset(baseModelPath, ImportAssetOptions.ForceUpdate);
        foreach (var state in States)
        {
            var animPath = animFolder + "/" + charName + "_" + state + ".fbx";
            if (File.Exists(animPath))
                AssetDatabase.ImportAsset(animPath, ImportAssetOptions.ForceUpdate);
        }

        // Create or overwrite controller
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        // Remove default empty state if any
        var defaultState = sm.defaultState;

        AnimatorState idleState = null;
        AnimatorState walkState = null;
        AnimatorState attackState = null;
        AnimatorState deadState = null;

        float y = 0;
        foreach (var stateName in States)
        {
            var animPath = animFolder + "/" + charName + "_" + stateName + ".fbx";
            var clip = LoadFirstClip(animPath);
            if (clip == null)
            {
                Debug.LogWarning("No clip in " + animPath);
                continue;
            }

            // Loop idle/walk
            var so = new SerializedObject(clip);
            // clip.isLooping is read-only in runtime; set via ModelImporter clip settings ideally
            // At least mark for user

            var st = sm.AddState(stateName, new Vector3(300, y, 0));
            st.motion = clip;
            y += 70;

            if (stateName == "Idle") idleState = st;
            if (stateName == "Walk") walkState = st;
            if (stateName == "Attack") attackState = st;
            if (stateName == "Dead") deadState = st;
        }

        if (idleState != null)
            sm.defaultState = idleState;

        // Transitions
        if (idleState != null && walkState != null)
        {
            var t1 = idleState.AddTransition(walkState);
            t1.hasExitTime = false;
            t1.duration = 0.15f;
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var t2 = walkState.AddTransition(idleState);
            t2.hasExitTime = false;
            t2.duration = 0.15f;
            t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (attackState != null)
        {
            var anyAttack = sm.AddAnyStateTransition(attackState);
            anyAttack.hasExitTime = false;
            anyAttack.duration = 0.05f;
            anyAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            if (idleState != null)
            {
                var back = attackState.AddTransition(idleState);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;
            }
        }

        if (deadState != null)
        {
            var anyDie = sm.AddAnyStateTransition(deadState);
            anyDie.hasExitTime = false;
            anyDie.duration = 0.05f;
            anyDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
        }

        // Prefab with Animator
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseModel);
        var animator = instance.GetComponent<Animator>();
        if (animator == null)
            animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // Assign material if missing
        var mat = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Materials/" + charName + ".mat");
        if (mat != null)
        {
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
            }
        }

        var prefabPath = folder + "/" + charName + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        EditorUtility.SetDirty(controller);
        Debug.Log("[MeshyCharacterAnimatorSetup] " + charName + " -> " + controllerPath + " + prefab " + prefabPath);
    }

    private static void ConfigureModelImporter(string path, bool importAnim)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = importAnim;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.SaveAndReimport();
    }

    private static AnimationClip LoadFirstClip(string fbxPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        return assets.OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }
}
