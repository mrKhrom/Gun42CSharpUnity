using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Генерация псевдолегальных ходов по типу фигуры, рокировка, en passant, шах.
/// Методы: GetTargets — все клетки хода; GetTargetsSplit — отдельно ходы и атаки;
/// TryGetCastleMove — собрать рокировку; TryGetEnPassantMove — собрать взятие на проходе;
/// IsSquareAttacked — клетка под боем; IsKingInCheck — король под шахом; FindKing — найти короля;
/// IsCapture — ход берёт фигуру; IsAttackTarget — клетка для атаки.
/// </summary>
public static class ChessMoveGenerator
{
    public const int KingStartX = 4;
    public const int KingsideRookX = 7;
    public const int QueensideRookX = 0;

public static int Forward(Team team) => team == Team.White ? 1 : -1;

    public static Team Opposite(Team team) => team == Team.White ? Team.Black : Team.White;

public static bool IsPawnStartRank(Team team, int y)
    {
        return (team == Team.White && y == 1) || (team == Team.Black && y == 6);
    }

    public static int BackRank(Team team) => team == Team.White ? 0 : 7;

public static bool IsCapture(Unit mover, Cell target)
    {
        if (mover == null || target?.Unit == null) return false;
        return target.Unit.Team != mover.Team;
    }

public static bool IsAttackTarget(Unit mover, Cell target, EnPassantState enPassant = null)
    {
        if (IsCapture(mover, target))
            return true;
        return IsEnPassantLanding(mover, target, enPassant);
    }

    public static bool IsEnPassantLanding(Unit mover, Cell target, EnPassantState enPassant)
    {
        if (mover == null || target == null || enPassant == null || !enPassant.IsAvailable)
            return false;
        if (mover.Type != ChessPieceType.Pawn)
            return false;
        return enPassant.MatchesLanding(target) && enPassant.CanCaptureBy(mover);
    }

public static bool IsBlockedByAlly(Unit mover, Cell target)
    {
        if (mover == null || target?.Unit == null) return false;
        return target.Unit.Team == mover.Team;
    }

public static List<Cell> GetTargets(
        Unit unit,
        Battlefield board,
        EnPassantState enPassant = null)
    {
        var list = new List<Cell>();
        CollectTargets(unit, board, list, null, null, enPassant);
        return list;
    }

    public static void GetTargetsSplit(
        Unit unit,
        Battlefield board,
        List<Cell> moves,
        List<Cell> attacks,
        EnPassantState enPassant = null)
    {
        moves?.Clear();
        attacks?.Clear();
        CollectTargets(unit, board, null, moves, attacks, enPassant);
    }

public static bool TryGetCastleMove(Unit king, Cell target, Battlefield board, out ChessMove move)
    {
        move = default;
        if (king == null || target == null || board == null || king.Cell == null)
            return false;
        if (king.Type != ChessPieceType.King)
            return false;

        if (!TryBuildCastle(king, board, target.X, out var built))
            return false;

        if (built.To != target)
            return false;

        move = built;
        return true;
    }

public static bool TryGetEnPassantMove(
        Unit pawn,
        Cell target,
        Battlefield board,
        EnPassantState enPassant,
        out ChessMove move)
    {
        move = default;
        if (pawn == null || target == null || board == null || enPassant == null)
            return false;
        if (pawn.Type != ChessPieceType.Pawn || pawn.Cell == null)
            return false;
        if (!enPassant.MatchesLanding(target) || !enPassant.CanCaptureBy(pawn))
            return false;

        move = ChessMove.EnPassant(pawn, target, enPassant.VictimPawn);
        return true;
    }

public static bool IsSquareAttacked(Battlefield board, int x, int y, Team byTeam)
    {
        if (board == null)
            return false;

        for (int cx = 0; cx < Battlefield.Size; cx++)
        {
            for (int cy = 0; cy < Battlefield.Size; cy++)
            {
                var cell = board.GetCell(cx, cy);
                var u = cell?.Unit;
                if (u == null || u.Team != byTeam)
                    continue;

                if (DoesPieceAttackSquare(u, board, x, y))
                    return true;
            }
        }

        return false;
    }

public static bool IsKingInCheck(Battlefield board, Team team)
    {
        if (board == null)
            return false;

        var king = FindKing(board, team);
        if (king?.Cell == null)
            return false;

        return IsSquareAttacked(board, king.Cell.X, king.Cell.Y, Opposite(team));
    }

    public static Unit FindKing(Battlefield board, Team team)
    {
        if (board == null)
            return null;

        for (int x = 0; x < Battlefield.Size; x++)
        {
            for (int y = 0; y < Battlefield.Size; y++)
            {
                var u = board.GetCell(x, y)?.Unit;
                if (u != null && u.Team == team && u.Type == ChessPieceType.King)
                    return u;
            }
        }

        return null;
    }

