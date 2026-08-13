using UnityEngine;

/// <summary>
/// Keeps character Animator on a looped Idle state in Play Mode,
/// and samples Idle pose in the Editor so scene view does not show the first FBX take.
/// Put on unit root (e.g. WhiteKing) — finds Animator in children.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class IdleAnimationDriver : MonoBehaviour
{
    static readonly string[] IdleCandidates =
    {
        "Idle_02",
        "Idle_6",
        "Idle_5",
        "Idle",
        "Idle_01",
        "Idle_1"
    };

    [Tooltip("Leave empty to auto-pick Idle_02 / Idle_6 / ... from the controller")]
    [SerializeField] private string _idleStateName;

    [SerializeField] private Animator _animator;
    [SerializeField] private bool _sampleInEditMode = true;
    [SerializeField] private bool _forceIdleOnStart = true;

    private string _resolvedIdle;
    private int _resolvedHash;
    private bool _resolved;

    private void Awake()
    {
        CacheAnimator();
    }

    private void OnEnable()
    {
        CacheAnimator();
        ResolveIdleState(force: true);

        if (Application.isPlaying)
        {
            if (_forceIdleOnStart)
                PlayIdle(normalizedTime: 0f);
        }
        else if (_sampleInEditMode)
        {
            SampleEditModePose();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        CacheAnimator();
        ResolveIdleState(force: true);
        if (_forceIdleOnStart)
            PlayIdle(normalizedTime: 0f);
    }

    private void Update()
    {
        if (!Application.isPlaying || _animator == null)
            return;

        // If clip has no loop flag, restart Idle when it finishes
        if (!_animator.isInitialized)
            return;

        var info = _animator.GetCurrentAnimatorStateInfo(0);
        if (!info.loop && info.normalizedTime >= 0.98f && IsCurrentIdle(info))
            PlayIdle(normalizedTime: 0f);
    }

#if UNITY_EDITOR
    private double _nextEditSampleTime;

    private void EditorTick()
    {
        if (this == null || Application.isPlaying || !_sampleInEditMode)
        {
            UnityEditor.EditorApplication.update -= EditorTick;
            return;
        }

        if (UnityEditor.EditorApplication.timeSinceStartup < _nextEditSampleTime)
            return;

        _nextEditSampleTime = UnityEditor.EditorApplication.timeSinceStartup + 0.25;
        SampleEditModePose();
    }
#endif

    public void PlayIdle(float normalizedTime = 0f)
    {
        if (!CacheAnimator())
            return;

        ResolveIdleState(force: false);
        if (!_resolved || string.IsNullOrEmpty(_resolvedIdle))
            return;

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.Play(_resolvedHash, 0, Mathf.Clamp01(normalizedTime));
        _animator.Update(0f);
    }

    public void SampleEditModePose()
    {
        if (Application.isPlaying || !_sampleInEditMode)
            return;
        if (!CacheAnimator())
            return;

        ResolveIdleState(force: false);
        if (!_resolved || string.IsNullOrEmpty(_resolvedIdle))
            return;

        // Force evaluation of Idle frame 0 (standing pose)
        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.Play(_resolvedHash, 0, 0f);
        _animator.Update(0f);
    }

    private bool CacheAnimator()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        if (_animator == null)
            return false;

        // Root motion from Idle/walk clips would write Transform.position (often +Y/~0.5).
        // Tactics pieces must stay on scene pivots — disable at the source (Animator), not by snapping Y.
        if (_animator.applyRootMotion)
            _animator.applyRootMotion = false;

        return _animator.runtimeAnimatorController != null;
    }

    private void ResolveIdleState(bool force)
    {
        if (_resolved && !force)
            return;

        _resolved = false;
        _resolvedIdle = null;
        _resolvedHash = 0;

        if (_animator == null || _animator.runtimeAnimatorController == null)
            return;

        // Explicit override from inspector
        if (!string.IsNullOrEmpty(_idleStateName))
        {
            SetResolved(_idleStateName);
            return;
        }

        // Prefer known idle names (HasState may be false until Animator initializes in Edit Mode)
        foreach (var candidate in IdleCandidates)
        {
            if (ControllerHasClipOrState(candidate))
            {
                SetResolved(candidate);
                return;
            }
        }

        // Fallback: any clip with "Idle" in the name (state name matches clip in our controllers)
        var clips = _animator.runtimeAnimatorController.animationClips;
        if (clips != null)
        {
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (clip.name.IndexOf("Idle", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                SetResolved(clip.name);
                return;
            }
        }
    }

    private bool ControllerHasClipOrState(string name)
    {
        if (string.IsNullOrEmpty(name) || _animator == null)
            return false;

        if (_animator.isInitialized && HasState(name))
            return true;

        var clips = _animator.runtimeAnimatorController != null
            ? _animator.runtimeAnimatorController.animationClips
            : null;
        if (clips == null)
            return false;

        foreach (var clip in clips)
        {
            if (clip != null && clip.name == name)
                return true;
        }

        return false;
    }

    private void SetResolved(string stateName)
    {
        _resolvedIdle = stateName;
        _resolvedHash = Animator.StringToHash(stateName);
        _resolved = true;
    }

    private bool HasState(string stateName)
    {
        if (_animator == null || string.IsNullOrEmpty(stateName))
            return false;
        return _animator.HasState(0, Animator.StringToHash(stateName));
    }

    private bool IsCurrentIdle(AnimatorStateInfo info)
    {
        if (!_resolved)
            return false;
        return info.shortNameHash == _resolvedHash || info.IsName(_resolvedIdle);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _resolved = false;
        if (!isActiveAndEnabled)
            return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            CacheAnimator();
            ResolveIdleState(force: true);
            if (!Application.isPlaying && _sampleInEditMode)
                SampleEditModePose();
        };
    }
#endif
}
