using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class ChessCommand : IGameplayCommand
{
    private readonly Battlefield _board;
    private readonly PlayerController _player;
    private readonly ITurnInfoView _turnView;
    private readonly EnPassantState _enPassant;

    private Team _currentTeam = Team.White;
    private Unit _selected;
    private readonly List<Cell> _targets = new();

    public Team CurrentTeam => _currentTeam;

    public ChessCommand(
        Battlefield board,
        PlayerController player,
        [InjectOptional] ITurnInfoView turnView,
        [InjectOptional] EnPassantState enPassant)
    {
        _board = board;
        _player = player;
        _turnView = turnView;
        _enPassant = enPassant;
    }

    public void SetFirstTeam(Team team)
    {
        _currentTeam = team;
        _turnView?.ShowTurn(_currentTeam);
    }

    public void Interact(Cell cell)
    {
        if (_player != null && _player.IsBusy)
            return;

        if (cell == null)
            return;

        if (_board != null)
            _board.EnsureInitialized();

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

        // --- Клик по допустимой клетке: ход / рокировка / en passant ---
        if (_targets.Contains(cell))
        {
            var moving = _selected;
            var to = cell;

            ClearSelection();

            if (ChessMoveGenerator.TryGetCastleMove(moving, to, _board, out var castle))
            {
                _player.ExecuteCastle(
                    castle.Mover,
                    castle.To,
                    castle.Rook,
                    castle.RookTo,
                    OnMoveResolved);
            }
            else if (ChessMoveGenerator.TryGetEnPassantMove(
                         moving, to, _board, _enPassant, out var ep))
            {
                _player.ExecuteEnPassant(
                    ep.Mover,
                    ep.To,
                    ep.CapturedUnit,
                    OnMoveResolved);
            }
            else
            {
                _player.ExecuteMove(moving, to, OnMoveResolved);
            }

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

        if (unit?.Cell == null)
            return;

        _selected = unit;
        _targets.Clear();
        _targets.AddRange(ChessMoveGenerator.GetTargets(unit, _board, _enPassant));

        if (_board != null)
        {
            _board.HighlightCell(unit.Cell, CellHighlight.Selected);
            foreach (var t in _targets)
            {
                var mode = ChessMoveGenerator.IsAttackTarget(unit, t, _enPassant)
                    ? CellHighlight.Attack
                    : CellHighlight.Move;
                _board.HighlightCell(t, mode);
            }
        }
        else
        {
            unit.Cell.SetHighlight(CellHighlight.Selected);
            foreach (var t in _targets)
            {
                bool isAttack = t.Unit != null
                    || ChessMoveGenerator.IsEnPassantLanding(unit, t, _enPassant);
                t.SetHighlight(isAttack ? CellHighlight.Attack : CellHighlight.Move);
            }
        }
    }

    private void ClearSelection()
    {
        _board?.ClearAllHighlights();
        _selected = null;
        _targets.Clear();
    }

    private void OnMoveResolved()
    {
        _currentTeam = _currentTeam == Team.White ? Team.Black : Team.White;
        _turnView?.ShowTurn(_currentTeam);
        Debug.Log($"[Chess] Now playing: {_currentTeam}");
    }
}
