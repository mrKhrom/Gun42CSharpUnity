using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Creates Sylvana Animator Controller from clips inside Sylvana.fbx
/// and assigns it to the Silvana unit prefab / scene instances.
/// Menu: Tools/Sylvana/Setup Animator Controller
/// Auto-runs once if controller is missing.
/// </summary>
public static class SetupSylvanaAnimator
{
    const string FbxPath = "Assets/Models/Orcs/Sylvana/Sylvana.fbx";
    const string ControllerPath = "Assets/Models/Orcs/Sylvana/Sylvana.controller";
    const string PrefabPath = "Assets/Prefabs/Silvana.prefab";

    // Preferred default clip name order
    static readonly string[] PreferredDefault = { "Idle_6", "Idle", "Walking", "Walking_Woman" };

    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ControllerPath.Replace("Assets/", Application.dataPath + "/").Replace('/', Path.DirectorySeparatorChar)
                .Replace("\\", Path.DirectorySeparatorChar.ToString())))
            {
                // File.Exists with Assets path
            }
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            {
                Debug.Log("[SetupSylvanaAnimator] Controller missing — creating...");
                Setup();
            }
        };
    }

    [MenuItem("Tools/Sylvana/Setup Animator Controller")]
    public static void Setup()
    {
        // Ensure FBX imported with animations
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[SetupSylvanaAnimator] FBX not found: " + FbxPath);
            return;
        }

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();

        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .OrderBy(c => c.name)
            .ToList();

        if (clips.Count == 0)
        {
            Debug.LogError("[SetupSylvanaAnimator] No AnimationClips found in " + FbxPath +
                           ". Check FBX Animation import settings.");
            return;
        }

        Debug.Log("[SetupSylvanaAnimator] Found clips: " + string.Join(", ", clips.Select(c => c.name)));

        // Create / overwrite controller
        if (File.Exists(ControllerPath))
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = controller.layers[0].stateMachine;

        // Clear default empty state
        var toRemove = sm.states.Select(s => s.state).ToList();
        foreach (var st in toRemove)
            sm.RemoveState(st);

        AnimatorState defaultState = null;
        float y = 0f;
        foreach (var clip in clips)
        {
            var state = sm.AddState(clip.name, new Vector3(300, y, 0));
            state.motion = clip;
            y += 70f;

            // Prefer Idle as default
            if (defaultState == null || PreferredDefault.Any(p => clip.name.Contains(p)))
            {
                if (PreferredDefault.Any(p => clip.name == p) || defaultState == null)
                    defaultState = state;
                // exact match preferred
                if (PreferredDefault.Contains(clip.name))
                    defaultState = state;
            }
        }

        if (defaultState != null)
            sm.defaultState = defaultState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Assign to Silvana prefab (and nested model)
        AssignControllerToPrefab(controller);
        AssignControllerInOpenScenes(controller);

        Debug.Log($"[SetupSylvanaAnimator] Done.\nController: {ControllerPath}\nDefault: {(defaultState != null ? defaultState.name : "?")}\nClips: {clips.Count}");
        EditorUtility.DisplayDialog(
            "Sylvana Animator",
            $"Controller created with {clips.Count} clips.\nDefault: {(defaultState != null ? defaultState.name : "none")}\n\nAssigned to Silvana prefab / scene Animators.\n\nPreview: select model with Animator → Window → Animation → Animation → Play.",
            "OK");
    }

    static void AssignControllerToPrefab(RuntimeAnimatorController controller)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogWarning("[SetupSylvanaAnimator] Prefab not found: " + PrefabPath);
            return;
        }

        try
        {
            // Ensure visual model is present as child
            var model = root.GetComponentsInChildren<Animator>(true)
                .Select(a => a.gameObject)
                .FirstOrDefault(go => go != root);

            // If no Animator child, try instantiate FBX under prefab
            if (root.GetComponentInChildren<Animator>(true) == null)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
                if (fbx != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                    instance.name = "Sylvana";
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    Debug.Log("[SetupSylvanaAnimator] Added Sylvana.fbx under Silvana prefab.");
                }
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            if (animators.Length == 0)
            {
                // Root might need Animator if mesh is on root (unlikely)
                var anim = root.GetComponent<Animator>();
                if (anim == null)
                    anim = root.AddComponent<Animator>();
                animators = new[] { anim };
            }

            foreach (var anim in animators)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                EditorUtility.SetDirty(anim);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[SetupSylvanaAnimator] Assigned controller to {animators.Length} Animator(s) on prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void AssignControllerInOpenScenes(RuntimeAnimatorController controller)
    {
        int n = 0;
        foreach (var anim in Object.FindObjectsOfType<Animator>())
        {
            var path = anim.gameObject.name.ToLowerInvariant();
            bool match = path.Contains("sylv") || path.Contains("silv");
            // also match if mesh comes from Sylvana fbx
            var smr = anim.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                var mp = AssetDatabase.GetAssetPath(smr.sharedMesh).Replace("\\", "/");
                if (mp.Contains("Models/Orcs/Sylvana/"))
                    match = true;
            }
            if (!match) continue;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            EditorUtility.SetDirty(anim);
            n++;
        }
        if (n > 0)
            Debug.Log($"[SetupSylvanaAnimator] Updated {n} Animator(s) in open scene(s).");
    }
}