    private static void CollectTargets(
        Unit unit,
        Battlefield board,
        List<Cell> combined,
        List<Cell> moves,
        List<Cell> attacks,
        EnPassantState enPassant)
    {
        if (unit == null || unit.Cell == null || board == null)
            return;

        int x = unit.Cell.X;
        int y = unit.Cell.Y;

        switch (unit.Type)
        {
            case ChessPieceType.Pawn:
                AddPawn(unit, board, x, y, combined, moves, attacks, enPassant);
                break;
            case ChessPieceType.Knight:
                AddOffsets(unit, board, x, y, KnightOffsets, combined, moves, attacks);
                break;
            case ChessPieceType.King:
                AddOffsets(unit, board, x, y, KingOffsets, combined, moves, attacks);
                TryAddCastling(unit, board, combined, moves);
                break;
            case ChessPieceType.Rook:
                AddRays(unit, board, x, y, RookDirs, combined, moves, attacks);
                break;
            case ChessPieceType.Bishop:
                AddRays(unit, board, x, y, BishopDirs, combined, moves, attacks);
                break;
            case ChessPieceType.Queen:
                AddRays(unit, board, x, y, QueenDirs, combined, moves, attacks);
                break;
        }
    }

    private static readonly (int dx, int dy)[] KnightOffsets =
    {
        (1, 2), (2, 1), (-1, 2), (-2, 1),
        (1, -2), (2, -1), (-1, -2), (-2, -1)
    };

    private static readonly (int dx, int dy)[] KingOffsets =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1)
    };

    private static readonly (int dx, int dy)[] RookDirs =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1)
    };

    private static readonly (int dx, int dy)[] BishopDirs =
    {
        (1, 1), (1, -1), (-1, 1), (-1, -1)
    };

    private static readonly (int dx, int dy)[] QueenDirs =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1)
    };

    private static void AddPawn(
        Unit unit, Battlefield board, int x, int y,
        List<Cell> combined, List<Cell> moves, List<Cell> attacks,
        EnPassantState enPassant)
    {
        int dir = Forward(unit.Team);

        var forward = board.GetCell(x, y + dir);
        if (forward != null && forward.Unit == null)
        {
            AddMove(forward, combined, moves);

            if (IsPawnStartRank(unit.Team, y))
            {
                var forward2 = board.GetCell(x, y + 2 * dir);
                if (forward2 != null && forward2.Unit == null)
                    AddMove(forward2, combined, moves);
            }
        }

        foreach (int dx in new[] { -1, 1 })
        {
            var diag = board.GetCell(x + dx, y + dir);
            if (diag == null)
                continue;

            // Обычное взятие
            if (diag.Unit != null && diag.Unit.Team != unit.Team)
            {
                AddAttack(diag, combined, attacks);
                continue;
            }

            // En passant: landing пуст, право активно
            if (diag.Unit == null
                && enPassant != null
                && enPassant.MatchesLanding(diag)
                && enPassant.CanCaptureBy(unit))
            {
                AddAttack(diag, combined, attacks);
            }
        }
    }

    private static void AddOffsets(
        Unit unit, Battlefield board, int x, int y,
        (int dx, int dy)[] offsets,
        List<Cell> combined, List<Cell> moves, List<Cell> attacks)
    {
        foreach (var (dx, dy) in offsets)
        {
            var c = board.GetCell(x + dx, y + dy);
            if (c == null) continue;

            if (c.Unit != null && c.Unit.Team == unit.Team)
                continue;

            if (c.Unit == null)
                AddMove(c, combined, moves);
            else
                AddAttack(c, combined, attacks);
        }
    }

    private static void AddRays(
        Unit unit, Battlefield board, int x, int y,
        (int dx, int dy)[] dirs,
        List<Cell> combined, List<Cell> moves, List<Cell> attacks)
    {
        foreach (var (dx, dy) in dirs)
        {
            int cx = x + dx;
            int cy = y + dy;

            while (true)
            {
                var c = board.GetCell(cx, cy);
                if (c == null)
                    break;

                if (c.Unit == null)
                {
                    AddMove(c, combined, moves);
                }
                else
                {
                    if (c.Unit.Team != unit.Team)
                        AddAttack(c, combined, attacks);
                    break;
                }

                cx += dx;
                cy += dy;
            }
        }
    }

    private static void TryAddCastling(
        Unit king,
        Battlefield board,
        List<Cell> combined,
        List<Cell> moves)
    {
        if (king.HasMoved || king.Cell == null)
            return;

        // O-O
        if (TryBuildCastle(king, board, king.Cell.X + 2, out var ks))
            AddMove(ks.To, combined, moves);

        // O-O-O
        if (TryBuildCastle(king, board, king.Cell.X - 2, out var qs))
            AddMove(qs.To, combined, moves);
    }

