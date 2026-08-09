using UnityEditor;
using UnityEngine;

/// <summary>
/// Forces textured materials on item models after import.
/// </summary>
public class FixItemMaterialsPostprocessor : AssetPostprocessor
{
    const string BowPath = "Assets/Models/Items/Bow/Bow.fbx";
    const string BowMatPath = "Assets/Models/Items/Bow/Materials/blinn1.mat";
    const string BowMatFallback = "Assets/Models/Items/Bow/Materials/Bow.mat";

    const string BowElfPath = "Assets/Models/Items/BowElf/BowElf.fbx";
    const string BowElfMatPath = "Assets/Models/Items/BowElf/Materials/blinn1.mat";
    const string BowElfMatAlt = "Assets/Models/Items/BowElf/Materials/Bow.fbx_blinn1_baseColor.mat";
    const string BowElfUnlit = "Assets/Models/Items/BowElf/Materials/BowElf_Unlit.mat";

    const string ArrowPath = "Assets/Models/Items/Arrow/Arrow.obj";
    const string ArrowMatPath = "Assets/Models/Items/Arrow/Materials/Arrow.mat";

    void OnPostprocessModel(GameObject root)
    {
        var path = assetPath.Replace("\\", "/");

        if (path == BowPath)
            AssignMaterial(root, Load(BowMatPath, BowMatFallback));
        else if (path == BowElfPath)
            AssignMaterial(root, Load(BowElfMatPath, BowElfMatAlt, BowElfUnlit));
        else if (path == ArrowPath)
            AssignMaterial(root, AssetDatabase.LoadAssetAtPath<Material>(ArrowMatPath));
    }

    static Material Load(params string[] paths)
    {
        foreach (var p in paths)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) return m;
        }
        return null;
    }

    static void AssignMaterial(GameObject root, Material mat)
    {
        if (root == null || mat == null)
        {
            Debug.LogWarning("[FixItemMaterials] Missing root or material");
            return;
        }

        int count = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                r.sharedMaterial = mat;
            }
            else
            {
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                r.sharedMaterials = mats;
            }
            count++;
        }
        Debug.Log($"[FixItemMaterials] Assigned '{mat.name}' (tex={mat.mainTexture}) to {count} renderer(s) on '{root.name}'");
    }

    [MenuItem("Tools/Items/Fix BowElf Materials In Scene")]
    static void FixBowElfInScene()
    {
        var mat = Load(BowElfMatPath, BowElfMatAlt, BowElfUnlit);
        if (mat == null)
        {
            EditorUtility.DisplayDialog("Fix BowElf", "Material not found", "OK");
            return;
        }

        int n = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            bool match = r.gameObject.name.ToLowerInvariant().Contains("bowelf")
                      || r.gameObject.name.ToLowerInvariant().Contains("evelynn")
                      || r.gameObject.name.ToLowerInvariant().Contains("bow");
            var path = "";
            var mf = r.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh)
                path = AssetDatabase.GetAssetPath(mf.sharedMesh).Replace("\\", "/");
            var smr = r as SkinnedMeshRenderer;
            if (smr && smr.sharedMesh)
                path = AssetDatabase.GetAssetPath(smr.sharedMesh).Replace("\\", "/");
            if (path.Contains("Models/Items/BowElf/"))
                match = true;
            // only BowElf folder for this menu, not old Bow
            if (!path.Contains("Models/Items/BowElf/") && !r.gameObject.name.ToLowerInvariant().Contains("bowelf")
                && !r.gameObject.name.ToLowerInvariant().Contains("evelynn"))
            {
                // if generic "bow" name but mesh from BowElf
                if (!path.Contains("BowElf"))
                    match = false;
            }
            if (!match) continue;

            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
                r.sharedMaterial = mat;
            else
            {
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            EditorUtility.SetDirty(r);
            n++;
        }
        EditorUtility.DisplayDialog("Fix BowElf", $"Fixed {n} renderer(s).\nMaterial: {mat.name}\nTexture: {mat.mainTexture}", "OK");
    }

    [MenuItem("Tools/Items/Reimport BowElf With Materials")]
    static void ReimportBowElf()
    {
        AssetDatabase.ImportAsset(BowElfPath, ImportAssetOptions.ForceUpdate);
    }

    [MenuItem("Tools/Items/Reimport Bow With Materials")]
    static void ReimportBow()
    {
        AssetDatabase.ImportAsset(BowPath, ImportAssetOptions.ForceUpdate);
    }

    [MenuItem("Tools/Items/Fix Bow Materials In Scene")]
    static void FixBowInScene()
    {
        var mat = Load(BowMatPath, BowMatFallback);
        if (mat == null) return;
        int n = 0;
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            var path = "";
            var mf = r.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh)
                path = AssetDatabase.GetAssetPath(mf.sharedMesh).Replace("\\", "/");
            if (!path.Contains("Models/Items/Bow/") || path.Contains("BowElf")) continue;
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) r.sharedMaterial = mat;
            else { for (int i = 0; i < mats.Length; i++) mats[i] = mat; r.sharedMaterials = mats; }
            EditorUtility.SetDirty(r);
            n++;
        }
        EditorUtility.DisplayDialog("Fix Bow", $"Fixed {n} renderer(s)", "OK");
    }
}
