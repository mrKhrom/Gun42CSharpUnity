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
    [Header("Optional combat")]
    [SerializeField] private ArcheryWeapon _archeryWeapon;
    [SerializeField] private bool _testArcheryOnClick = false;

    public Cell Cell { get; set; }
    public Team Team => _team;

    public event Action OnMoveEndCallback;

    private bool _isMoving;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Cell?.OnPointerEnter(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Cell?.OnPointerExit(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Cell?.OnPointerClick(eventData);
        if (_testArcheryOnClick)
            PlayArcheryAttack();
    }

    /// <summary>Plays Archery_Shot_1 if ArcheryWeapon is present.</summary>
    public void PlayArcheryAttack()
    {
        if (_archeryWeapon == null)
            _archeryWeapon = GetComponent<ArcheryWeapon>();
        _archeryWeapon?.PlayArcheryAttack();
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
}