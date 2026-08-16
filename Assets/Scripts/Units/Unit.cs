using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class Unit : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private Team _team = Team.White;
    [SerializeField] private ChessPieceType _type = ChessPieceType.Pawn;

    public Cell Cell { get; private set; }
    public Team Team => _team;
    public ChessPieceType Type => _type;
    public float MoveSpeed => _moveSpeed;
    public bool IsMoving => _isMoving;
    public bool HasMoved { get; set; }

    public event Action OnMoveEndCallback;

    private bool _isMoving;

    public void SetTeam(Team team) => _team = team;

    public void SetType(ChessPieceType type) => _type = type;

    public void BindToCell(Cell cell, bool snap = false)
    {
        if (Cell != null && Cell.Unit == this)
            Cell.Unit = null;

        Cell = cell;

        if (cell != null)
        {
            cell.Unit = this;
            if (snap)
            {
                var p = cell.transform.position;
                transform.position = new Vector3(p.x, transform.position.y, p.z);
            }
        }
    }

    public void Setup(Team team, ChessPieceType type, Cell cell, bool snap = true)
    {
        _team = team;
        _type = type;
        name = $"{team}_{type}";
        BindToCell(cell, snap);
        HasMoved = false;
    }

    public void PromoteTo(ChessPieceType newType)
    {
        if (newType == ChessPieceType.Pawn || newType == ChessPieceType.King)
        {
            Debug.LogWarning($"[Unit] PromoteTo rejected: {newType}");
            return;
        }

        _type = newType;
        name = $"{_team}_{_type}";

        ApplyPromotedVisual(newType);

        Debug.Log($"[Unit] {_team} promoted → {_type}");
    }

    private void ApplyPromotedVisual(ChessPieceType newType)
    {
        // includeInactive: ChessSetup может быть выключен на Systems
        var setup = FindObjectOfType<ChessSetup>(true);
        if (setup == null)
        {
            Debug.LogWarning(
                $"[Unit] Нет ChessSetup — визуал {_team}/{newType} не сменён. " +
                "Назначь префабы в ChessSetup на Systems.");
            return;
        }

        var prefab = setup.GetPrefab(_team, newType);
        if (prefab == null)
        {
            Debug.LogWarning(
                $"[Unit] Нет префаба {_team}/{newType} в ChessSetup — тип изменён, визуал старый.");
            return;
        }

        ReplaceVisualFromPrefab(prefab.gameObject);

        // После смены mesh/Animator — заново найти Animator и Idle
        var anim = GetComponent<UnitAnimationDriver>();
        if (anim != null)
        {
            anim.CacheAnimator();
            if (!anim.IsDead)
                anim.PlayIdle(0f);
        }
    }

    private void ReplaceVisualFromPrefab(GameObject prefabRoot)
    {
        if (prefabRoot == null)
            return;

        var hostTf = transform;

        for (int i = hostTf.childCount - 1; i >= 0; i--)
            DestroyImmediate(hostTf.GetChild(i).gameObject);

        var temp = Instantiate(prefabRoot);
        temp.name = $"{prefabRoot.name}_PromoteTemp";
        temp.transform.SetPositionAndRotation(hostTf.position, hostTf.rotation);

        var srcAnim = temp.GetComponent<Animator>();
        var dstAnim = GetComponent<Animator>();
        if (srcAnim != null)
        {
            if (dstAnim == null)
                dstAnim = gameObject.AddComponent<Animator>();

            dstAnim.runtimeAnimatorController = srcAnim.runtimeAnimatorController;
            dstAnim.avatar = srcAnim.avatar;
            dstAnim.applyRootMotion = false;
            dstAnim.cullingMode = srcAnim.cullingMode;
            dstAnim.updateMode = srcAnim.updateMode;
        }

        while (temp.transform.childCount > 0)
        {
            var child = temp.transform.GetChild(0);
            child.SetParent(hostTf, false);
        }

        var rootMf = temp.GetComponent<MeshFilter>();
        if (rootMf != null)
        {
            var visualGo = new GameObject("PromotedMesh");
            visualGo.transform.SetParent(hostTf, false);
            var mf = visualGo.AddComponent<MeshFilter>();
            mf.sharedMesh = rootMf.sharedMesh;
            var mrSrc = temp.GetComponent<MeshRenderer>();
            if (mrSrc != null)
            {
                var mr = visualGo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mrSrc.sharedMaterials;
            }
        }

        if (Application.isPlaying)
            Destroy(temp);
        else
            DestroyImmediate(temp);

        Debug.Log($"[Unit] Visual → {prefabRoot.name} on {name}");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EnsureCellLinked();
        Cell?.OnPointerEnter(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Cell?.OnPointerExit(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        EnsureCellLinked();
        Cell?.OnPointerClick(eventData);
    }

    public void Move(Cell targetCell)
    {
        if (targetCell == null || _isMoving) return;
        StartCoroutine(MoveRoutine(targetCell));
    }

    public IEnumerator AnimateMoveTo(Vector3 worldTarget)
    {
        _isMoving = true;

        worldTarget.y = transform.position.y;

        while ((transform.position - worldTarget).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                worldTarget,
                _moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = worldTarget;
        _isMoving = false;
    }

    private IEnumerator MoveRoutine(Cell targetCell)
    {
        if (Cell != null && Cell.Unit == this)
            Cell.Unit = null;

        yield return AnimateMoveTo(targetCell.transform.position);

        BindToCell(targetCell, snap: false);
        HasMoved = true;
        OnMoveEndCallback?.Invoke();
    }

    private void EnsureCellLinked()
    {
        if (Cell != null) return;

        var board = FindObjectOfType<Battlefield>();
        if (board != null)
        {
            board.EnsureInitialized();
            board.RelinkUnits();
            if (Cell != null) return;
        }

        Cell closest = null;
        float best = float.MaxValue;
        foreach (var cell in FindObjectsOfType<Cell>())
        {
            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(cell.transform.position.x, cell.transform.position.z));
            if (dist < best)
            {
                best = dist;
                closest = cell;
            }
        }

        if (closest != null && best < 0.75f)
            BindToCell(closest, snap: false);
    }
}
