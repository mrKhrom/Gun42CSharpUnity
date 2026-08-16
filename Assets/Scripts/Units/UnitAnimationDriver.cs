using System;
using System.Collections;
using UnityEngine;

// Единый драйвер анимаций фигуры: Idle (всегда loop) / Walk / Attack / Death + поворот + sink.
// Вызовы Walk/Attack/Death — из PlayerController. Имя фигуры — в warning'ах.
[DisallowMultipleComponent]
[ExecuteAlways]
public class UnitAnimationDriver : MonoBehaviour
{
    static readonly string[] IdleCandidates =
    {
        "Idle_02", "Idle_6", "Idle_5", "Idle", "Idle_01", "Idle_1",
        "Stand_1", "Stand1", "Stand_Ready", "StandReady", "Stand",
        "GruntStand", "WolfRider_Stand", "Idle1", "Idle2", "IdleReady",
        "Tauren_ Idle1", "Tauren_IdleReady"
    };

    static readonly string[] WalkCandidates =
    {
        "Walking", "Walk", "Running", "Run",
        "Militia_Walk", "FootmanWalk", "RiflemanWalk",
        "Spear_Walk", "Walk_Turn_Right"
    };

    static readonly string[] AttackCandidates =
    {
        "Attack", "Axe_Spin_Attack", "Double_Combo_Attack", "Triple_Combo_Attack",
        "Basic_Attack", "Slash", "Strike"
    };

    static readonly string[] DeathCandidates =
    {
        "Dead", "Death", "Fall_Dead_from_Abdominal_Injury", "Die", "Death_1"
    };

    const float DirEpsilonSqr = 1e-6f;

    [SerializeField] private Animator _animator;

    [Header("States (имена STATE в Animator Controller, не только clip)")]
    [SerializeField] private string _idleStateName;
    [SerializeField] private string _walkStateName;
    [SerializeField] private string _attackStateName;
    [SerializeField] private string _deathStateName;

    [Header("Playback")]
    [SerializeField] private float _crossFade = 0.1f;
    [SerializeField] private bool _forceIdleOnStart = true;
    [SerializeField] private bool _sampleInEditMode = true;
    [SerializeField] private bool _forceIdleLoop = true;
    [SerializeField] private bool _forceWalkLoop = true;

    [Header("Turn")]
    [SerializeField] private float _turnSpeed = 540f;
    [Tooltip("Чёрные уже смотрят в центр (mesh/−Z). true = не крутить их на 180° при ходе вперёд.")]
    [SerializeField] private bool _blackFacesBoardCenter = true;

    [Header("Attack hit")]
    [Tooltip("Через сколько секунд после старта Attack запустить Death (провал) жертвы.")]
    [SerializeField] private float _attackHitTime = 0.15f;

    [Header("Death sink")]
    [SerializeField] private float _sinkDepth = 1.5f;
    [SerializeField] private float _sinkDuration = 0.6f;
    [SerializeField] private float _stateNormalizeWait = 0.95f;
    [SerializeField] private bool _destroyAfterDeath = true;

    public float AttackHitTime => Mathf.Max(0f, _attackHitTime);

    public bool IsBusy => _busyCount > 0 || _dead;
    public bool IsDead => _dead;

    // Для логов: имя GO + Team/Type если есть Unit
    public string FigureLabel
    {
        get
        {
            var u = GetComponent<Unit>();
            if (u != null)
                return $"{gameObject.name} [{u.Team}/{u.Type}]";
            return gameObject.name;
        }
    }

    string _resolvedIdle;
    string _resolvedWalk;
    string _resolvedAttack;
    string _resolvedDeath;
    int _idleHash;
    int _walkHash;
    int _attackHash;
    int _deathHash;
    bool _statesResolved;

    bool _dead;
    bool _walking;
    bool _wantIdle;
    int _busyCount;
    bool _loggedMissingIdle;
    bool _loggedMissingWalk;
    bool _loggedMissingAttack;
    bool _loggedMissingDeath;

    void Awake()
    {
        CacheAnimator();
    }

