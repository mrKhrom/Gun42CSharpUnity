using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// TMP needs a Font Asset, not a raw TTF. Creates Varnyx-Regular SDF if missing.
/// </summary>
[InitializeOnLoad]
public static class CreateVarnyxTmpFont
{
    const string TtfPath = "Assets/Fonts/Varnyx-Regular.ttf";
    const string SdfPath = "Assets/Fonts/Varnyx-Regular SDF.asset";

    static CreateVarnyxTmpFont()
    {
        EditorApplication.delayCall += EnsureFontAsset;
    }

    [MenuItem("Tools/Create Varnyx TMP Font Asset")]
    public static void EnsureFontAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath) != null)
            return;

        var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (font == null)
        {
            Debug.LogWarning("[Varnyx TMP] Не найден " + TtfPath);
            return;
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);

        if (fontAsset == null)
        {
            Debug.LogError(
                "[Varnyx TMP] Не удалось создать Font Asset. " +
                "В Inspector у TTF включи Include Font Data.");
            return;
        }

        fontAsset.name = "Varnyx-Regular SDF";
        AssetDatabase.CreateAsset(fontAsset, SdfPath);

        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
            && fontAsset.atlasTextures[0] != null)
        {
            fontAsset.atlasTextures[0].name = "Varnyx-Regular SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = "Varnyx-Regular SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        fontAsset.TryAddCharacters(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?;:'\"-()[]/");

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Varnyx TMP] Font Asset создан: " + SdfPath);
    }
}
