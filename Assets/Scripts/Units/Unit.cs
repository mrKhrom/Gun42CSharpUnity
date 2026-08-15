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

    public Cell Cell { get; set; }
    public Team Team => _team;
    public ChessPieceType Type => _type;

    public void SetType(ChessPieceType type) => _type = type;

    public event Action OnMoveEndCallback;

    private bool _isMoving;

    /// <summary>
    /// Hover on figure collider → highlight the cell under this unit (Focus).
    /// </summary>
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

    private IEnumerator MoveRoutine(Cell targetCell)
    {
        _isMoving = true;

        if (Cell != null)
            Cell.Unit = null;

        Vector3 targetPos = targetCell.transform.position;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                _moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPos;
        Cell = targetCell;
        targetCell.Unit = this;

        _isMoving = false;
        OnMoveEndCallback?.Invoke();
    }

    /// <summary>
    /// Fallback if CellManager has not linked yet (or link failed).
    /// </summary>
    private void EnsureCellLinked()
    {
        if (Cell != null) return;

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
        {
            Cell = closest;
            closest.Unit = this;
        }
    }
}
