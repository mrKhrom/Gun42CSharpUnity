using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class ChessCommand : IGameplayCommand, ICheatCommands
{
    private readonly Battlefield _board;
    private readonly PlayerController _player;
    private readonly ITurnInfoView _turnView;
    private readonly EnPassantState _enPassant;
    private readonly TurnCameraController _turnCamera;
    private readonly CheatCommandStack _cheats;

    private Team _currentTeam = Team.White;
    private Unit _selected;
    private readonly List<Cell> _targets = new();
    private bool _gameOver;

    public Team CurrentTeam => _currentTeam;
    public bool IsGameOver => _gameOver;
    public bool IsBusy => _player != null && _player.IsBusy;

    public ChessCommand(
        Battlefield board,
        PlayerController player,
        CheatCommandStack cheats,
        [InjectOptional] ITurnInfoView turnView,
        [InjectOptional] EnPassantState enPassant,
        [InjectOptional] TurnCameraController turnCamera)
    {
        _board = board;
        _player = player;
        _cheats = cheats;
        _turnView = turnView;
        _enPassant = enPassant;
        _turnCamera = turnCamera;
    }

    public void SetFirstTeam(Team team)
    {
        _gameOver = false;
        _currentTeam = team;
        _cheats?.Clear();
        _turnView?.ShowTurn(_currentTeam);
        // Старт: snap без анимации
        _turnCamera?.OnTurnChanged(_currentTeam, snap: true);
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
            // Тот же юнит — не дёргать Select/SFX снова
            if (cell.Unit == _selected)
                return;
            SelectUnit(cell.Unit);
            return;
        }

        // --- Клик по допустимой (легальной) клетке ---
        if (_targets.Contains(cell))
        {
            var moving = _selected;
            var to = cell;

            PushUndoSnapshot();
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

        unit.GetComponent<UnitAudio>()?.PlaySelect();

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
        // После IsBusy=false / onCompleted: камера на сторону того, кто ходит
        _turnCamera?.OnTurnChanged(_currentTeam, snap: false);
        EvaluatePosition(showTurnIfQuiet: true);
    }

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

            // Анимация + звук смерти короля проигравшей стороны
            PlayMatedKingDeath(_currentTeam);
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

    // Мат: Death + sink у короля стороны, которой поставили мат
    private void PlayMatedKingDeath(Team matedTeam)
    {
        var king = ChessMoveGenerator.FindKing(_board, matedTeam);
        if (king == null)
        {
            Debug.LogWarning($"[Chess] Нет короля {matedTeam} для анимации мата");
            return;
        }

        king.GetComponent<UnitAudio>()?.PlayDeath();

        var anim = king.GetComponent<UnitAnimationDriver>();
        if (anim == null)
        {
            Debug.LogWarning($"[Chess] У короля {matedTeam} нет UnitAnimationDriver");
            return;
        }

        if (anim.IsDead)
            return;

        // Не Destroy: undo после мата должен вернуть короля на клетку.
        anim.StartCoroutine(anim.PlayDeathSinkAndHide(destroyGameObject: false));
    }

    public void CheatNextTurn()
    {
        if (IsBusy)
            return;

        PushUndoSnapshot();
        ClearSelection();
        _currentTeam = ChessMoveGenerator.Opposite(_currentTeam);
        _gameOver = false;
        Debug.Log($"[Cheat] NextTurn → {_currentTeam}");
        _turnCamera?.OnTurnChanged(_currentTeam, snap: false);
        EvaluatePosition(showTurnIfQuiet: true);
    }

    public bool CheatKillSelectedEnemy()
    {
        if (IsBusy)
            return false;

        var victim = ResolveCheatKillVictim();
        if (victim == null)
        {
            Debug.Log("[Cheat] Kill: нет выбранной своей фигуры с подсвеченной вражеской целью");
            return false;
        }

        if (victim.Team == _currentTeam)
        {
            Debug.Log("[Cheat] Kill: свою фигуру читом не убиваем");
            return false;
        }

        PushUndoSnapshot();
        KillUnitKeepForUndo(victim);
        ClearSelection();
        EvaluatePosition(showTurnIfQuiet: true);
        Debug.Log($"[Cheat] Kill {victim.name}");
        return true;
    }

    public void CheatUndo()
    {
        if (IsBusy)
            return;
        if (_cheats == null || !_cheats.TryPop(out var snap) || snap == null)
        {
            Debug.Log("[Cheat] Undo: стек пуст");
            return;
        }

        RestoreSnapshot(snap);
        Debug.Log($"[Cheat] Undo → {_currentTeam}");
    }

    Unit ResolveCheatKillVictim()
    {
        if (_selected == null || _selected.Team != _currentTeam)
            return null;

        var enemies = new List<Unit>();
        foreach (var cell in _targets)
        {
            if (cell == null)
                continue;
            if (ChessMoveGenerator.IsCapture(_selected, cell) && cell.Unit != null)
            {
                if (!enemies.Contains(cell.Unit))
                    enemies.Add(cell.Unit);
            }
            else if (ChessMoveGenerator.IsEnPassantLanding(_selected, cell, _enPassant)
                     && _enPassant != null && _enPassant.VictimPawn != null)
            {
                if (!enemies.Contains(_enPassant.VictimPawn))
                    enemies.Add(_enPassant.VictimPawn);
            }
        }

        if (enemies.Count == 0)
            return null;

        var hover = _board != null ? _board.LastHoveredCell : null;
        if (hover != null)
        {
            if (hover.Unit != null && enemies.Contains(hover.Unit))
                return hover.Unit;
            if (_enPassant != null && _enPassant.IsAvailable
                && hover == _enPassant.LandingCell
                && enemies.Contains(_enPassant.VictimPawn))
                return _enPassant.VictimPawn;
        }

        if (enemies.Count == 1)
            return enemies[0];

        return null;
    }

    void KillUnitKeepForUndo(Unit victim)
    {
        if (victim == null)
            return;

        victim.GetComponent<UnitAudio>()?.PlayDeath();
        victim.BindToCell(null, snap: false);
        _board?.UnregisterUnit(victim);

        var anim = victim.GetComponent<UnitAnimationDriver>();
        if (anim != null && !anim.IsDead)
            anim.StartCoroutine(anim.PlayDeathSinkAndHide(destroyGameObject: false));
        else
            victim.gameObject.SetActive(false);
    }

    void PushUndoSnapshot()
    {
        _cheats?.Push(CaptureSnapshot());
    }

    BoardSnapshot CaptureSnapshot()
    {
        var snap = new BoardSnapshot
        {
            CurrentTeam = _currentTeam,
            GameOver = _gameOver
        };

        if (_selected != null && _selected.Cell != null)
        {
            snap.SelectedX = _selected.Cell.X;
            snap.SelectedY = _selected.Cell.Y;
        }

        if (_enPassant != null && _enPassant.IsAvailable)
        {
            snap.EnPassantAvailable = true;
            snap.EnPassantVictim = _enPassant.VictimPawn;
            if (_enPassant.LandingCell != null)
            {
                snap.EnPassantLandingX = _enPassant.LandingCell.X;
                snap.EnPassantLandingY = _enPassant.LandingCell.Y;
            }
        }

        var units = Object.FindObjectsByType<Unit>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var unit in units)
        {
            if (unit == null)
                continue;
            var rec = new BoardSnapshot.UnitRecord
            {
                Unit = unit,
                HasCell = unit.Cell != null,
                CellX = unit.Cell != null ? unit.Cell.X : -1,
                CellY = unit.Cell != null ? unit.Cell.Y : -1,
                Position = unit.transform.position,
                Rotation = unit.transform.rotation,
                Scale = unit.transform.localScale,
                HasMoved = unit.HasMoved,
                Active = unit.gameObject.activeSelf,
                Dead = unit.GetComponent<UnitAnimationDriver>() != null
                       && unit.GetComponent<UnitAnimationDriver>().IsDead
            };
            snap.Units.Add(rec);
        }

        return snap;
    }

    void RestoreSnapshot(BoardSnapshot snap)
    {
        ClearSelection();

        if (_board != null)
        {
            _board.EnsureInitialized();
            foreach (var cell in _board.AllCells())
            {
                if (cell != null)
                    cell.Unit = null;
            }
        }

        foreach (var rec in snap.Units)
        {
            var unit = rec.Unit;
            if (unit == null)
                continue;

            _board?.UnregisterUnit(unit);
            unit.BindToCell(null, snap: false);

            var anim = unit.GetComponent<UnitAnimationDriver>();
            if (anim != null)
                anim.StopAllCoroutines();

            if (rec.Active && !rec.Dead)
            {
                if (anim != null)
                    anim.ReviveForUndo();
                else if (!unit.gameObject.activeSelf)
                    unit.gameObject.SetActive(true);
            }
            else if (unit.gameObject.activeSelf != rec.Active)
            {
                unit.gameObject.SetActive(rec.Active);
            }

            unit.transform.SetPositionAndRotation(rec.Position, rec.Rotation);
            unit.transform.localScale = rec.Scale;
            unit.HasMoved = rec.HasMoved;

            Cell cell = null;
            if (rec.HasCell && _board != null)
                cell = _board.GetCell(rec.CellX, rec.CellY);
            unit.BindToCell(cell, snap: false);

            if (rec.Active && !rec.Dead)
                _board?.RegisterUnit(unit);
        }

        Cell epLanding = null;
        if (snap.EnPassantLandingX >= 0 && _board != null)
            epLanding = _board.GetCell(snap.EnPassantLandingX, snap.EnPassantLandingY);
        _enPassant?.Restore(snap.EnPassantAvailable, snap.EnPassantVictim, epLanding);

        _currentTeam = snap.CurrentTeam;
        _gameOver = snap.GameOver;

        if (!_gameOver && snap.SelectedX >= 0 && _board != null)
        {
            var selCell = _board.GetCell(snap.SelectedX, snap.SelectedY);
            if (selCell != null && selCell.Unit != null && selCell.Unit.Team == _currentTeam)
                SelectUnit(selCell.Unit);
        }

        _turnCamera?.OnTurnChanged(_currentTeam, snap: true);
        EvaluatePosition(showTurnIfQuiet: true);
    }
}
