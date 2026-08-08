using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto-fix Jaina (Meshy) on import: materials, clips, upright prefab.
/// After import, use Assets/Prefabs/Jaina.prefab on the scene.
/// </summary>
public class JainaModelPostprocessor : AssetPostprocessor
{
    const string FbxPath = "Assets/Models/Humans/Jaina/Jaina.fbx";
    const string PrefabPath = "Assets/Prefabs/Jaina.prefab";
    const string MatPath = "Assets/Models/Humans/Jaina/Materials/Material_1_baseColor.mat";
    const string ControllerPath = "Assets/Models/Humans/Jaina/Jaina.controller";

    static bool s_Building;
    static bool s_RenamingClips;

    void OnPreprocessModel()
    {
        if (assetPath != FbxPath) return;

        var imp = (ModelImporter)assetImporter;
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.animationCompression = ModelImporterAnimationCompression.Off;
        imp.preserveHierarchy = true;
        imp.materialLocation = ModelImporterMaterialLocation.External;
        imp.importCameras = false;
        imp.importLights = false;

        try
        {
            var p = typeof(ModelImporter).GetProperty("bakeAxisConversion");
            if (p != null && p.CanWrite) p.SetValue(imp, true);
        }
        catch { /* older API */ }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) return;

        foreach (var k in imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
            imp.RemoveRemap(k);

        foreach (var n in new[]
                 {
                     "Material_1_baseColor", "Material_1", "Material_1_baseColor.001",
                     "Material_1_basecolor", "Material", "Material.001", "Jaina", "Body", "Mesh", "mesh"
                 })
        {
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat);
        }
    }

    void OnPostprocessModel(GameObject g)
    {
        if (assetPath != FbxPath) return;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            if (mat == null) continue;
            var mats = smr.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                smr.sharedMaterial = mat;
                continue;
            }
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == null) mats[i] = mat;
            smr.sharedMaterials = mats;
        }

        var animator = g.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    static void OnPostprocessAllAssets(
        string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
    {
        if (s_Building || s_RenamingClips) return;
        if (imported == null || !imported.Contains(FbxPath)) return;

        // Delay so import fully finishes
        EditorApplication.delayCall += () =>
        {
            if (s_Building) return;
            RenameClipsOnce();
            BuildPrefabOnce();
        };
    }

    static void RenameClipsOnce()
    {
        if (s_RenamingClips) return;
        var imp = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (imp == null) return;

        var defaults = imp.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0) return;

        bool need = false;
        for (int i = 0; i < defaults.Length; i++)
        {
            string take = defaults[i].takeName ?? "";
            string nice = NiceName(take, i);
            if (defaults[i].name != nice || !defaults[i].loopTime)
            {
                need = true;
                break;
            }
        }
        if (!need) return;

        s_RenamingClips = true;
        try
        {
            for (int i = 0; i < defaults.Length; i++)
            {
                defaults[i].name = NiceName(defaults[i].takeName ?? "", i);
                defaults[i].loopTime = true;
                defaults[i].keepOriginalOrientation = true;
                defaults[i].keepOriginalPositionY = true;
                defaults[i].keepOriginalPositionXZ = true;
            }
            imp.clipAnimations = defaults;
            // Do NOT SaveAndReimport here — applying clipAnimations alone often enough after next refresh.
            // Force single reimport guarded by flag.
            imp.SaveAndReimport();
        }
        finally
        {
            // allow later imports, but BuildPrefab runs after this reimport via OnPostprocessAllAssets
            EditorApplication.delayCall += () => { s_RenamingClips = false; };
        }
    }

    static string NiceName(string take, int index)
    {
        if (!string.IsNullOrEmpty(take) && take.Length < 32 && !take.Contains("-"))
            return take;
        return "Clip_" + (index + 1).ToString("00");
    }

    static void BuildPrefabOnce()
    {
        if (s_Building) return;
        s_Building = true;
        try
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null)
            {
                Debug.LogError("[Jaina] FBX GameObject not loaded");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";

            var root = new GameObject("Jaina");
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localScale = Vector3.one;
            instance.transform.localRotation = Quaternion.identity;

            // If bakeAxisConversion already stood the model up, identity wins.
            // Otherwise pick best of common Meshy fixes.
            Quaternion[] candidates =
            {
                Quaternion.identity,
                Quaternion.Euler(90f, 0f, 0f),
                Quaternion.Euler(-90f, 0f, 0f),
                Quaternion.Euler(90f, 180f, 0f),
                Quaternion.Euler(-90f, 180f, 0f),
            };

            Quaternion best = Quaternion.identity;
            float bestScore = float.NegativeInfinity;
            foreach (var rot in candidates)
            {
                instance.transform.localRotation = rot;
                if (!TryBounds(root, out var b)) continue;
                float score = b.size.y * 2f - Mathf.Max(b.size.x, b.size.z);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = rot;
                }
            }
            instance.transform.localRotation = best;

            if (TryBounds(root, out var fb))
                instance.transform.localPosition += new Vector3(0f, -fb.min.y, 0f);

            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                if (mat == null) continue;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] == null) mats[i] = mat;
                if (mats.Length == 0) smr.sharedMaterial = mat;
                else smr.sharedMaterials = mats;
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (clips.Count > 0)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(ControllerPath) != null)
                    AssetDatabase.DeleteAsset(ControllerPath);

                var ctrl = UnityEditor.Animations.AnimatorController
                    .CreateAnimatorControllerAtPathWithClip(ControllerPath, clips[0]);
                var sm = ctrl.layers[0].stateMachine;
                for (int i = 1; i < clips.Count; i++)
                {
                    var st = sm.AddState(clips[i].name);
                    st.motion = clips[i];
                }
                animator.runtimeAnimatorController = ctrl;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log(
                $"[Jaina] Prefab ready: {PrefabPath} | clips={clips.Count} | " +
                $"orient={best.eulerAngles} score={bestScore:F2}. Drag THIS prefab to the scene.");
        }
        finally
        {
            s_Building = false;
        }
    }

    static bool TryBounds(GameObject root, out Bounds b)
    {
        b = new Bounds();
        bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any;
    }

    [MenuItem("Tools/Fix Jaina Model Now")]
    public static void MenuFix()
    {
        s_Building = false;
        s_RenamingClips = false;
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
        // Build after import callbacks
        EditorApplication.delayCall += () =>
        {
            RenameClipsOnce();
            EditorApplication.delayCall += BuildPrefabOnce;
        };
    }
}
