using UnityEngine;
using UnityEngine.Rendering;

public static class GraphicsPerformance
{
    public const int TargetFps = 60;
    public const float ShadowDistance = 18f;

public const int PreferredQualityLevel = 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyBeforeSceneLoad()
    {
        // Не гонять Ultra/Very High, если кто-то переключил вручную — мягко вернуть Medium+
        int q = QualitySettings.GetQualityLevel();
        if (q > PreferredQualityLevel)
            QualitySettings.SetQualityLevel(PreferredQualityLevel, applyExpensiveChanges: true);

        // Плавность: синхрон с монитором + потолок кадров (GPU не рисует 300+ FPS впустую)
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = TargetFps;

        // Тени: дешёвый realtime для маленькой доски
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadowDistance = ShadowDistance;
        QualitySettings.shadowNearPlaneOffset = 2f;

        QualitySettings.pixelLightCount = 1;
        QualitySettings.antiAliasing = 0;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
        QualitySettings.softVegetation = false;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.particleRaycastBudget = 32;
        QualitySettings.billboardsFaceCameraPosition = false;
    }

public static void OptimizeUnitShadowCasters()
    {
        var units = Object.FindObjectsOfType<Unit>(includeInactive: false);
        int reduced = 0;

        foreach (var unit in units)
        {
            if (unit == null) continue;

            var renderers = unit.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length <= 1)
                continue;

            Renderer best = null;
            float bestScore = -1f;

            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                // Приоритет: SkinnedMesh / Mesh с большим объёмом bounds
                var b = r.bounds;
                float score = b.size.x * b.size.y * b.size.z;
                if (r is SkinnedMeshRenderer) score *= 1.25f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = r;
                }
            }

            if (best == null) continue;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r == best)
                {
                    if (r.shadowCastingMode == ShadowCastingMode.Off)
                        r.shadowCastingMode = ShadowCastingMode.On;
                    continue;
                }

                if (r.shadowCastingMode != ShadowCastingMode.Off)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    reduced++;
                }
            }
        }

        if (reduced > 0)
            Debug.Log($"[GraphicsPerformance] Shadow casters reduced on multi-mesh units: off={reduced}");
    }
}