    void OnEnable()
    {
        CacheAnimator();
        ResolveAllStates(force: true);

        if (Application.isPlaying)
        {
            if (_forceIdleOnStart && !_dead)
                PlayIdle(0f);
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

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
    }

    void Start()
    {
        if (!Application.isPlaying || _dead)
            return;

        CacheAnimator();
        ResolveAllStates(force: true);
        if (_forceIdleOnStart)
            PlayIdle(0f);
    }

    void Update()
    {
        if (!Application.isPlaying || _animator == null || _dead)
            return;

        if (!_animator.isInitialized)
            return;

        // Walk loop на всё время движения
        if (_walking && _forceWalkLoop)
        {
            MaintainLoopingState(_walkHash, _resolvedWalk, ForcePlayWalk);
            return;
        }

        // Idle loop, пока не Walk/Attack/Death
        if (!_wantIdle || _busyCount > 0 || !_forceIdleLoop)
            return;

        if (string.IsNullOrEmpty(_resolvedIdle) || _idleHash == 0)
            return;

        MaintainLoopingState(_idleHash, _resolvedIdle, ForcePlayIdle);
    }

    // Удерживает state в loop: re-enter если ушли / клип без Loop Time закончился
    void MaintainLoopingState(int stateHash, string stateName, System.Action<float> forcePlay)
    {
        if (stateHash == 0 || string.IsNullOrEmpty(stateName))
            return;

        var info = _animator.GetCurrentAnimatorStateInfo(0);
        bool inState = IsCurrentState(info, stateHash, stateName);

        if (!inState && !_animator.IsInTransition(0))
        {
            forcePlay(0f);
            return;
        }

        if (!inState)
            return;

        float cycle = info.normalizedTime;
        float frac = cycle - Mathf.Floor(cycle);

        if (!info.loop)
        {
            if (cycle >= 0.95f)
                forcePlay(0f);
        }
        else if (frac >= 0.99f && cycle >= 1f)
        {
            forcePlay(0f);
        }
    }

#if UNITY_EDITOR
    double _nextEditSampleTime;

    void EditorTick()
    {
        if (this == null || Application.isPlaying || !_sampleInEditMode)
        {
            UnityEditor.EditorApplication.update -= EditorTick;
            return;
        }

        if (UnityEditor.EditorApplication.timeSinceStartup < _nextEditSampleTime)
            return;

        _nextEditSampleTime = UnityEditor.EditorApplication.timeSinceStartup + 0.5;
        SampleEditModePose();
    }

    void OnValidate()
    {
        _statesResolved = false;
        _loggedMissingIdle = _loggedMissingWalk = _loggedMissingAttack = _loggedMissingDeath = false;
        if (!isActiveAndEnabled)
            return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            CacheAnimator();
            ResolveAllStates(force: true);
            if (!Application.isPlaying && _sampleInEditMode)
                SampleEditModePose();
        };
    }
#endif

    public bool CacheAnimator()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>(true);

        if (_animator == null)
            return false;

        if (_animator.applyRootMotion)
            _animator.applyRootMotion = false;