private static bool TryBuildCastle(
        Unit king,
        Battlefield board,
        int kingToX,
        out ChessMove move)
    {
        move = default;

        if (king == null || board == null || king.Cell == null)
            return false;
        if (king.Type != ChessPieceType.King || king.HasMoved)
            return false;

        int y = king.Cell.Y;
        int fromX = king.Cell.X;

        if (fromX != KingStartX)
            return false;
        if (y != BackRank(king.Team))
            return false;

        SpecialMoveKind kind;
        int rookFromX;
        int rookToX;
        int[] mustBeEmpty;
        int[] mustBeSafe; // клетки, которые король занимает/проходит (включая start для шаха)

        if (kingToX == KingStartX + 2)
        {
            // O-O: король 4→6, ладья 7→5
            kind = SpecialMoveKind.CastleKingSide;
            rookFromX = KingsideRookX;
            rookToX = 5;
            mustBeEmpty = new[] { 5, 6 };
            mustBeSafe = new[] { 4, 5, 6 }; // e, f, g
        }
        else if (kingToX == KingStartX - 2)
        {
            // O-O-O: король 4→2, ладья 0→3
            kind = SpecialMoveKind.CastleQueenSide;
            rookFromX = QueensideRookX;
            rookToX = 3;
            mustBeEmpty = new[] { 1, 2, 3 };
            mustBeSafe = new[] { 4, 3, 2 }; // e, d, c
        }
        else
        {
            return false;
        }

        // Путь пуст
        foreach (int px in mustBeEmpty)
        {
            var c = board.GetCell(px, y);
            if (c == null || c.Unit != null)
                return false;
        }

        // Ладья
        var rookCell = board.GetCell(rookFromX, y);
        var rook = rookCell?.Unit;
        if (rook == null || rook.Team != king.Team || rook.Type != ChessPieceType.Rook)
            return false;
        if (rook.HasMoved)
            return false;

        // Шах / проход / финиш
        var enemy = Opposite(king.Team);
        foreach (int sx in mustBeSafe)
        {
            if (IsSquareAttacked(board, sx, y, enemy))
                return false;
        }

        var kingTo = board.GetCell(kingToX, y);
        var rookTo = board.GetCell(rookToX, y);
        if (kingTo == null || rookTo == null)
            return false;

        move = new ChessMove(
            king,
            king.Cell,
            kingTo,
            kind,
            rook,
            rookCell,
            rookTo);

        return true;
    }

private static bool DoesPieceAttackSquare(Unit attacker, Battlefield board, int tx, int ty)
    {
        if (attacker?.Cell == null || board == null)
            return false;

        int ax = attacker.Cell.X;
        int ay = attacker.Cell.Y;
        int dx = tx - ax;
        int dy = ty - ay;

        switch (attacker.Type)
        {
            case ChessPieceType.Pawn:
            {
                int dir = Forward(attacker.Team);
                return dy == dir && (dx == 1 || dx == -1);
            }
            case ChessPieceType.Knight:
                return (Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 2)
                       || (Mathf.Abs(dx) == 2 && Mathf.Abs(dy) == 1);
            case ChessPieceType.King:
                return Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1 && (dx != 0 || dy != 0);
            case ChessPieceType.Bishop:
                return IsClearDiagonal(board, ax, ay, tx, ty);
            case ChessPieceType.Rook:
                return IsClearOrthogonal(board, ax, ay, tx, ty);
            case ChessPieceType.Queen:
                return IsClearOrthogonal(board, ax, ay, tx, ty)
                       || IsClearDiagonal(board, ax, ay, tx, ty);
            default:
                return false;
        }
    }

    private static bool IsClearOrthogonal(Battlefield board, int ax, int ay, int tx, int ty)
    {
        if (ax != tx && ay != ty)
            return false;
        if (ax == tx && ay == ty)
            return false;
        return IsClearRay(board, ax, ay, tx, ty);
    }

    private static bool IsClearDiagonal(Battlefield board, int ax, int ay, int tx, int ty)
    {
        if (Mathf.Abs(tx - ax) != Mathf.Abs(ty - ay))
            return false;
        if (ax == tx && ay == ty)
            return false;
        return IsClearRay(board, ax, ay, tx, ty);
    }

private static bool IsClearRay(Battlefield board, int ax, int ay, int tx, int ty)
    {
        int stepX = tx.CompareTo(ax);
        int stepY = ty.CompareTo(ay);
        // CompareTo: -1,0,1 — достаточно для диагонали/ортогонали

        int cx = ax + stepX;
        int cy = ay + stepY;

        while (cx != tx || cy != ty)
        {
            var c = board.GetCell(cx, cy);
            if (c == null)
                return false;
            if (c.Unit != null)
                return false;
            cx += stepX;
            cy += stepY;
        }

        return true;
    }

    private static void AddMove(Cell cell, List<Cell> combined, List<Cell> moves)
    {
        combined?.Add(cell);
        moves?.Add(cell);
    }

    private static void AddAttack(Cell cell, List<Cell> combined, List<Cell> attacks)
    {
        combined?.Add(cell);
        attacks?.Add(cell);
    }
}
