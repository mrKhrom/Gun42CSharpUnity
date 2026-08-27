using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Фигура на доске: сторона, тип, клетка, превращение, клик/наведение.
/// Методы: SetTeam — задать сторону; SetType — задать тип; BindToCell — привязать к клетке;
/// Setup — полная инициализация; PromoteTo — превратить пешку; OnPointerEnter / Exit / Click — наведение и клик.
/// </summary>
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
    public bool HasMoved { get; set; }

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

    public Unit PromoteTo(ChessPieceType newType)
    {
        if (newType == ChessPieceType.Pawn || newType == ChessPieceType.King)
        {
            Debug.LogWarning($"[Unit] PromoteTo rejected: {newType}");
            return this;
        }

        var setup = FindChessSetup();
        GameObject sourceGo = setup != null ? setup.GetSpawnSource(_team, newType) : null;
        if (sourceGo == null)
        {
            Debug.LogWarning(
                $"[Unit] Нет префаба {_team}/{newType} — визуал пешки не сменён");
            _type = newType;
            name = $"{_team}_{_type}";
            return this;
        }

        var cell = Cell;
        var team = _team;
        var parent = transform.parent;
        var worldPos = transform.position;
        var worldRot = transform.rotation;
        var board = FindObjectOfType<Battlefield>();

        var neuGo = Instantiate(sourceGo, parent);
        neuGo.SetActive(true);
        neuGo.transform.SetPositionAndRotation(worldPos, worldRot);
        neuGo.name = $"{team}_{newType}";

        var neu = neuGo.GetComponent<Unit>() ?? neuGo.GetComponentInChildren<Unit>(true);
        if (neu == null)
        {
            Debug.LogError($"[Unit] В префабе {sourceGo.name} нет Unit");
            if (Application.isPlaying)
                Destroy(neuGo);
            else
                DestroyImmediate(neuGo);
            _type = newType;
            name = $"{_team}_{_type}";
            return this;
        }

        if (neu.transform != neuGo.transform)
            neu.transform.SetPositionAndRotation(worldPos, worldRot);

        // Пешка ещё жива: сначала сажаем новую фигуру на клетку, потом удаляем пешку
        // в том же кадре (DestroyImmediate), чтобы RelinkUnits не вернул пешку.
        BindToCell(null, snap: false);
        board?.UnregisterUnit(this);

        neu.Setup(team, newType, cell, snap: false);
        neu.transform.SetPositionAndRotation(worldPos, worldRot);
        neu.HasMoved = true;
        board?.RegisterUnit(neu);

        var anim = neu.GetComponent<UnitAnimationDriver>()
                   ?? neu.GetComponentInChildren<UnitAnimationDriver>(true);
        if (anim != null)
        {
            anim.CacheAnimator();
            if (!anim.IsDead)
                anim.PlayIdle(0f);
        }

        Debug.Log($"[Unit] Promote visual: {name} → {neu.name} (from {sourceGo.name})");

        DestroyImmediate(gameObject);
        return neu;
    }

    static ChessSetup FindChessSetup()
    {
        var setup = FindFirstObjectByType<ChessSetup>(FindObjectsInactive.Include);
        if (setup != null)
            return setup;

        foreach (var s in Resources.FindObjectsOfTypeAll<ChessSetup>())
        {
            if (s != null && s.gameObject.scene.IsValid())
                return s;
        }

        return null;
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
