using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes Meshy Jaina import: upright orientation + working animation clips + prefab.
/// Menu: Tools/Fix Jaina Model
/// Batchmode: -executeMethod JainaModelFixer.Run
/// </summary>
public static class JainaModelFixer
{
    const string FbxPath = "Assets/Models/Humans/Jaina/Jaina.fbx";
    const string PrefabPath = "Assets/Prefabs/Jaina.prefab";
    const string ReportPath = "Assets/Models/Humans/Jaina/FIX_REPORT.txt";

    [MenuItem("Tools/Fix Jaina Model")]
    public static void Run()
    {
        var log = new StringBuilder();
        void L(string s) { Debug.Log("[JainaModelFixer] " + s); log.AppendLine(s); }

        if (!File.Exists(FbxPath))
        {
            L("ERROR: FBX not found at " + FbxPath);
            WriteReport(log);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // --- 1) Configure ModelImporter ---
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            L("ERROR: ModelImporter is null");
            WriteReport(log);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.weldVertices = true;
        importer.keepQuads = false;
        importer.swapUVChannels = false;
        importer.generateSecondaryUV = false;
        importer.importBlendShapes = true;
        importer.importVisibility = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.preserveHierarchy = true;
        importer.sortHierarchyByName = false;

        // bakeAxisConversion available in 2020+
        try
        {
            var prop = typeof(ModelImporter).GetProperty("bakeAxisConversion");
            if (prop != null && prop.CanWrite)
                prop.SetValue(importer, false);
        }
        catch { /* ignore */ }

        // Materials: remap to our Material_1_baseColor if present
        var mat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Models/Humans/Jaina/Materials/Material_1_baseColor.mat");
        if (mat != null)
        {
            var map = importer.GetExternalObjectMap();
            // clear previous material remaps
            var keys = map.Keys.Where(k => k.type == typeof(Material)).ToList();
            foreach (var k in keys)
                importer.RemoveRemap(k);

            string[] names =
            {
                "Material_1_baseColor", "Material_1", "Material_1_baseColor.001",
                "Material_1_basecolor", "Jaina", "Jaina.001", "Material", "Material.001",
                "Body", "Mesh", "mesh", "No Name"
            };
            foreach (var n in names)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat);
            }
            L("Remapped materials -> Material_1_baseColor");
        }
        else
        {
            L("WARN: Material_1_baseColor.mat not found");
        }

