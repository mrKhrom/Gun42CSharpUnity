using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
// CACHE_BUST_v3

/// <summary>
/// Собирает ОДИН prefab с 4 анимациями (Idle/Walk/Attack/Dead) из 4 Meshy FBX.
///
/// Silvana.fbx сам по себе всегда содержит только 1 клип (Idle) — это нормально.
/// Все 4 клипа живут на prefab: Animator Controller + CharacterAnimationPlayer.
///
/// Меню: Tools → Setup Silvana + Jaina Prefabs (4 animations)
/// При отсутствии prefab setup запускается автоматически после компиляции.
/// </summary>
[InitializeOnLoad]
public static class MeshyCharacterAnimatorSetup
{
    private static readonly string[] CharacterFolders =
    {
        "Assets/Models/Orcs/Silvana",
        "Assets/Models/Humans/Jaina"
    };

    private static readonly (string state, string suffix, bool loop)[] States =
    {
        ("Idle", "Idle", true),
        ("Walk", "Walk", true),
        ("Attack", "Attack", false),
        ("Dead", "Dead", false),
    };

    private const string AutoRanKey = "MeshyCharacterAnimatorSetup.AutoRan.v2";

    static MeshyCharacterAnimatorSetup()
    {
        // После recompile: если prefab'ов нет — собрать автоматически (1 раз за сессию).
        EditorApplication.delayCall += TryAutoSetup;
    }

