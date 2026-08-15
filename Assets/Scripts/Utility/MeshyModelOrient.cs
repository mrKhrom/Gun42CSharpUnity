using UnityEngine;

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
