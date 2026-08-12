using UnityEngine;

/// <summary>
/// Place on the same GameObject as Animator so Animation Events can reach ArcheryWeapon on a parent.
/// </summary>
public class ArcheryWeaponProxy : MonoBehaviour
{
    [SerializeField] private ArcheryWeapon _weapon;

    private void Awake()
    {
        if (_weapon == null)
            _weapon = GetComponentInParent<ArcheryWeapon>();
    }

    public void OnArcheryStart() => _weapon?.OnArcheryStart();
    public void OnArrowRelease() => _weapon?.OnArrowRelease();
    public void OnArcheryEnd() => _weapon?.OnArcheryEnd();
}