        _statesResolved = false;
        return _animator.runtimeAnimatorController != null;
    }

    public void PlayIdle(float normalizedTime = 0f)
    {
        if (_dead)
            return;
        if (!EnsureAnimatorAndStates())
            return;

        if (string.IsNullOrEmpty(_resolvedIdle) || _idleHash == 0)
        {
            LogMissingOnce(ref _loggedMissingIdle, "Idle", _idleStateName);
            return;
        }

        _walking = false;
        _wantIdle = true;
        // Idle всегда через Play (не CrossFade) — иначе loop/restart часто ломается
        ForcePlayIdle(normalizedTime);
    }

    // Жёсткий Play без CrossFade (для loop restart)
    void ForcePlayIdle(float normalizedTime)
    {
        ForcePlayState(_idleHash, normalizedTime);
    }

    void ForcePlayWalk(float normalizedTime)
    {
        ForcePlayState(_walkHash, normalizedTime);
    }

    void ForcePlayState(int stateHash, float normalizedTime)
    {
        if (_animator == null || stateHash == 0)
            return;
        if (_animator.isInitialized && !_animator.HasState(0, stateHash))
            return;

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _animator.speed = 1f;
        _animator.Play(stateHash, 0, Mathf.Clamp01(normalizedTime));
        _animator.Update(0f);
    }

    public void StartWalk()
    {
        if (_dead)
            return;
        if (!EnsureAnimatorAndStates())
            return;

        if (string.IsNullOrEmpty(_resolvedWalk) || _walkHash == 0)
        {
            LogMissingOnce(ref _loggedMissingWalk, "Walk", _walkStateName);
            return;
        }

        _walking = true;
        _wantIdle = false;
        // Walk через Play (не CrossFade) — loop restart в Update
        ForcePlayWalk(0f);
    }

    public void StopWalkToIdle()
    {
        if (_dead)
            return;
        _walking = false;
        PlayIdle(0f);
    }

    // Поворот к направлению хода: дельта от «лица» модели, без абсолютного LookRotation.
    // Чёрные уже смотрят в центр: лицо ≈ −transform.forward (или root 180).
    // Ход к центру → угол ≈ 0 (без разворота на 180°). Вбок/назад — обычный поворот.
    public IEnumerator FaceWorldDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < DirEpsilonSqr)
            yield break;
        worldDirection.Normalize();

        Vector3 faceAxis = GetVisualFaceAxis();
        faceAxis.y = 0f;
        if (faceAxis.sqrMagnitude < DirEpsilonSqr)
            faceAxis = Vector3.forward;
        else
            faceAxis.Normalize();

        // Уже смотрим вдоль dir — не крутим (в т.ч. чёрные «вперёд» к белым)
        if (Vector3.Dot(faceAxis, worldDirection) > 0.985f)
            yield break;

        float signedYaw = Vector3.SignedAngle(faceAxis, worldDirection, Vector3.up);
        if (Mathf.Abs(signedYaw) < 2f)
            yield break;

        // Не разворачиваем чёрных «задом наперёд» на ~180°, если это ложный flip
        // (лицо уже к центру, а transform.forward смотрит назад)
        if (IsBlackFacingCenter() && Mathf.Abs(Mathf.Abs(signedYaw) - 180f) < 15f)
        {
            // Если визуально уже близко к dir — пропуск
            if (Vector3.Dot(faceAxis, worldDirection) > -0.2f)
                yield break;
        }

        float targetYaw = transform.eulerAngles.y + signedYaw;
        var targetRot = Quaternion.Euler(0f, targetYaw, 0f);
        yield return RotateTo(targetRot);
    }

    public IEnumerator FacePoint(Vector3 worldPoint)
    {
        var dir = worldPoint - transform.position;
        dir.y = 0f;
        yield return FaceWorldDirection(dir);
    }

    // Ось «куда смотрит модель» в мире (XZ).
    Vector3 GetVisualFaceAxis()
    {
        // Чёрные: часто mesh/сцена смотрит в −Z при root.forward = +Z → инвертируем.
        // Если root уже Y≈180, transform.forward уже к центру — инверсия не нужна.
        if (IsBlackFacingCenter())
        {
            float yaw = transform.eulerAngles.y;
            bool rootAbout180 = Mathf.Abs(Mathf.DeltaAngle(yaw, 180f)) < 45f;
            if (rootAbout180)
                return transform.forward;
            return -transform.forward;
        }

        return transform.forward;
    }

    bool IsBlackFacingCenter()
    {
        if (!_blackFacesBoardCenter)
            return false;
        var u = GetComponent<Unit>();
        return u != null && u.Team == Team.Black;
    }

    // onHitFrame вызывается один раз через AttackHitTime секунд после старта Attack (смерть жертвы).
    public IEnumerator PlayAttackAndWait(Action onHitFrame = null)
    {
        if (_dead)
            yield break;

        BeginBusy();
        try
        {
            if (!EnsureAnimatorAndStates())
                yield break;

            if (string.IsNullOrEmpty(_resolvedAttack) || _attackHash == 0)
            {
                LogMissingOnce(ref _loggedMissingAttack, "Attack", _attackStateName);
                onHitFrame?.Invoke();
                yield break;
            }

            _walking = false;
            _wantIdle = false;
            if (!TryPlayState(_resolvedAttack, _attackHash, 0f, "Attack"))
            {
                onHitFrame?.Invoke();
                yield break;
            }

            float hitTime = AttackHitTime;
            bool hitFired = false;
            float elapsed = 0f;
            float normalizeEnd = _stateNormalizeWait > 0f ? _stateNormalizeWait : 0.95f;
            float safety = 12f;

            yield return null;

            while (safety > 0f)
            {
                if (_animator == null)
                    break;

                elapsed += Time.deltaTime;
                safety -= Time.deltaTime;

                if (!hitFired && elapsed >= hitTime)
                {
                    hitFired = true;
                    onHitFrame?.Invoke();
                }

                if (_animator.isInitialized)
                {
                    var info = _animator.GetCurrentAnimatorStateInfo(0);
                    bool inAttack = IsCurrentState(info, _attackHash, _resolvedAttack)
                                    || _animator.IsInTransition(0);
                    if (inAttack && info.normalizedTime >= normalizeEnd && !_animator.IsInTransition(0))
                        break;
                    // если уже ушли из attack в idle — тоже выход
                    if (!inAttack && elapsed > hitTime + 0.05f)
                        break;
                }

                yield return null;
            }

            if (!hitFired)
                onHitFrame?.Invoke();

            if (!_dead)
                PlayIdle(0f);
        }
        finally
        {
            EndBusy();
        }
    }

    public IEnumerator PlayDeathSinkAndHide()
    {
        if (_dead)
            yield break;

        _dead = true;
        _walking = false;
        _wantIdle = false;
        BeginBusy();

        try
        {
            DisableInteraction();

            if (EnsureAnimatorAndStates()
                && !string.IsNullOrEmpty(_resolvedDeath)
                && _deathHash != 0
                && TryPlayState(_resolvedDeath, _deathHash, 0f, "Death"))
            {
                yield return WaitCurrentState(_stateNormalizeWait);
            }
            else
            {
                LogMissingOnce(ref _loggedMissingDeath, "Death", _deathStateName);
            }

            yield return SinkBelowBoard();

            if (_destroyAfterDeath)
            {
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        finally
        {
            EndBusy();
        }
    }

    public IEnumerator WaitCurrentState(float normalizedEnd = 0.95f)
    {
        if (_animator == null)
            yield break;

        normalizedEnd = Mathf.Clamp01(normalizedEnd);
        yield return null;

        float safety = 12f;
        while (safety > 0f)
        {
            if (_animator == null)
                yield break;

            if (!_animator.isInitialized)
            {
                safety -= Time.deltaTime;
                yield return null;
                continue;
            }

            var info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.normalizedTime >= normalizedEnd && !_animator.IsInTransition(0))
                yield break;

            safety -= Time.deltaTime;
            yield return null;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("DEBUG/Play Idle")]
    void DebugPlayIdle() => PlayIdle(0f);

    [ContextMenu("DEBUG/Play Walk 1s → Idle")]
    void DebugPlayWalk()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[UnitAnimation] {FigureLabel}: Walk debug только в Play Mode");
            return;
        }
        StartCoroutine(DebugWalkCo());
    }

    IEnumerator DebugWalkCo()
    {
        StartWalk();
        yield return new WaitForSeconds(1f);
        StopWalkToIdle();
    }

    [ContextMenu("DEBUG/Play Attack → Idle")]
    void DebugPlayAttack()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[UnitAnimation] {FigureLabel}: Attack debug только в Play Mode");
            return;
        }
        StartCoroutine(PlayAttackAndWait());
    }

    [ContextMenu("DEBUG/Play Death → sink → hide")]
    void DebugPlayDeath()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[UnitAnimation] {FigureLabel}: Death debug только в Play Mode");
            return;
        }
        StartCoroutine(PlayDeathSinkAndHide());
    }