        // Use default clips then rename (this is the correct Unity API)
        ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0)
        {
            // force reimport once to populate takes
            importer.SaveAndReimport();
            importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            defaults = importer.defaultClipAnimations;
        }

        L("defaultClipAnimations count: " + (defaults != null ? defaults.Length : 0));
        if (defaults != null && defaults.Length > 0)
        {
            for (int i = 0; i < defaults.Length; i++)
            {
                string take = defaults[i].takeName;
                string nice = "Clip_" + (i + 1).ToString("00");
                if (!string.IsNullOrEmpty(take) && take.Length < 40 && !take.Contains("-"))
                    nice = take; // keep readable names like Axe_Stance
                defaults[i].name = nice;
                defaults[i].loopTime = true;
                defaults[i].lockRootRotation = false;
                defaults[i].lockRootHeightY = false;
                defaults[i].lockRootPositionXZ = false;
                defaults[i].keepOriginalOrientation = true;
                defaults[i].keepOriginalPositionY = true;
                defaults[i].keepOriginalPositionXZ = true;
                L($"  clip[{i}] take='{take}' -> name='{nice}' frames {defaults[i].firstFrame}-{defaults[i].lastFrame}");
            }
            importer.clipAnimations = defaults;
        }
        else
        {
            // clear broken custom clips
            importer.clipAnimations = new ModelImporterClipAnimation[0];
            L("WARN: no default clips; cleared clipAnimations");
        }

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        L("Reimported FBX");

        // --- 2) Verify sub-assets ---
        var all = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        var animClips = all.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
        var meshes = all.OfType<Mesh>().ToList();
        var avatars = all.OfType<Avatar>().ToList();
        L($"Sub-assets: clips={animClips.Count}, meshes={meshes.Count}, avatars={avatars.Count}, total={all.Length}");
        foreach (var c in animClips)
            L($"  AnimationClip: '{c.name}' len={c.length:F2}s empty={c.empty}");

        // --- 3) Build oriented prefab ---
        var modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (modelRoot == null)
        {
            L("ERROR: cannot load FBX GameObject");
            WriteReport(log);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // Instantiate as prefab instance so materials/meshes stay linked
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelRoot);
        instance.name = "Model";

        // Try orientation candidates; pick tallest world bounds (standing character)
        Quaternion[] candidates =
        {
            Quaternion.Euler(90f, 0f, 0f),
            Quaternion.Euler(-90f, 0f, 0f),
            Quaternion.Euler(0f, 0f, 90f),
            Quaternion.Euler(0f, 0f, -90f),
            Quaternion.Euler(180f, 0f, 0f),
            Quaternion.identity,
            Quaternion.Euler(90f, 180f, 0f),
            Quaternion.Euler(-90f, 180f, 0f),
        };

        var root = new GameObject("Jaina");
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localScale = Vector3.one;

        Quaternion bestRot = candidates[0];
        float bestScore = float.NegativeInfinity;
        Bounds bestBounds = new Bounds();

        foreach (var rot in candidates)
        {
            instance.transform.localRotation = rot;
            // force update
            foreach (var t in root.GetComponentsInChildren<Transform>())
                t.hasChanged = true;

            Bounds b;
            if (!TryGetWorldBounds(root, out b))
                continue;

            // Standing score: height large, horizontal footprint smaller, min.y near 0 preferred
            float height = b.size.y;
            float footprint = Mathf.Max(b.size.x, b.size.z);
            float score = height * 2f - footprint;
            // prefer feet closer to ground after we shift later
            L($"  candidate euler={rot.eulerAngles} bounds={b.size} center={b.center} score={score:F2}");
            if (score > bestScore)
            {
                bestScore = score;
                bestRot = rot;
                bestBounds = b;
            }
        }

        instance.transform.localRotation = bestRot;
        L($"Chosen orientation euler={bestRot.eulerAngles} score={bestScore:F2}");

        // Recalc bounds and put feet on y=0
        Bounds finalBounds;
        if (TryGetWorldBounds(root, out finalBounds))
        {
            float dy = -finalBounds.min.y;
            instance.transform.localPosition += new Vector3(0f, dy, 0f);
            L($"Feet adjust dy={dy:F3}, bounds size={finalBounds.size}");
        }

        // Skinned mesh: avoid disappearing in animation preview/culling
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            smr.skinnedMotionVectors = true;
            if (smr.sharedMaterial == null && mat != null)
                smr.sharedMaterial = mat;
            // ensure material has albedo
            if (smr.sharedMaterials != null)
            {
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null && mat != null)
                        mats[i] = mat;
                }
                smr.sharedMaterials = mats;
            }
            L($"  SMR '{smr.name}' mats={smr.sharedMaterials?.Length} updateWhenOffscreen=1 bounds={smr.bounds.size}");
        }

        // Animator with first clip (optional controller-less: use Animation component for preview)
        var animator = root.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = instance.GetComponent<Animator>();
        if (animator != null)
        {
            L($"Animator avatar={(animator.avatar != null ? animator.avatar.name : "null")} applyRootMotion={animator.applyRootMotion}");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // Add Animation component for simple clip preview in editor
        var anim = root.GetComponent<Animation>();
        if (anim == null) anim = root.AddComponent<Animation>();
        anim.playAutomatically = false;
        anim.cullingType = AnimationCullingType.AlwaysAnimate;
        foreach (var clip in animClips)
        {
            // clips are sub-assets of FBX; Animation component needs them on same hierarchy paths
            // Wrap clip play on child model via legacy Animation only works if clip is legacy.
            // Prefer Animator. For Editor validation, just reference count.
            L($"  available clip for setup: {clip.name}");
        }

        // If clips are legacy-compatible, add them; otherwise create AnimatorController
        if (animClips.Count > 0)
        {
            string controllerPath = "Assets/Models/Humans/Jaina/Jaina.controller";
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(
                controllerPath, animClips[0]);
            // add other clips as states
            var sm = controller.layers[0].stateMachine;
            for (int i = 1; i < animClips.Count; i++)
            {
                var state = sm.AddState(animClips[i].name);
                state.motion = animClips[i];
            }
            if (animator == null)
                animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            L("Created AnimatorController: " + controllerPath);
            // remove legacy Animation to avoid conflict
            Object.DestroyImmediate(anim);
        }

        // Ensure folder
        if (!Directory.Exists("Assets/Prefabs"))
            Directory.CreateDirectory("Assets/Prefabs");

        // Save prefab
        if (File.Exists(PrefabPath))
            AssetDatabase.DeleteAsset(PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        L("Saved prefab: " + PrefabPath);

        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Final verification load
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            var psmr = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            L($"Prefab OK: SMRs={psmr.Length}, children={prefab.transform.childCount}");
        }
        else
        {
            L("ERROR: prefab failed to load");
        }

        WriteReport(log);
        L("DONE");
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    static void WriteReport(StringBuilder log)
    {
        try
        {
            File.WriteAllText(ReportPath, log.ToString());
            AssetDatabase.Refresh();
        }
        catch { /* ignore */ }
    }
}
