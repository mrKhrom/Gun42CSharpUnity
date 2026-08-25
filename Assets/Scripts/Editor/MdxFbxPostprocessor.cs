using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Generic importer for FBX files produced by mdx2fbx.
/// Copy to Assets/Scripts/Editor/.
///
/// Detects converted files by the "mdx2fbx" header or a sibling .anim.json / .mdx.
/// Menu: Tools/Setup MDX Model  — runs on the selected FBX (or all selected).
/// Does not SaveAndReimport from OnPostprocessAllAssets (avoids import loops).
/// </summary>
public class MdxFbxPostprocessor : AssetPostprocessor
{
    static bool s_Busy;

    static bool IsMdxFbx(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return false;
        if (!File.Exists(path) && !File.Exists(ToAbs(path))) return false;

        string json = Path.ChangeExtension(path, ".anim.json");
        string mdx = Path.ChangeExtension(path, ".mdx");
        if (File.Exists(json) || File.Exists(ToAbs(json))) return true;
        if (File.Exists(mdx) || File.Exists(ToAbs(mdx))) return true;

        try
        {
            string abs = ToAbs(path);
            using (var reader = new StreamReader(abs))
            {
                for (int i = 0; i < 12 && !reader.EndOfStream; i++)
                {
                    string line = reader.ReadLine();
                    if (line != null && line.IndexOf("mdx2fbx", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    static string ToAbs(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return assetPath;
        if (Path.IsPathRooted(assetPath)) return assetPath;
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }

    static string FolderOf(string assetPath)
    {
        return Path.GetDirectoryName(assetPath)?.Replace("\\", "/") ?? "Assets";
    }

    void OnPreprocessModel()
    {
        if (!IsMdxFbx(assetPath)) return;

        var imp = (ModelImporter)assetImporter;
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.animationCompression = ModelImporterAnimationCompression.Off;
        imp.resampleCurves = false;
        imp.weldVertices = false;
        imp.optimizeBones = false;
        imp.importVisibility = false;
        imp.preserveHierarchy = true;
        imp.importCameras = false;
        imp.importLights = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        imp.materialSearch = ModelImporterMaterialSearch.Local;

        foreach (var k in imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
            imp.RemoveRemap(k);

        var primary = FindOrCreateMaterial(assetPath);
        var extras = ExtraAtlasMaterials(assetPath, primary);
        var extraStems = new HashSet<string>(extras.Select(e => e.name), System.StringComparer.OrdinalIgnoreCase);

        if (primary != null)
        {
            var names = new List<string>
            {
                primary.name, "Material", "No Name", "Fbx Default Material", "DefaultMaterial",
                "lambert", "lambert1", "Scene"
            };
            for (int i = 0; i < 24; i++)
                names.Add("Mat_" + i);
            foreach (var n in ReadFbxMaterialNames(assetPath))
                names.Add(n);
            foreach (var n in ReadFbxTextureStems(assetPath))
                names.Add(n);
            string primaryStem = PrimaryTextureStem(primary);
            if (!string.IsNullOrEmpty(primaryStem))
            {
                names.Add(primaryStem);
                names.Add(primaryStem + ".png");
                for (int i = 0; i < 24; i++)
                    names.Add("Mat_" + i + "_" + primaryStem);
            }
            string matFolder = FolderOf(assetPath) + "/Materials";
            if (AssetDatabase.IsValidFolder(matFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { matFolder }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (m == null || extraStems.Contains(m.name)) continue;
                    names.Add(m.name);
                }
            }
            foreach (var n in names.Distinct())
            {
                if (extraStems.Contains(n)) continue;
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), primary);
            }
        }

        // Extra atlases (Factory, Uther, Sorceress) override matching Mat_N_Stem.
        foreach (var extra in extras)
        {
            string stem = extra.name;
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), stem), extra);
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), stem + ".png"), extra);
            for (int i = 0; i < 24; i++)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Mat_" + i + "_" + stem), extra);
            foreach (var n in ReadFbxMaterialNames(assetPath))
            {
                if (n.Equals(stem, System.StringComparison.OrdinalIgnoreCase)
                    || n.EndsWith("_" + stem, System.StringComparison.OrdinalIgnoreCase))
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), extra);
            }
        }
    }

    void OnPostprocessModel(GameObject g)
    {
        if (!IsMdxFbx(assetPath)) return;

        var mat = FindExistingMaterial(assetPath);
        foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            smr.quality = SkinQuality.Bone4;
            if (mat == null) continue;
            var mats = smr.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                smr.sharedMaterial = mat;
                continue;
            }
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !HasAlbedo(mats[i]))
                    mats[i] = mat;
            }
            smr.sharedMaterials = mats;
        }

        var animator = g.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
    {
        if (s_Busy || imported == null) return;
        var files = imported.Where(IsMdxFbx).ToList();
        if (files.Count == 0) return;

        EditorApplication.delayCall += () =>
        {
            if (s_Busy) return;
            s_Busy = true;
            try
            {
                foreach (var path in files)
                    ApplyClipSettings(path);
            }
            finally
            {
                s_Busy = false;
            }
        };
    }

    static bool ShouldLoop(string take)
    {
        if (string.IsNullOrEmpty(take)) return false;
        var low = take.ToLowerInvariant();
        if (low.Contains("hit") || low.StartsWith("attack") || low.StartsWith("death") || low.StartsWith("decay")
            || low.StartsWith("dissipate") || low.StartsWith("spell") || low.StartsWith("morph"))
            return false;
        if (low.StartsWith("stand") || low.StartsWith("walk") || low.StartsWith("portrait"))
            return true;
        if (low.StartsWith("globalseq")) return true;
        return false;
    }

    static bool ApplyClipSettings(string fbxPath)
    {
        var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null) return false;

        var defaults = imp.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0)
        {
            if (imp.clipAnimations != null && imp.clipAnimations.Length > 0)
            {
                imp.clipAnimations = new ModelImporterClipAnimation[0];
                EditorUtility.SetDirty(imp);
            }
            return false;
        }

        var clips = new ModelImporterClipAnimation[defaults.Length];
        for (int i = 0; i < defaults.Length; i++)
        {
            var c = defaults[i];
            bool loop = ShouldLoop(c.takeName);
            c.loopTime = loop;
            c.loop = loop;
            c.name = string.IsNullOrEmpty(c.takeName) ? c.name : c.takeName;
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionY = true;
            c.keepOriginalPositionXZ = true;
            clips[i] = c;
        }

        if (ClipsMatch(imp.clipAnimations, clips))
            return false;

        imp.clipAnimations = clips;
        EditorUtility.SetDirty(imp);
        return true;
    }

    static bool ClipsMatch(ModelImporterClipAnimation[] current, ModelImporterClipAnimation[] desired)
    {
        if (current == null || current.Length != desired.Length) return false;
        for (int i = 0; i < desired.Length; i++)
        {
            var a = current[i];
            var b = desired[i];
            if (a.takeName != b.takeName || a.name != b.name) return false;
            if (a.loopTime != b.loopTime || a.loop != b.loop) return false;
        }
        return true;
    }

    static Material FindExistingMaterial(string fbxPath)
    {
        string folder = FolderOf(fbxPath);
        string name = Path.GetFileNameWithoutExtension(fbxPath);
        var candidates = new[]
        {
            folder + "/Materials/" + name + ".mat",
            folder + "/" + name + ".mat",
            folder + "/Materials/" + StripVersion(name) + ".mat",
        };
        foreach (var c in candidates)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(c);
            if (mat != null) return mat;
        }
        string matFolder = folder + "/Materials";
        if (AssetDatabase.IsValidFolder(matFolder))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { matFolder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null) return mat;
            }
        }
        return null;
    }

    static Material FindOrCreateMaterial(string fbxPath)
    {
        var existing = FindExistingMaterial(fbxPath);
        if (existing != null)
        {
            EnsureAlbedo(existing, FolderOf(fbxPath));
            return existing;
        }

        string folder = FolderOf(fbxPath);
        string name = StripVersion(Path.GetFileNameWithoutExtension(fbxPath));
        string matFolder = folder + "/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder(folder, "Materials");
        string matPath = matFolder + "/" + name + ".mat";

        Shader shader = Shader.Find("Standard")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("HDRP/Lit");
        if (shader == null) return null;

        var mat = new Material(shader) { name = name };
        var tex = FindAlbedo(folder);
        if (tex != null)
        {
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        }
        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }

    static bool HasAlbedo(Material m)
    {
        if (m == null) return false;
        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) return true;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return true;
        return false;
    }

    static void EnsureAlbedo(Material mat, string folder)
    {
        if (mat == null || HasAlbedo(mat)) return;
        var tex = FindAlbedo(folder);
        if (tex == null) return;
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);
    }

    static string PrimaryTextureStem(Material primary)
    {
        if (primary == null) return "";
        Texture tex = null;
        if (primary.HasProperty("_MainTex")) tex = primary.GetTexture("_MainTex");
        if (tex == null && primary.HasProperty("_BaseMap")) tex = primary.GetTexture("_BaseMap");
        if (tex == null) return "";
        string path = AssetDatabase.GetAssetPath(tex);
        return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
    }

    static List<string> ReadFbxMaterialNames(string assetPath)
    {
        var names = new List<string>();
        string abs = ToAbs(assetPath);
        if (!File.Exists(abs)) return names;
        try
        {
            foreach (var line in File.ReadLines(abs))
            {
                int i = line.IndexOf("Material::", System.StringComparison.Ordinal);
                if (i < 0) continue;
                int a = i + "Material::".Length;
                int b = line.IndexOf('"', a);
                if (b > a)
                    names.Add(line.Substring(a, b - a));
            }
        }
        catch
        {
            /* ignore unreadable ASCII */
        }
        return names;
    }

    static List<string> ReadFbxTextureStems(string assetPath)
    {
        var names = new List<string>();
        string abs = ToAbs(assetPath);
        if (!File.Exists(abs)) return names;
        try
        {
            foreach (var line in File.ReadLines(abs))
            {
                int i = line.IndexOf("RelativeFilename:", System.StringComparison.Ordinal);
                if (i < 0) continue;
                int a = line.IndexOf('"', i);
                int b = line.LastIndexOf('"');
                if (a < 0 || b <= a) continue;
                string fn = line.Substring(a + 1, b - a - 1);
                string file = Path.GetFileName(fn);
                if (string.IsNullOrEmpty(file)) continue;
                string low = file.ToLowerInvariant();
                if (low.StartsWith("teamcolor") || low.StartsWith("teamglow")) continue;
                names.Add(file);
                names.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
        catch
        {
            /* ignore unreadable ASCII */
        }
        return names;
    }

    static List<Material> ExtraAtlasMaterials(string fbxPath, Material primary)
    {
        var result = new List<Material>();
        string folder = FolderOf(fbxPath);
        string texFolder = folder + "/Textures";
        string matFolder = folder + "/Materials";
        Texture primaryTex = primary != null && primary.HasProperty("_MainTex") ? primary.GetTexture("_MainTex") : null;
        if (primaryTex == null && primary != null && primary.HasProperty("_BaseMap"))
            primaryTex = primary.GetTexture("_BaseMap");

        var candidates = new List<(string stem, string path, Texture2D tex)>();
        string[] roots = { folder, texFolder };
        var seen = new HashSet<string>();
        foreach (var root in roots)
        {
            if (!AssetDatabase.IsValidFolder(root)) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fn = Path.GetFileName(path);
                string stem = Path.GetFileNameWithoutExtension(fn);
                string low = fn.ToLowerInvariant();
                if (low.StartsWith("teamcolor") || low.StartsWith("teamglow")) continue;
                if (!seen.Add(stem.ToLowerInvariant())) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                candidates.Add((stem, path, tex));
            }
        }

        // One painted atlas (Arthas_Swordd.png): every mesh uses the primary mat.
        // Extra Factory/Uther remaps were painting the cape blue and pauldrons wood.
        if (candidates.Count <= 1)
            return result;

        foreach (var (stem, path, tex) in candidates)
        {
            if (tex == primaryTex) continue;
            string primaryStem = primaryTex != null
                ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(primaryTex))
                : "";
            if (!string.IsNullOrEmpty(primaryStem)
                && (string.Equals(stem, primaryStem, System.StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith(primaryStem + "_", System.StringComparison.OrdinalIgnoreCase)
                    || primaryStem.StartsWith(stem + "_", System.StringComparison.OrdinalIgnoreCase)))
                continue;

            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder(folder, "Materials");
            string matPath = matFolder + "/" + stem + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Standard")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("HDRP/Lit");
                if (shader == null) continue;
                mat = new Material(shader) { name = stem };
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            result.Add(mat);
        }
        return result;
    }

    static Texture2D FindAlbedo(string folder)
    {
        string[] roots = { folder + "/Textures", folder };
        foreach (var root in roots)
        {
            if (!AssetDatabase.IsValidFolder(root) && root != folder) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fn = Path.GetFileName(path).ToLowerInvariant();
                if (fn.StartsWith("teamcolor") || fn.StartsWith("teamglow")) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }
        }
        return null;
    }

    static string StripVersion(string name)
    {
        // KnightV2 / JainaV2 / ThrallAlternateV2 → Knight / Jaina / ThrallAlternate
        if (name.Length > 2 && (name.EndsWith("V2") || name.EndsWith("v2")))
            return name.Substring(0, name.Length - 2);
        return name;
    }

    static void BuildControllerAndPrefab(string fbxPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null)
        {
            Debug.LogError("[mdx2fbx] FBX not loaded: " + fbxPath);
            return;
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__"))
            .GroupBy(c => c.name)
            .Select(g => g.First())
            .OrderBy(c => c.name)
            .ToList();

        string folder = FolderOf(fbxPath);
        string name = StripVersion(Path.GetFileNameWithoutExtension(fbxPath));
        string controllerPath = folder + "/" + name + ".controller";
        string prefabPath = "Assets/Prefabs/" + name + ".prefab";

        AnimatorController ctrl = null;
        if (clips.Count > 0)
        {
            if (File.Exists(controllerPath))
                AssetDatabase.DeleteAsset(controllerPath);
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var st in sm.states.ToList())
                sm.RemoveState(st.state);

            if (!ctrl.parameters.Any(p => p.name == "Attack"))
                ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            if (!ctrl.parameters.Any(p => p.name == "Speed"))
                ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var byName = new Dictionary<string, AnimatorState>();
            float y = 0f;
            foreach (var clip in clips)
            {
                var state = sm.AddState(clip.name, new Vector3(280f, y, 0f));
                state.motion = clip;
                byName[clip.name] = state;
                y += 50f;
            }

            AnimatorState idle = byName.Values.First();
            foreach (var key in new[] { "Stand_1", "Stand", "Stand_Ready", "Walk" })
            {
                if (byName.TryGetValue(key, out idle))
                    break;
            }
            sm.defaultState = idle;

            if (byName.TryGetValue("Walk", out var walk) && idle != null && idle != walk)
            {
                var toWalk = idle.AddTransition(walk);
                toWalk.hasExitTime = false;
                toWalk.duration = 0.15f;
                toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                var toIdle = walk.AddTransition(idle);
                toIdle.hasExitTime = false;
                toIdle.duration = 0.15f;
                toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }

            AnimatorState attack = null;
            foreach (var key in new[] { "Attack_1", "Attack", "Spell" })
            {
                if (byName.TryGetValue(key, out attack))
                    break;
            }
            if (attack != null && idle != null && attack != idle)
            {
                var toAtk = idle.AddTransition(attack);
                toAtk.hasExitTime = false;
                toAtk.duration = 0.08f;
                toAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                var back = attack.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.12f;
            }
            EditorUtility.SetDirty(ctrl);
        }

        var mat = FindOrCreateMaterial(fbxPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Model";
        var root = new GameObject(name);
        instance.transform.SetParent(root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.updateWhenOffscreen = true;
            if (mat == null) continue;
            var mats = smr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !HasAlbedo(mats[i]))
                    mats[i] = mat;
            }
            if (mats.Length == 0) smr.sharedMaterial = mat;
            else smr.sharedMaterials = mats;
        }

        var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (ctrl != null)
            animator.runtimeAnimatorController = ctrl;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[mdx2fbx] Ready: {prefabPath} | clips={clips.Count} | mat={(mat ? mat.name : "none")}");
    }

    [MenuItem("Tools/Setup MDX Model")]
    public static void Run()
    {
        if (s_Busy) return;
        var paths = Selection.assetGUIDs
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsMdxFbx)
            .ToList();
        if (paths.Count == 0)
        {
            Debug.LogWarning("[mdx2fbx] Select one or more mdx2fbx .fbx files, then Tools/Setup MDX Model.");
            return;
        }

        s_Busy = true;
        try
        {
            foreach (var path in paths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            foreach (var path in paths)
                ApplyClipSettings(path);
            AssetDatabase.SaveAssets();
            foreach (var path in paths)
                BuildControllerAndPrefab(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[mdx2fbx] Setup failed: " + ex);
        }
        finally
        {
            s_Busy = false;
        }
    }
}
