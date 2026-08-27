using System.Collections.Generic;

/// <summary>
/// Легальность ходов: король не остаётся под шахом. Симуляция хода туда-обратно.
/// Методы: GetLegalTargets — клетки без шаха своему королю; WouldLeaveKingInCheck — ход оставляет шах;
/// SideHasLegalMove — есть ли легальный ход; TryBuildDelta — собрать дельту хода;
/// Apply — применить ход на доске; Unapply — откатить ход.
/// </summary>
public static class ChessLegality
{
/// <summary>
/// Дельта одного хода для проверки шаха (кто куда, взятие, ладья при рокировке).
/// </summary>
public struct BoardDelta
    {
        public Unit Mover;
        public Cell From;
        public Cell To;
        public Unit Captured;
        public Cell CapturedFrom;
        public Unit Rook;
        public Cell RookFrom;
        public Cell RookTo;
        public SpecialMoveKind Kind;
    }

public static List<Cell> GetLegalTargets(
        Unit unit,
        Battlefield board,
        EnPassantState enPassant = null)
    {
        var legal = new List<Cell>();
        if (unit == null || board == null)
            return legal;

        var pseudo = ChessMoveGenerator.GetTargets(unit, board, enPassant);
        foreach (var to in pseudo)
        {
            if (!WouldLeaveKingInCheck(unit, to, board, enPassant))
                legal.Add(to);
        }

        return legal;
    }

public static bool WouldLeaveKingInCheck(
        Unit mover,
        Cell to,
        Battlefield board,
        EnPassantState enPassant = null)
    {
        if (mover == null || to == null || board == null || mover.Cell == null)
            return true;

        if (!TryBuildDelta(mover, to, board, enPassant, out var delta))
            return true;

        Apply(delta);
        bool inCheck = ChessMoveGenerator.IsKingInCheck(board, mover.Team);
        Unapply(delta);
        return inCheck;
    }

    public static bool SideHasLegalMove(
        Battlefield board,
        Team team,
        EnPassantState enPassant = null)
    {
        if (board == null)
            return false;

        for (int x = 0; x < Battlefield.Size; x++)
        {
            for (int y = 0; y < Battlefield.Size; y++)
            {
                var u = board.GetCell(x, y)?.Unit;
                if (u == null || u.Team != team)
                    continue;

                if (GetLegalTargets(u, board, enPassant).Count > 0)
                    return true;
            }
        }

        return false;
    }

    public static bool TryBuildDelta(
        Unit mover,
        Cell to,
        Battlefield board,
        EnPassantState enPassant,
        out BoardDelta delta)
    {
        delta = default;
        if (mover?.Cell == null || to == null || board == null)
            return false;

        if (ChessMoveGenerator.TryGetCastleMove(mover, to, board, out var castle))
        {
            delta = new BoardDelta
            {
                Kind = castle.Special,
                Mover = castle.Mover,
                From = castle.From,
                To = castle.To,
                Rook = castle.Rook,
                RookFrom = castle.RookFrom,
                RookTo = castle.RookTo
            };
            return true;
        }

        if (ChessMoveGenerator.TryGetEnPassantMove(mover, to, board, enPassant, out var ep))
        {
            delta = new BoardDelta
            {
                Kind = SpecialMoveKind.EnPassant,
                Mover = ep.Mover,
                From = ep.From,
                To = ep.To,
                Captured = ep.CapturedUnit,
                CapturedFrom = ep.CapturedUnit != null ? ep.CapturedUnit.Cell : null
            };
            return true;
        }

        // Обычный ход: взятие на to, если враг
        Unit captured = null;
        if (to.Unit != null && to.Unit != mover && to.Unit.Team != mover.Team)
            captured = to.Unit;

        delta = new BoardDelta
        {
            Kind = SpecialMoveKind.None,
            Mover = mover,
            From = mover.Cell,
            To = to,
            Captured = captured,
            CapturedFrom = captured != null ? to : null
        };
        return true;
    }

    public static void Apply(BoardDelta d)
    {
        if (d.Mover == null)
            return;

        if (d.Kind == SpecialMoveKind.CastleKingSide || d.Kind == SpecialMoveKind.CastleQueenSide)
        {
            d.Mover.BindToCell(d.To, snap: false);
            if (d.Rook != null && d.RookTo != null)
                d.Rook.BindToCell(d.RookTo, snap: false);
            return;
        }

        if (d.Captured != null)
            d.Captured.BindToCell(null, snap: false);

        d.Mover.BindToCell(d.To, snap: false);
    }

    public static void Unapply(BoardDelta d)
    {
        if (d.Mover == null)
            return;

        if (d.Kind == SpecialMoveKind.CastleKingSide || d.Kind == SpecialMoveKind.CastleQueenSide)
        {
            d.Mover.BindToCell(d.From, snap: false);
            if (d.Rook != null && d.RookFrom != null)
                d.Rook.BindToCell(d.RookFrom, snap: false);
            return;
        }

        d.Mover.BindToCell(d.From, snap: false);

        if (d.Captured != null && d.CapturedFrom != null)
            d.Captured.BindToCell(d.CapturedFrom, snap: false);
    }
}