#endif

    void SampleEditModePose()
    {
        if (Application.isPlaying || !_sampleInEditMode)
            return;
        if (!EnsureAnimatorAndStates())
            return;
        if (string.IsNullOrEmpty(_resolvedIdle) || _idleHash == 0)
            return;

        // В Edit Mode Play без существующего state → «State could not be found»
        if (!CanPlayState(_resolvedIdle, _idleHash))
        {
            LogMissingOnce(
                ref _loggedMissingIdle,
                "Idle",
                string.IsNullOrEmpty(_idleStateName) ? _resolvedIdle : _idleStateName);
            return;
        }

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        try
        {
            _animator.Play(_idleHash, 0, 0f);
            _animator.Update(0f);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(
                $"[UnitAnimation] {FigureLabel}: не удалось sample Idle '{_resolvedIdle}': {e.Message}");
        }
    }

    bool EnsureAnimatorAndStates()
    {
        if (!CacheAnimator())
            return false;
        ResolveAllStates(force: false);
        return true;
    }

    void ResolveAllStates(bool force)
    {
        if (_statesResolved && !force)
            return;

        _resolvedIdle = ResolveOne(_idleStateName, IdleCandidates, "Idle", "Idle");
        _resolvedWalk = ResolveOne(_walkStateName, WalkCandidates, "Walk", "Walk");
        _resolvedAttack = ResolveOne(_attackStateName, AttackCandidates, "Attack", "Attack");
        _resolvedDeath = ResolveOne(_deathStateName, DeathCandidates, "Death", "Dead");

        _idleHash = HashOrZero(_resolvedIdle);
        _walkHash = HashOrZero(_resolvedWalk);
        _attackHash = HashOrZero(_resolvedAttack);
        _deathHash = HashOrZero(_resolvedDeath);
        _statesResolved = true;
    }

    string ResolveOne(string explicitName, string[] candidates, string label, string preferNameContains)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return null;

        // Явное имя из Inspector — пробуем как есть, затем без префикса "Rig|Name" / "Armature|Name"
        if (!string.IsNullOrEmpty(explicitName))
        {
            var tried = ExpandNameVariants(explicitName.Trim());
            foreach (var name in tried)
            {
                if (ControllerHasClipOrState(name))
                    return name;
            }

            // В Edit Mode HasState часто false — если variants есть, берём short name
            if (!Application.isPlaying)
            {
                // предпочтительно short (после |) — так в Sylvana/Headhunter
                return tried[tried.Count - 1];
            }

            Debug.LogWarning(
                $"[UnitAnimation] {FigureLabel}: заданный {label} '{explicitName}' не найден в controller " +
                $"(пробовали: {string.Join(", ", tried)}) — auto");
        }

        foreach (var c in candidates)
        {
            if (ControllerHasClipOrState(c))
                return c;
        }

        var clips = _animator.runtimeAnimatorController.animationClips;
        if (clips != null && !string.IsNullOrEmpty(preferNameContains))
        {
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (clip.name.IndexOf(preferNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // clip может быть Armature|Idle_6 — state часто Idle_6
                    var variants = ExpandNameVariants(clip.name);
                    foreach (var v in variants)
                    {
                        if (ControllerHasClipOrState(v))
                            return v;
                    }
                    return variants[variants.Count - 1];
                }
            }
        }

        return null;
    }

    // "Armature|Idle_6" → ["Armature|Idle_6", "Idle_6"]
    static System.Collections.Generic.List<string> ExpandNameVariants(string name)
    {
        var list = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(name))
            return list;

        list.Add(name);
        int pipe = name.LastIndexOf('|');
        if (pipe >= 0 && pipe < name.Length - 1)
        {
            var shortName = name.Substring(pipe + 1).Trim();
            if (!string.IsNullOrEmpty(shortName) && !list.Contains(shortName))
                list.Add(shortName);
        }

        return list;
    }

    bool ControllerHasClipOrState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || _animator == null)
            return false;

        int hash = Animator.StringToHash(stateName);
        if (_animator.isInitialized && _animator.HasState(0, hash))
            return true;

        // В Edit Mode isInitialized часто false — проверяем клипы
        var clips = _animator.runtimeAnimatorController != null
            ? _animator.runtimeAnimatorController.animationClips
            : null;
        if (clips == null)
            return false;

        foreach (var clip in clips)
        {
            if (clip != null && clip.name == stateName)
                return true;
        }

        return false;
    }

    bool CanPlayState(string stateName, int hash)
    {
        if (_animator == null || hash == 0 || string.IsNullOrEmpty(stateName))
            return false;

        // Надёжная проверка только когда Animator инициализирован
        if (_animator.isInitialized)
            return _animator.HasState(0, hash);

        // Edit Mode: Play только если state/clip точно есть; иначе Skip (без GotoState spam)
        return ControllerHasClipOrState(stateName);
    }

    bool TryPlayState(string stateName, int hash, float normalizedTime, string label)
    {
        if (_animator == null || hash == 0 || string.IsNullOrEmpty(stateName))
            return false;

        if (_animator.isInitialized && !_animator.HasState(0, hash))
        {
            Debug.LogWarning(
                $"[UnitAnimation] {FigureLabel}: state '{stateName}' ({label}) не найден в Animator Controller. " +
                "Имя должно совпадать со STATE (не только с клипом).");
            return false;
        }

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        normalizedTime = Mathf.Clamp01(normalizedTime);

        try
        {
            if (_crossFade > 0.001f && Application.isPlaying && _animator.isInitialized)
                _animator.CrossFade(hash, _crossFade, 0, normalizedTime);
            else
                _animator.Play(hash, 0, normalizedTime);

            if (!Application.isPlaying || _crossFade <= 0.001f)
                _animator.Update(0f);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(
                $"[UnitAnimation] {FigureLabel}: Play '{stateName}' ({label}) failed: {e.Message}");
            return false;
        }
    }

    static int HashOrZero(string stateName)
    {
        return string.IsNullOrEmpty(stateName) ? 0 : Animator.StringToHash(stateName);
    }

    static bool IsCurrentState(AnimatorStateInfo info, int hash, string name)
    {
        if (hash != 0 && info.shortNameHash == hash)
            return true;
        if (!string.IsNullOrEmpty(name) && info.IsName(name))
            return true;
        return false;
    }

    void LogMissingOnce(ref bool flag, string label, string triedName)
    {
        if (flag) return;
        flag = true;
        var shown = string.IsNullOrEmpty(triedName) ? "(auto)" : triedName;
        Debug.LogWarning(
            $"[UnitAnimation] {FigureLabel}: {label} state не найден ('{shown}'). " +
            "Проверь имя STATE в Animator и поле на UnitAnimationDriver.");
    }

    IEnumerator RotateTo(Quaternion targetRot)
    {
        // Только yaw
        targetRot = Quaternion.Euler(0f, targetRot.eulerAngles.y, 0f);

        if (_turnSpeed <= 0f)
        {
            transform.rotation = targetRot;
            yield break;
        }

        // Кратчайший путь — без полного оборота
        while (Quaternion.Angle(transform.rotation, targetRot) > 0.5f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                _turnSpeed * Time.deltaTime);
            yield return null;
        }

        transform.rotation = targetRot;
    }

    IEnumerator SinkBelowBoard()
    {
        var start = transform.position;
        var end = start + Vector3.down * Mathf.Max(0.01f, _sinkDepth);
        float dur = Mathf.Max(0.01f, _sinkDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            u = u * u;
            transform.position = Vector3.Lerp(start, end, u);
            yield return null;
        }

        transform.position = end;
    }

    void DisableInteraction()
    {
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null)
                col.enabled = false;
        }
    }

    void BeginBusy() => _busyCount++;

    void EndBusy()
    {
        _busyCount = Mathf.Max(0, _busyCount - 1);
    }
}
