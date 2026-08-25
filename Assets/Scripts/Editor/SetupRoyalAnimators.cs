#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Создаёт AnimatorController для MDX королей/ферзей и вешает на префабы.
public static class SetupRoyalAnimators
{
    struct RoyalSpec
    {
        public string PrefabPath;
        public string FbxPath;
        public string ControllerPath;
        public string IdleClip;
        public string WalkClip;
        public string AttackClip;
        public string DeathClip;
    }

    static readonly RoyalSpec[] Specs =
    {
        new RoyalSpec
        {
            PrefabPath = "Assets/Prefabs/WhiteKing.prefab",
            FbxPath = "Assets/Models/Humans/ArthasMDX/ArthasMDX.fbx",
            ControllerPath = "Assets/Models/Humans/ArthasMDX/ArthasMDX.controller",
            IdleClip = "Stand_1",
            WalkClip = "Walk",
            AttackClip = "Attack_1",
            DeathClip = "Death"
        },
        new RoyalSpec
        {
            PrefabPath = "Assets/Prefabs/BlackKing.prefab",
            FbxPath = "Assets/Models/Orcs/ThrallMDX/ThrallMDX.fbx",
            ControllerPath = "Assets/Models/Orcs/ThrallMDX/ThrallMDX.controller",
            IdleClip = "Stand_1",
            WalkClip = "Walk",
            AttackClip = "Attack_1",
            DeathClip = "Death"
        },
        new RoyalSpec
        {
            PrefabPath = "Assets/Prefabs/WhiteQween.prefab",
            FbxPath = "Assets/Models/Humans/JainaMDX/JainaMDX.fbx",
            ControllerPath = "Assets/Models/Humans/JainaMDX/JainaMDX.controller",
            IdleClip = "Stand_1",
            WalkClip = "Walk",
            AttackClip = "Attack_1",
            DeathClip = "Death"
        },
        new RoyalSpec
        {
            PrefabPath = "Assets/Prefabs/BlackQween.prefab",
            FbxPath = "Assets/Models/Orcs/SylvanasMDX/SylvanasMDX.fbx",
            ControllerPath = "Assets/Models/Orcs/SylvanasMDX/SylvanasMDX.controller",
            IdleClip = "Stand",
            WalkClip = "Walk",
            AttackClip = "Attack_1",
            DeathClip = "Death"
        },
    };

    [InitializeOnLoadMethod]
    static void AutoRunIfNeeded()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            bool missing = false;
            foreach (var s in Specs)
            {
                if (!File.Exists(s.ControllerPath))
                {
                    missing = true;
                    break;
                }
            }

            if (!missing)
                return;

