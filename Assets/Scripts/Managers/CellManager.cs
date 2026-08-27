using System;
using UnityEngine;

/// <summary>
/// Прокси кликов по доске (логирование). Игровой ввод идёт через BattleController.
/// Событие: OnCellClicked.
/// </summary>
public class CellManager : MonoBehaviour
{
    [SerializeField] private Battlefield _battlefield;

    public event Action<Cell> OnCellClicked;

    public Battlefield Board => _battlefield;

    private void Awake()
    {
        if (_battlefield == null)
            _battlefield = FindObjectOfType<Battlefield>();
    }

    private void OnEnable()
    {
        if (_battlefield == null)
            _battlefield = FindObjectOfType<Battlefield>();

        if (_battlefield != null)
            _battlefield.OnCellClicked += HandleBoardCellClicked;
    }

    private void OnDisable()
    {
        if (_battlefield != null)
            _battlefield.OnCellClicked -= HandleBoardCellClicked;
    }

    private void Start()
    {
        if (_battlefield == null)
        {
            Debug.LogError("[CellManager] Battlefield не найден на сцене.");
            return;
        }

        _battlefield.EnsureInitialized();
    }

    private void HandleBoardCellClicked(Cell cell)
    {
        OnCellClicked?.Invoke(cell);
        if (cell != null)
            Debug.Log($"[CellManager] Клик: {cell.X},{cell.Y}" +
                      (cell.Unit != null ? $" unit={cell.Unit.Team}/{cell.Unit.Type}" : string.Empty));
    }
}
