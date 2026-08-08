using UnityEngine;

/// <summary>
/// Fixes Meshy biped orientation: mesh is Y-up in FBX but authored lying on side.
/// Put on the ROOT of a raw Jaina FBX instance (or leave default on prefab Orient child).
/// </summary>
[ExecuteAlways]
public class MeshyModelOrient : MonoBehaviour
{
    public Vector3 eulerOffset = new Vector3(90f, 0f, 0f);

    void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Apply();
    }
#endif

    [ContextMenu("Apply Orient")]
    public void Apply()
    {
        transform.localEulerAngles = eulerOffset;
    }
}
