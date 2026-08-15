using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Фигура: команда, тип, клетка, pointer → Cell (Select).
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
    public bool IsMoving => _isMoving;
    public bool HasMoved { get; set; }

    public event Action OnMoveEndCallback;

    private bool _isMoving;

    public void SetTeam(Team team) => _team = team;

    public void SetType(ChessPieceType type) => _type = type;

    /// <summary>
    /// Привязка к клетке (Battlefield / ход). Старую клетку очищает.
    /// </summary>
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

    /// <summary>
    /// Превращение пешки: меняет Type и подменяет child-модель/Animator
    /// по префабу своей команды (PieceVisualLibrary → ChessSetup).
    /// Root Unit, Cell, Collider, position сохраняются.
    /// </summary>
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
        // 1) PieceVisualLibrary (если есть на сцене)
        var lib = PieceVisualLibrary.Instance != null
            ? PieceVisualLibrary.Instance
            : FindObjectOfType<PieceVisualLibrary>();

        if (lib != null)
        {
            lib.ApplyVisual(this, newType);
            return;
        }

        // 2) Fallback: префабы из ChessSetup
        var setup = FindObjectOfType<ChessSetup>();
        if (setup != null)
        {
            var prefab = setup.GetPrefab(_team, newType);
            if (prefab != null)
            {
                PieceVisualLibrary.ReplaceVisualFromPrefab(this, prefab.gameObject);
                return;
            }
        }

        Debug.LogWarning(
            $"[Unit] Нет префаба для визуала {_team}/{newType}. " +
            "Назначь префабы в ChessSetup или PieceVisualLibrary.");
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

    /// <summary>
    /// Полный ход: логика клетки + анимация (если вызываешь без PlayerController).
    /// </summary>
    public void Move(Cell targetCell)
    {
        if (targetCell == null || _isMoving) return;
        StartCoroutine(MoveRoutine(targetCell));
    }

    /// <summary>
    /// Только визуальное перемещение. Логику BindToCell делает PlayerController.
    /// public — чтобы можно было: yield return unit.AnimateMoveTo(pos);
    /// </summary>
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
