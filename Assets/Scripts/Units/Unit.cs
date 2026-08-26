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

    // Превращение пешки: спавн префаба выбранной фигуры (полная модель + Animator),
    // старая пешка уничтожается. Возвращает новый Unit (или this, если только смена Type).
    public Unit PromoteTo(ChessPieceType newType)
    {
        if (newType == ChessPieceType.Pawn || newType == ChessPieceType.King)
        {
            Debug.LogWarning($"[Unit] PromoteTo rejected: {newType}");
            return this;
        }

        var setup = FindObjectOfType<ChessSetup>(true);
        if (setup == null)
        {
            Debug.LogWarning("[Unit] Нет ChessSetup — только Type, визуал не сменён");
            _type = newType;
            name = $"{_team}_{_type}";
            return this;
        }

        var prefabUnit = setup.GetPrefab(_team, newType);
        if (prefabUnit == null)
        {
            Debug.LogWarning(
                $"[Unit] Нет префаба {_team}/{newType} в ChessSetup — только Type");
            _type = newType;
            name = $"{_team}_{_type}";
            return this;
        }

        // Источник: префаб-ассет предпочтительнее scene-instance (полная иерархия модели)
        GameObject sourceGo = ResolvePrefabSource(prefabUnit.gameObject);

        var cell = Cell;
        var team = _team;
        var parent = transform.parent;
        var worldPos = transform.position;
        var worldRot = transform.rotation;
        var localScale = transform.localScale;

        var board = FindObjectOfType<Battlefield>();

        // Отвязать пешку и сразу выключить: Destroy отложен до конца кадра,
        // а RelinkUnits иначе снова сажает пешку на ту же клетку и затирает ферзя.
        BindToCell(null, snap: false);
        board?.UnregisterUnit(this);
        gameObject.SetActive(false);

        var neuGo = Instantiate(sourceGo, parent);
        neuGo.SetActive(true);
        neuGo.transform.SetPositionAndRotation(worldPos, worldRot);
        neuGo.transform.localScale = localScale;
        neuGo.name = $"{team}_{newType}";

        var neu = neuGo.GetComponent<Unit>();
        if (neu == null)
        {
            Debug.LogError($"[Unit] В префабе {sourceGo.name} нет Unit");
            Destroy(neuGo);
            _type = newType;
            name = $"{_team}_{_type}";
            gameObject.SetActive(true);
            if (cell != null)
                BindToCell(cell, snap: false);
            board?.RegisterUnit(this);
            return this;
        }

        // Логика новой фигуры — BindToCell в Setup, без полного Relink
        neu.Setup(team, newType, cell, snap: false);
        neu.transform.SetPositionAndRotation(worldPos, worldRot);
        neu.HasMoved = true;
        board?.RegisterUnit(neu);

        // Анимации новой модели (имена states с префаба ферзя/ладьи/…)
        var anim = neu.GetComponent<UnitAnimationDriver>();
        if (anim != null)
        {
            anim.CacheAnimator();
            if (!anim.IsDead)
                anim.PlayIdle(0f);
        }

        Debug.Log($"[Unit] Promote visual: {name} → {neu.name} (from {sourceGo.name})");

        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);

        return neu;
    }

    static GameObject ResolvePrefabSource(GameObject go)
    {
        if (go == null)
            return null;

#if UNITY_EDITOR
        var asset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (asset != null)
            return asset;
#endif
        // Runtime: Instantiate prefab-instance копирует визуал (вложенные mesh/rig)
        return go;
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
