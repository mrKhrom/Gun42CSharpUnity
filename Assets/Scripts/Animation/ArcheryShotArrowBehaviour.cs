using UnityEngine;

/// <summary>
/// StateMachineBehaviour for Archery_Shot_1: show in-hand arrow, release projectile by normalizedTime.
/// Does not require Animation Events on the clip.
/// </summary>
public class ArcheryShotArrowBehaviour : StateMachineBehaviour
{
    [Tooltip("Normalized time [0..1] when the arrow leaves the bow.")]
    [Range(0.05f, 0.95f)]
    public float releaseNormalizedTime = 0.55f;

    bool _released;
    ArcheryWeapon _weapon;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _released = false;
        _weapon = animator.GetComponentInParent<ArcheryWeapon>();
        if (_weapon == null)
            _weapon = animator.GetComponent<ArcheryWeapon>();
        _weapon?.OnArcheryStart();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_released || _weapon == null) return;

        // Use cycle-local time so loops still fire once per entry (we guard with _released).
        float t = stateInfo.normalizedTime;
        if (t >= 1f)
            t = t % 1f;

        if (t >= releaseNormalizedTime)
        {
            _released = true;
            _weapon.OnArrowRelease();
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _weapon?.OnArcheryEnd();
        _released = false;
        _weapon = null;
    }
}
