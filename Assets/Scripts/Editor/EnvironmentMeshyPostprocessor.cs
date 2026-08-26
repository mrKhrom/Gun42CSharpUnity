using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports Meshy environment FBX (trees, ground, buildings) with albedo/normal/metallic/roughness.
/// Applies to Assets/Models/Environment/** except NatureStarterKit2.
/// Menu: Tools/Setup Environment Meshy Models
/// </summary>
public class EnvironmentMeshyPostprocessor : AssetPostprocessor
{
    const string Root = "Assets/Models/Environment/";
    static bool s_Busy;

    static bool IsEnvRoot(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string n = path.Replace('\\', '/');
        if (!n.StartsWith(Root, System.StringComparison.OrdinalIgnoreCase)) return false;
        if (n.IndexOf("NatureStarterKit", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    static bool IsEnvFbx(string path)
    {
        return IsEnvRoot(path) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsEnvTexture(string path)
    {
        if (!IsEnvRoot(path)) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga";
    }

    void OnPreprocessTexture()
    {
        if (!IsEnvTexture(assetPath)) return;
        var imp = (TextureImporter)assetImporter;
        string stem = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        if (stem.EndsWith("_normal") || stem.Contains("_normal"))
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.sRGBTexture = false;
        }
        else if (stem.Contains("_metallic") || stem.Contains("_roughness")
                 || stem.Contains("_metallicsmoothness") || stem.Contains("_smoothness"))
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
        }
        else
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
        }
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 2048;
    }

    void OnPreprocessModel()
    {
        if (!IsEnvFbx(assetPath)) return;
        var imp = (ModelImporter)assetImporter;
        imp.animationType = ModelImporterAnimationType.None;
        imp.importAnimation = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        imp.materialSearch = ModelImporterMaterialSearch.Local;
        imp.globalScale = 1f;
        imp.addCollider = false;
        // File claims Y-up but vertex data is Z-up; bake does nothing. Stand up via MeshyModelOrient.
        imp.bakeAxisConversion = false;
        imp.preserveHierarchy = true;
        imp.useFileScale = true;

        var mat = FindOrCreateMaterial(assetPath);
        if (mat == null) return;

        foreach (var k in imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToList())
            imp.RemoveRemap(k);

        foreach (var n in MaterialNames(assetPath, mat))
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat);
    }

    void OnPostprocessModel(GameObject go)
    {
        if (!IsEnvFbx(assetPath)) return;
        var mat = FindOrCreateMaterial(assetPath);
        if (mat != null)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    r.sharedMaterial = mat;
                    continue;
                }
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        var orient = go.GetComponent<MeshyModelOrient>();
        if (orient == null)
            orient = go.AddComponent<MeshyModelOrient>();
        orient.eulerOffset = new Vector3(90f, 0f, 0f);
        orient.Apply();
    }

    [MenuItem("Tools/Setup Environment Meshy Models")]
    public static void SetupSelectedOrAll()
    {
        if (s_Busy) return;
        s_Busy = true;
        try
        {
            var paths = new List<string>();
            foreach (var obj in Selection.objects)
            {
                string p = AssetDatabase.GetAssetPath(obj);
                if (IsEnvFbx(p)) paths.Add(p);
            }
            if (paths.Count == 0)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root.TrimEnd('/') }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (IsEnvFbx(p)) paths.Add(p);
                }
            }
            foreach (var p in paths)
            {
                FindOrCreateMaterial(p);
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp != null)
                    imp.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[EnvironmentMeshy] Setup " + paths.Count + " FBX");
        }
        finally
        {
            s_Busy = false;
        }
    }

    static IEnumerable<string> MaterialNames(string fbxPath, Material mat)
    {
        var names = new List<string>
        {
            mat.name, "Material", "Material.001", "Material.002", "Material.003",
            "No Name", "Fbx Default Material", "DefaultMaterial",
            "lambert", "lambert1", "phong1", "Scene", "standardSurface1"
        };
        string stem = Path.GetFileNameWithoutExtension(fbxPath);
        names.Add(stem);
        names.Add(stem + ".png");
        for (int i = 0; i < 8; i++)
            names.Add("Mat_" + i);
        return names.Distinct();
    }

    static Material FindOrCreateMaterial(string fbxPath)
    {
        string folder = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
        string name = Path.GetFileNameWithoutExtension(fbxPath);
        string matFolder = folder + "/Materials";
        string matPath = matFolder + "/" + name + ".mat";

        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder(folder, "Materials");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        Shader shader = Shader.Find("Standard")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("HDRP/Lit");
        if (shader == null) return mat;

        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else if (mat.shader != shader)
            mat.shader = shader;

        ApplyTextures(mat, folder, name);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D LoadTex(string texFolder, string file)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/" + file);
    }

    static void ApplyTextures(Material mat, string folder, string name)
    {
        string texFolder = folder + "/Textures";
        if (!AssetDatabase.IsValidFolder(texFolder)) return;
        Texture2D albedo = LoadTex(texFolder, name + ".png");
        Texture2D normal = LoadTex(texFolder, name + "_Normal.png");
        Texture2D metallic = LoadTex(texFolder, name + "_Metallic.png");
        Texture2D packed = LoadTex(texFolder, name + "_MetallicSmoothness.png");
        if (albedo == null)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (tex == null) continue;
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (n.Contains("_metallicsmoothness")) packed = packed ?? tex;
                else if (n.Contains("_normal")) normal = normal ?? tex;
                else if (n.Contains("_metallic")) metallic = metallic ?? tex;
                else if (!n.Contains("_roughness") && albedo == null) albedo = tex;
            }
        }

        if (albedo != null)
        {
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
        }
        if (normal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }

        Texture2D metalMap = packed != null ? packed : metallic;
        if (metalMap != null && mat.HasProperty("_MetallicGlossMap"))
        {
            mat.SetTexture("_MetallicGlossMap", metalMap);
            mat.EnableKeyword("_METALLICGLOSSMAP");
            if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", 1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 1f);
        }
        else
        {
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.35f);
        }

        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 0f);
        if (mat.HasProperty("_Color")) mat.color = Color.white;
    }
}