            Debug.Log("[SetupRoyalAnimators] Контроллеры отсутствуют — автозапуск Setup…");
            Run();
        };
    }

    [MenuItem("Tools/Chess/Setup Royal Animators (King/Queen MDX)")]
    public static void Run()
    {
        int ok = 0;
        foreach (var spec in Specs)
        {
            if (SetupOne(spec))
                ok++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SetupRoyalAnimators] Готово: {ok}/{Specs.Length}");
    }

    static bool SetupOne(RoyalSpec spec)
    {
        if (!File.Exists(spec.FbxPath))
        {
            Debug.LogError($"[SetupRoyalAnimators] Нет FBX: {spec.FbxPath}");
            return false;
        }

        if (!File.Exists(spec.PrefabPath))
        {
            Debug.LogError($"[SetupRoyalAnimators] Нет префаба: {spec.PrefabPath}");
            return false;
        }

        var clips = LoadClips(spec.FbxPath);
        var idle = FindClip(clips, spec.IdleClip);
        var walk = FindClip(clips, spec.WalkClip);
        var attack = FindClip(clips, spec.AttackClip);
        var death = FindClip(clips, spec.DeathClip);

        if (idle == null || walk == null || attack == null || death == null)
        {
            Debug.LogError(
                $"[SetupRoyalAnimators] Клипы не найдены в {spec.FbxPath}. " +
                $"Idle={idle != null} Walk={walk != null} Attack={attack != null} Death={death != null}. " +
                $"Доступно: {string.Join(", ", clips.Select(c => c.name))}");
            return false;
        }

        var controller = BuildController(spec.ControllerPath, idle, walk, attack, death);
        if (controller == null)
            return false;

        // Назначить на префаб
        var root = PrefabUtility.LoadPrefabContents(spec.PrefabPath);
        try
        {
            var animators = root.GetComponentsInChildren<Animator>(true);
            if (animators == null || animators.Length == 0)
            {
                Debug.LogError($"[SetupRoyalAnimators] Нет Animator в {spec.PrefabPath}");
                return false;
            }

            // Предпочтительно Animator на MDX-модели
            Animator targetAnim = null;
            foreach (var a in animators)
            {
                if (a.gameObject.name.IndexOf("MDX", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    targetAnim = a;
                    break;
                }
            }
            if (targetAnim == null)
            {
                foreach (var a in animators)
                {
                    if (a.transform != root.transform)
                    {
                        targetAnim = a;
                        break;
                    }
                }
            }
            if (targetAnim == null)
                targetAnim = animators[0];

            targetAnim.runtimeAnimatorController = controller;
            targetAnim.applyRootMotion = false;
            targetAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(targetAnim);

            // UnitAnimationDriver на root
            var driver = root.GetComponent<UnitAnimationDriver>();
            if (driver == null)
                driver = root.AddComponent<UnitAnimationDriver>();

            var so = new SerializedObject(driver);
            so.FindProperty("_animator").objectReferenceValue = targetAnim;
            so.FindProperty("_idleStateName").stringValue = idle.name;
            so.FindProperty("_walkStateName").stringValue = walk.name;
            so.FindProperty("_attackStateName").stringValue = attack.name;
            so.FindProperty("_deathStateName").stringValue = death.name;
            so.FindProperty("_speedParameter").stringValue = "Speed";
            so.FindProperty("_forceIdleOnStart").boolValue = true;
            so.FindProperty("_forceIdleLoop").boolValue = true;
            so.FindProperty("_forceWalkLoop").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);

            PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath);
            Debug.Log(
                $"[SetupRoyalAnimators] {Path.GetFileName(spec.PrefabPath)} → {spec.ControllerPath} " +
                $"[{idle.name}/{walk.name}/{attack.name}/{death.name}] on Animator '{targetAnim.gameObject.name}'");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static AnimatorController BuildController(
        string controllerPath,
        AnimationClip idle,
        AnimationClip walk,
        AnimationClip attack,
        AnimationClip death)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(controllerPath) ?? "Assets");

        if (File.Exists(controllerPath))
            AssetDatabase.DeleteAsset(controllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // Параметр Speed как у Knight: >0.1 Walk, иначе Idle
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState(idle.name);
        idleState.motion = idle;

        var walkState = sm.AddState(walk.name);
        walkState.motion = walk;

        var attackState = sm.AddState(attack.name);
        attackState.motion = attack;

        var deathState = sm.AddState(death.name);
        deathState.motion = death;

        sm.defaultState = idleState;

        // Idle → Walk (Speed > 0.1)
        var toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.15f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // Walk → Idle (Speed < 0.1)
        var toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Any → Attack (trigger)
        var anyAttack = sm.AddAnyStateTransition(attackState);
        anyAttack.hasExitTime = false;
        anyAttack.duration = 0.08f;
        anyAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        // Attack → Idle
        var attackToIdle = attackState.AddTransition(idleState);
        attackToIdle.hasExitTime = true;
        attackToIdle.exitTime = 0.9f;
        attackToIdle.duration = 0.1f;

        // Death без выхода (one-shot)
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static List<AnimationClip> LoadClips(string fbxPath)
    {
        var list = new List<AnimationClip>();
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                list.Add(clip);
        }
        return list;
    }

    static AnimationClip FindClip(List<AnimationClip> clips, string name)
    {
        return clips.FirstOrDefault(c => c.name == name)
               ?? clips.FirstOrDefault(c => c.name.EndsWith(name));
    }
}
#endif
