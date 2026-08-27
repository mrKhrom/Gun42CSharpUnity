using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TurnCameraController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _cameraTransform;

    [Header("Anchors")]
    [SerializeField] private Transform _whiteAnchor;
    [SerializeField] private Transform _blackAnchor;
    [SerializeField] private bool _invertBlack;

    [Header("Board center (для auto-якорей)")]
    [SerializeField] private Vector3 _boardCenter = new Vector3(3.5f, 0f, 3.5f);

    [Header("Motion")]
    [SerializeField] private bool _animate = true;
    [SerializeField] private float _moveDuration = 0.45f;
    [SerializeField] private AnimationCurve _ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool IsMoving { get; private set; }

    Team _currentView = Team.White;
    Coroutine _moveCo;

    void Awake()
    {
        ResolveCamera();
        EnsureAnchors();
    }

    public void BindGameplayCamera(Camera cam)
    {
        if (cam == null)
            return;

        StopMove();
        _camera = cam;
        _cameraTransform = cam.transform;
        RebuildAutoAnchors();
    }

    void ResolveCamera()
    {
        if (IsUsable(_camera))
        {
            if (_cameraTransform == null)
                _cameraTransform = _camera.transform;
            return;
        }

        _camera = FindCameraInSameScene();
        if (!IsUsable(_camera))
        {
            foreach (var cam in FindObjectsOfType<Camera>())
            {
                if (!IsUsable(cam))
                    continue;
                if (cam.gameObject.scene == gameObject.scene)
                {
                    _camera = cam;
                    break;
                }
            }
        }

        _cameraTransform = IsUsable(_camera) ? _camera.transform : null;
    }

    static bool IsUsable(Camera cam)
    {
        return cam != null && cam.enabled && cam.gameObject.activeInHierarchy;
    }

    Camera FindCameraInSameScene()
    {
        var scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        var roots = scene.GetRootGameObjects();
        Camera tagged = null;
        foreach (var root in roots)
        {
            var cams = root.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cams)
            {
                if (!IsUsable(cam))
                    continue;
                if (cam.CompareTag("MainCamera"))
                    return cam;
                if (tagged == null)
                    tagged = cam;
            }
        }

        return tagged;
    }

    void RebuildAutoAnchors()
    {
        DestroyAutoAnchor(ref _whiteAnchor, "CamAnchor_White");
        DestroyAutoAnchor(ref _blackAnchor, "CamAnchor_Black");
        EnsureAnchors();
    }

    static void DestroyAutoAnchor(ref Transform anchor, string name)
    {
        if (anchor == null)
            return;
        if (anchor.name != name)
            return;
        var go = anchor.gameObject;
        anchor = null;
        if (go != null)
            Destroy(go);
    }

    // Если якоря не назначены — создаём из текущей позы камеры (White) и зеркала (Black)
    void EnsureAnchors()
    {
        if (_cameraTransform == null)
            return;

        var root = transform;
        if (_whiteAnchor == null)
        {
            var go = new GameObject("CamAnchor_White");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(_cameraTransform.position, _cameraTransform.rotation);
            _whiteAnchor = go.transform;
        }

        if (_blackAnchor == null)
        {
            var go = new GameObject("CamAnchor_Black");
            go.transform.SetParent(root, false);
            // 180° вокруг оси Y через центр доски
            Vector3 offset = _whiteAnchor.position - _boardCenter;
            Vector3 blackPos = _boardCenter + new Vector3(-offset.x, offset.y, -offset.z);
            var whiteEuler = _whiteAnchor.rotation.eulerAngles;
            var blackRot = Quaternion.Euler(whiteEuler.x, whiteEuler.y + 180f, whiteEuler.z);
            go.transform.SetPositionAndRotation(blackPos, blackRot);
            _blackAnchor = go.transform;
        }
    }

    public void SnapToTeam(Team team)
    {
        StopMove();
        var anchor = GetAnchor(team);
        if (anchor == null || _cameraTransform == null)
            return;

        _cameraTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
        _currentView = team;
        IsMoving = false;
    }

    public void AnimateToTeam(Team team)
    {
        if (!_animate || _moveDuration <= 0.001f)
        {
            SnapToTeam(team);
            return;
        }

        if (_cameraTransform == null)
            ResolveCamera();

        var anchor = GetAnchor(team);
        if (anchor == null || _cameraTransform == null)
            return;

        if (_currentView == team
            && Vector3.Distance(_cameraTransform.position, anchor.position) < 0.01f)
            return;

        StopMove();
        _moveCo = StartCoroutine(MoveRoutine(anchor, team));
    }

    // teamNowToMove — кто ходит сейчас (после смены хода)
    public void OnTurnChanged(Team teamNowToMove, bool snap = false)
    {
        if (!IsUsable(_camera) || _cameraTransform == null)
        {
            ResolveCamera();
            if (_whiteAnchor == null || _blackAnchor == null)
                EnsureAnchors();
        }

        if (snap || !_animate || _moveDuration <= 0.001f)
            SnapToTeam(teamNowToMove);
        else
            AnimateToTeam(teamNowToMove);
    }

    Transform GetAnchor(Team team)
    {
        EnsureAnchors();
        bool black = team == Team.Black;
        if (_invertBlack)
            black = !black;
        return black ? _blackAnchor : _whiteAnchor;
    }

    IEnumerator MoveRoutine(Transform target, Team team)
    {
        IsMoving = true;

        Vector3 fromPos = _cameraTransform.position;
        Quaternion fromRot = _cameraTransform.rotation;
        Vector3 toPos = target.position;
        Quaternion toRot = target.rotation;

        float dur = Mathf.Max(0.01f, _moveDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = _ease != null && _ease.keys != null && _ease.length > 0
                ? _ease.Evaluate(u)
                : u;

            _cameraTransform.position = Vector3.Lerp(fromPos, toPos, e);
            _cameraTransform.rotation = Quaternion.Slerp(fromRot, toRot, e);
            yield return null;
        }

        _cameraTransform.SetPositionAndRotation(toPos, toRot);
        _currentView = team;
        IsMoving = false;
        _moveCo = null;
    }

    void StopMove()
    {
        if (_moveCo != null)
        {
            StopCoroutine(_moveCo);
            _moveCo = null;
        }
        IsMoving = false;
    }

#if UNITY_EDITOR
    [ContextMenu("DEBUG/Snap White")]
    void DebugSnapWhite() => SnapToTeam(Team.White);

    [ContextMenu("DEBUG/Snap Black")]
    void DebugSnapBlack() => SnapToTeam(Team.Black);

    [ContextMenu("DEBUG/Animate Black")]
    void DebugAnimBlack() => AnimateToTeam(Team.Black);

    [ContextMenu("DEBUG/Animate White")]
    void DebugAnimWhite() => AnimateToTeam(Team.White);
#endif
}
