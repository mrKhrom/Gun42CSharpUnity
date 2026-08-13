using UnityEngine;

/// <summary>
/// Bow attack helper: shows in-hand arrow during Archery_Shot_1, then spawns a flying projectile.
/// Put on unit root (BlackQween). Animator may be on a child (Sylvana).
/// </summary>
public class ArcheryWeapon : MonoBehaviour
{
    [Header("Hand")]
    [SerializeField] private Transform _arrowSocket;
    [SerializeField] private GameObject _arrowInHand;

    [Header("Projectile")]
    [SerializeField] private ArrowProjectile _projectilePrefab;
    [SerializeField] private float _speed = 12f;
    [SerializeField] private float _maxDistance = 5f;
    [Tooltip("If true, fly along arrow socket forward; else unit transform.forward")]
    [SerializeField] private bool _useSocketForward = true;

    [Header("Animator")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _attackTrigger = "Attack";

    private bool _released;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        if (_arrowInHand != null)
            _arrowInHand.SetActive(false);
    }

    /// <summary>Test / gameplay: play Archery_Shot_1 via Attack trigger.</summary>
    public void PlayArcheryAttack()
    {
        if (_animator == null)
        {
            Debug.LogWarning("[ArcheryWeapon] Animator not found.", this);
            return;
        }
        _animator.SetTrigger(_attackTrigger);
    }

    /// <summary>Animation start / SMB OnStateEnter.</summary>
    public void OnArcheryStart()
    {
        _released = false;
        if (_arrowInHand != null)
            _arrowInHand.SetActive(true);
    }

    /// <summary>Release frame / SMB threshold.</summary>
    public void OnArrowRelease()
    {
        if (_released) return;
        _released = true;

        if (_arrowInHand != null)
            _arrowInHand.SetActive(false);

        if (_projectilePrefab == null)
        {
            Debug.LogWarning("[ArcheryWeapon] Projectile prefab missing.", this);
            return;
        }

        Transform origin = _arrowSocket != null ? _arrowSocket : transform;
        Vector3 pos = origin.position;
        Quaternion rot = origin.rotation;

        Vector3 dir;
        if (_useSocketForward && _arrowSocket != null)
            dir = _arrowSocket.forward;
        else
            dir = transform.forward;

        var proj = Instantiate(_projectilePrefab, pos, rot);
        proj.Launch(dir, _speed, _maxDistance);
    }

    /// <summary>Animation end / SMB OnStateExit.</summary>
    public void OnArcheryEnd()
    {
        if (_arrowInHand != null)
            _arrowInHand.SetActive(false);
        _released = false;
    }

    // --- Optional Animation Event names (same methods) ---
    // Wire events to these if using .anim events on the Animator object via proxy.
}
