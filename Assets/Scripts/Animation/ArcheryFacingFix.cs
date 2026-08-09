using UnityEngine;

/// <summary>
/// While this Animator state is active, adds yaw to the character root
/// without altering animation curves (keeps the character upright).
/// Use on Archery_Shot_1 (and similar) states.
///
/// Prefab root should already stand with baseEuler (typically -90, 0, 0 for Meshy).
/// </summary>
public class ArcheryFacingFix : StateMachineBehaviour
{
    [Tooltip("Extra yaw (degrees) applied on top of Base Euler while in this state.")]
    public float yawOffset = 90f;

    [Tooltip("Resting local euler of the Animator transform (Sylvana model). Meshy bipeds usually need X=-90.")]
    public Vector3 baseEuler = new Vector3(-90f, 0f, 0f);

    [Tooltip("If set, rotate this transform instead of animator.transform.")]
    public string optionalChildName = "";

    Vector3 _savedEuler;
    bool _saved;
    Transform _target;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _target = ResolveTarget(animator);
        if (_target == null) return;

        _savedEuler = _target.localEulerAngles;
        _saved = true;

        // Keep upright base (X), only change facing (Y)
        _target.localEulerAngles = new Vector3(baseEuler.x, baseEuler.y + yawOffset, baseEuler.z);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!_saved || _target == null) return;
        // Restore base upright orientation (not the possibly-dirty saved if user scrubbed)
        _target.localEulerAngles = baseEuler;
        _saved = false;
        _target = null;
    }

    static Transform ResolveTarget(Animator animator)
    {
        // Prefer the transform that has the skeleton under it (animator root).
        return animator != null ? animator.transform : null;
    }
}