    private static void TryAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SessionState.GetBool(AutoRanKey, false))
            return;

        var missing = false;
        foreach (var folder in CharacterFolders)
        {
            var name = Path.GetFileName(folder);
            var prefab = $"{folder}/{name}.prefab";
            var controller = $"{folder}/{name}.controller";
            if (!File.Exists(ToFullPath(prefab)) || !File.Exists(ToFullPath(controller)))
            {
                missing = true;
                break;
            }
        }

        if (!missing)
            return;

        SessionState.SetBool(AutoRanKey, true);
        Debug.Log("[Meshy] Prefab/controller missing — auto Setup Silvana + Jaina…");
        SetupAll(showDialog: false);
    }

    [MenuItem("Tools/Setup Silvana + Jaina Prefabs (4 animations)")]
    public static void SetupMenu()
    {
        SessionState.SetBool(AutoRanKey, true);
        SetupAll(showDialog: true);
    }

    /// <summary>Для batchmode: -executeMethod MeshyCharacterAnimatorSetup.SetupAllBatch</summary>
    public static void SetupAllBatch()
    {
        SetupAll(showDialog: false);
        EditorApplication.Exit(0);
    }

    public static void SetupAll(bool showDialog)
    {
        var errors = new List<string>();
        var created = new List<string>();

        foreach (var folder in CharacterFolders)
        {
            try
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    errors.Add("Folder missing: " + folder);
                    continue;
                }

                FixMaterials(folder);
                var prefabPath = BuildOnePrefabWithFourAnims(folder);
                created.Add(prefabPath);
            }
            catch (System.Exception ex)
            {
                errors.Add(folder + ": " + ex.Message);
                Debug.LogException(ex);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var msg = errors.Count == 0
            ? "Готово — 4 анимации на одном prefab:\n\n" +
              string.Join("\n", created) +
              "\n\nИспользуй .prefab (не .fbx)!\n" +
              "Silvana.fbx в Inspector → Animation всегда показывает 1 клип — это только Idle-файл.\n" +
              "На prefab: Animator (Idle/Walk/Attack/Dead) + CharacterAnimationPlayer."
            : "Частично/ошибки:\n" + string.Join("\n", errors) +
              (created.Count > 0 ? "\n\nСоздано:\n" + string.Join("\n", created) : "");

        Debug.Log("[Meshy] " + msg.Replace("\n", " | "));
        if (showDialog)
            EditorUtility.DisplayDialog("Silvana + Jaina", msg, "OK");
    }

    // -------------------------------------------------------------------------

    private static string ToFullPath(string assetPath)
    {
        // assetPath like Assets/...
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(projectRoot ?? "", assetPath);
    }

    private static void FixMaterials(string folder)
    {
        var charName = Path.GetFileName(folder);
        var matPath = $"{folder}/Materials/{charName}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Debug.LogWarning("[Meshy] No material at " + matPath);
            return;
        }

        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{charName}_Base_Color.png");
        var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{charName}_Normal.png");
        if (baseTex != null) mat.SetTexture("_MainTex", baseTex);
        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.color = Color.white;
        EditorUtility.SetDirty(mat);

        foreach (var path in FindAllFbx(folder))
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;

            ConfigureModelImporter(imp, mat, charName);
            imp.SaveAndReimport();
        }
    }

    private static void ConfigureModelImporter(ModelImporter imp, Material mat, string charName)
    {
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.materialLocation = ModelImporterMaterialLocation.External;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        if (mat == null) return;

        foreach (var n in new[]
                 {
                     "Material", "Material_1", "Material_1_baseColor", "Material_1_basecolor",
                     "texture_0", "Mat", "No Name", "DefaultMaterial", "Fbx Default Material", "default",
                     charName, "Silvana", "Jaina", "Sylvanas"
                 })
        {
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat);
        }

        foreach (var kv in imp.GetExternalObjectMap())
        {
            if (kv.Key.type == typeof(Material))
                imp.AddRemap(kv.Key, mat);
        }
    }

    private static string BuildOnePrefabWithFourAnims(string folder)
    {
        var charName = Path.GetFileName(folder);
        var baseFbx = $"{folder}/{charName}.fbx";
        var animDir = $"{folder}/Animations";
        var controllerPath = $"{folder}/{charName}.controller";
        var prefabPath = $"{folder}/{charName}.prefab";

        if (!File.Exists(ToFullPath(baseFbx)))
            throw new FileNotFoundException("Base FBX missing", baseFbx);

        // Ensure import settings on all FBX (Generic + Avatar + animations)
        foreach (var path in FindAllFbx(folder))
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        // Extract 4 clips → standalone .anim
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var (state, suffix, loop) in States)
        {
            var fbxPath = $"{animDir}/{charName}_{suffix}.fbx";
            if (!File.Exists(ToFullPath(fbxPath)))
                throw new FileNotFoundException("Anim FBX missing", fbxPath);

            var srcClip = LoadBestClip(fbxPath);
            if (srcClip == null)
                throw new System.Exception(
                    $"No AnimationClip in {fbxPath}. Open FBX → Animation → Import Animation = ON, Apply, re-run.");

            var animPath = $"{animDir}/{charName}_{state}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath) != null)
                AssetDatabase.DeleteAsset(animPath);

            // Copy clip as independent asset (not sub-asset of FBX)
            var copy = new AnimationClip();
            EditorUtility.CopySerialized(srcClip, copy);
            copy.name = $"{charName}_{state}";
            copy.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

            var so = new SerializedObject(copy);
            var loopProp = so.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopProp != null)
            {
                loopProp.boolValue = loop;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.CreateAsset(copy, animPath);
            var loaded = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (loaded == null)
                throw new System.Exception("Failed to create " + animPath);

            clips[state] = loaded;
            Debug.Log($"[Meshy] {charName}.{state} <= {srcClip.name} len={srcClip.length:F2}s from {fbxPath}");
        }

        // Controller with 4 states
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        // Remove default empty state that CreateAnimatorControllerAtPath may leave
        var sm = controller.layers[0].stateMachine;
        foreach (var c in sm.states.ToArray())
            sm.RemoveState(c.state);
        foreach (var t in sm.anyStateTransitions.ToArray())
            sm.RemoveAnyStateTransition(t);

        // Parameters
        if (!HasParam(controller, "Speed"))
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        if (!HasParam(controller, "Attack"))
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        if (!HasParam(controller, "Die"))
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorState idle = null, walk = null, attack = null, dead = null;
        var y = 0f;
        foreach (var (state, _, _) in States)
        {
            var st = sm.AddState(state, new Vector3(300, y, 0));
            st.motion = clips[state];
            y += 80f;
            if (state == "Idle") idle = st;
            if (state == "Walk") walk = st;
            if (state == "Attack") attack = st;
            if (state == "Dead") dead = st;
        }

        sm.defaultState = idle;

        if (idle != null && walk != null)
        {
            var t1 = idle.AddTransition(walk);
            t1.hasExitTime = false;
            t1.duration = 0.15f;
            t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var t2 = walk.AddTransition(idle);
            t2.hasExitTime = false;
            t2.duration = 0.15f;
            t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (attack != null)
        {
            var t = sm.AddAnyStateTransition(attack);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");

            if (idle != null)
            {
                var back = attack.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;
            }
        }

        if (dead != null)
        {
            var t = sm.AddAnyStateTransition(dead);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, "Die");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // ---- ONE PREFAB: root + model child + Animator + 4 clips ----
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(baseFbx);
        if (model == null)
            throw new System.Exception("Cannot load base model " + baseFbx);

        var root = new GameObject(charName);
        GameObject modelInstance;
        try
        {
            modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        }
        catch
        {
            modelInstance = Object.Instantiate(model);
        }

        modelInstance.name = "Model";
        modelInstance.transform.SetParent(root.transform, false);

        // Avatar from model BEFORE removing nested Animators
        Avatar avatar = null;
        foreach (var a in modelInstance.GetComponentsInChildren<Animator>(true))
        {
            if (a.avatar != null)
            {
                avatar = a.avatar;
                break;
            }
        }

        // Fallback: load avatar sub-asset from base FBX
        if (avatar == null)
        {
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(baseFbx))
            {
                if (sub is Avatar av)
                {
                    avatar = av;
                    break;
                }
            }
        }

        foreach (var a in modelInstance.GetComponentsInChildren<Animator>(true))
            Object.DestroyImmediate(a);

        var animator = root.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var player = root.AddComponent<CharacterAnimationPlayer>();
        player.idle = clips["Idle"];
        player.walk = clips["Walk"];
        player.attack = clips["Attack"];
        player.dead = clips["Dead"];

        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{folder}/Materials/{charName}.mat");
        if (mat != null)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var arr = r.sharedMaterials;
                for (var i = 0; i < arr.Length; i++)
                    arr[i] = mat;
                r.sharedMaterials = arr;
            }
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            AssetDatabase.DeleteAsset(prefabPath);

        var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        if (saved == null)
            throw new System.Exception("SaveAsPrefabAsset failed: " + prefabPath);

        // Verify prefab has controller with 4 motions
        var verifyAnimator = saved.GetComponent<Animator>();
        var verifyPlayer = saved.GetComponent<CharacterAnimationPlayer>();
        var ctrl = verifyAnimator != null ? verifyAnimator.runtimeAnimatorController as AnimatorController : null;
        var stateCount = ctrl != null ? ctrl.layers[0].stateMachine.states.Length : 0;

        Debug.Log(
            $"[Meshy] CREATED PREFAB with {stateCount} states: {prefabPath}\n" +
            $"  Idle={clips["Idle"].name}, Walk={clips["Walk"].name}, " +
            $"Attack={clips["Attack"].name}, Dead={clips["Dead"].name}\n" +
            $"  Avatar={(avatar != null ? avatar.name : "NULL")}, " +
            $"Player clips={(verifyPlayer != null && verifyPlayer.idle && verifyPlayer.walk && verifyPlayer.attack && verifyPlayer.dead ? "OK" : "MISSING")}");

        return prefabPath;
    }

    private static bool HasParam(AnimatorController c, string name)
    {
        foreach (var p in c.parameters)
            if (p.name == name) return true;
        return false;
    }

    private static AnimationClip LoadBestClip(string fbxPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        return assets
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__"))
            .OrderByDescending(c => c.length)
            .FirstOrDefault();
    }

    private static IEnumerable<string> FindAllFbx(string folder)
    {
        return AssetDatabase.FindAssets("t:Model", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase));
    }
}
