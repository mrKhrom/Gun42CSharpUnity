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
    private bool _gameOver;

    public Team CurrentTeam => _currentTeam;
    public bool IsGameOver => _gameOver;

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
        _gameOver = false;
        _currentTeam = team;
        _turnView?.ShowTurn(_currentTeam);
        EvaluatePosition(showTurnIfQuiet: true);
    }

    public void Interact(Cell cell)
    {
        if (_gameOver)
            return;

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

        // --- Клик по допустимой (легальной) клетке ---
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
        if (_gameOver)
            return;
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

        var legal = ChessLegality.GetLegalTargets(unit, _board, _enPassant);
        if (legal.Count == 0)
        {
            // Фигура без легальных ходов (булавка / шах) — не выбираем
            Debug.Log($"[Chess] {unit.Team}/{unit.Type}: 0 legal moves");
            return;
        }

        _selected = unit;
        _targets.Clear();
        _targets.AddRange(legal);

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

            // Король под шахом — подсветить его клетку
            if (ChessMoveGenerator.IsKingInCheck(_board, _currentTeam))
            {
                var king = ChessMoveGenerator.FindKing(_board, _currentTeam);
                if (king?.Cell != null && king.Cell != unit.Cell)
                    _board.HighlightCell(king.Cell, CellHighlight.Attack);
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
        Debug.Log($"[Chess] Now playing: {_currentTeam}");
        EvaluatePosition(showTurnIfQuiet: true);
    }

    /// <summary>
    /// После смены стороны (или старта): шах / мат / пат.
    /// </summary>
    public void EvaluatePosition(bool showTurnIfQuiet)
    {
        if (_board == null)
            return;

        _board.EnsureInitialized();

        bool inCheck = ChessMoveGenerator.IsKingInCheck(_board, _currentTeam);
        bool hasMove = ChessLegality.SideHasLegalMove(_board, _currentTeam, _enPassant);

        if (inCheck && !hasMove)
        {
            _gameOver = true;
            ClearSelection();
            var winner = ChessMoveGenerator.Opposite(_currentTeam);
            _turnView?.ShowCheckmate(winner);
            Debug.Log($"[Chess] CHECKMATE — {winner} wins. {_currentTeam} is mated.");
            return;
        }

        if (!inCheck && !hasMove)
        {
            _gameOver = true;
            ClearSelection();
            _turnView?.ShowStalemate();
            Debug.Log("[Chess] STALEMATE — draw.");
            return;
        }

        if (inCheck)
        {
            _turnView?.ShowCheck(_currentTeam);
            Debug.Log($"[Chess] CHECK — {_currentTeam} is in check.");

            var king = ChessMoveGenerator.FindKing(_board, _currentTeam);
            if (king?.Cell != null)
                _board.HighlightCell(king.Cell, CellHighlight.Attack);
            return;
        }

        if (showTurnIfQuiet)
            _turnView?.ShowTurn(_currentTeam);
    }
}
