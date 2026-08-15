using System.Collections.Generic;
using UnityEngine;

public class ChessCommand : IGameplayCommand
{
    private readonly Battlefield _board;
    private readonly PlayerController _player;

    private Team _currentTeam = Team.White;
    private Unit _selected;
    private readonly List<Cell> _targets = new();

    public Team CurrentTeam => _currentTeam;

    public ChessCommand(Battlefield board, PlayerController player)
    {
        _board = board;
        _player = player;
    }

    public void SetFirstTeam(Team team) => _currentTeam = team;

    public void Interact(Cell cell)
    {
        if (_player != null && _player.IsBusy)
            return;

        if (cell == null)
            return;

        // --- Нет выбранной фигуры ---
        if (_selected == null)
        {
            TrySelectFromCell(cell);
            return;
        }

        // --- Клик по своей фигуре: перевыбор ---
        if (cell.Unit != null && cell.Unit.Team == _currentTeam)
        {
            SelectUnit(cell.Unit);
            return;
        }

        // --- Клик по допустимой клетке: ход ---
        if (_targets.Contains(cell))
        {
            var moving = _selected;
            var to = cell;

            ClearSelection();
            _player.ExecuteMove(moving, to, OnMoveResolved);
            return;
        }

        // --- Клик мимо ---
        Cancel();
    }

    public void Cancel()
    {
        ClearSelection();
    }

    public void Confirm()
    {
        // MVP: ход подтверждается вторым кликом.
        // Можно позже: Confirm применяет "предвыбранную" цель.
    }

    private void TrySelectFromCell(Cell cell)
    {
        if (cell.Unit == null) return;
        if (cell.Unit.Team != _currentTeam) return;
        SelectUnit(cell.Unit);
    }

    private void SelectUnit(Unit unit)
    {
        ClearSelection();

        _selected = unit;
        _targets.Clear();
        _targets.AddRange(ChessMoveGenerator.GetTargets(unit, _board));

        unit.Cell.SetHighlight(CellHighlight.Selected);

        foreach (var t in _targets)
        {
            bool isAttack = t.Unit != null;
            t.SetHighlight(isAttack ? CellHighlight.Attack : CellHighlight.Move);
        }
    }

    private void ClearSelection()
    {
        _board.ClearAllHighlights();
        _selected = null;
        _targets.Clear();
    }

    private void OnMoveResolved()
    {
        _currentTeam = _currentTeam == Team.White ? Team.Black : Team.White;
        Debug.Log($"[Chess] Now playing: {_currentTeam}");
    }
}