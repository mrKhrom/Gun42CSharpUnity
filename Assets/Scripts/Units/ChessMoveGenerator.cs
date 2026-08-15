using System.Collections.Generic;

/// <summary>
/// Генерация возможных клеток хода/атаки по правилам шахмат (ТЗ 3.1–3.6, 4.1–4.2).
/// Не двигает фигуры — только считает цели для Command / подсветки.
/// </summary>
public static class ChessMoveGenerator
{
    /// <summary>Направление «вперёд» для пешки: White +Y (world +Z), Black −Y.</summary>
    public static int Forward(Team team) => team == Team.White ? 1 : -1;

    /// <summary>Стартовый ряд пешек: White y=1, Black y=6.</summary>
    public static bool IsPawnStartRank(Team team, int y)
    {
        return (team == Team.White && y == 1) || (team == Team.Black && y == 6);
    }

    /// <summary>На клетке враг относительно mover.</summary>
    public static bool IsCapture(Unit mover, Cell target)
    {
        if (mover == null || target?.Unit == null) return false;
        return target.Unit.Team != mover.Team;
    }

    /// <summary>На клетке союзник.</summary>
    public static bool IsBlockedByAlly(Unit mover, Cell target)
    {
        if (mover == null || target?.Unit == null) return false;
        return target.Unit.Team == mover.Team;
    }

    /// <summary>
    /// Все легальные (без шаха) цели: пустые клетки хода + клетки с врагом (взятие).
    /// Пешка: вперёд ≠ атака (ТЗ 3.1, 4.1).
    /// </summary>
    public static List<Cell> GetTargets(Unit unit, Battlefield board)
    {
        var list = new List<Cell>();
        CollectTargets(unit, board, list, null, null);
        return list;
    }

    /// <summary>
    /// Раздельно: quiet-ходы и взятия — удобно для Move/Attack подсветки.
    /// </summary>
    public static void GetTargetsSplit(
        Unit unit,
        Battlefield board,
        List<Cell> moves,
        List<Cell> attacks)
    {
        moves?.Clear();
        attacks?.Clear();
        CollectTargets(unit, board, null, moves, attacks);
    }

    private static void CollectTargets(
        Unit unit,
        Battlefield board,
        List<Cell> combined,
        List<Cell> moves,
        List<Cell> attacks)
    {
        if (unit == null || unit.Cell == null || board == null)
            return;

        int x = unit.Cell.X;
        int y = unit.Cell.Y;

        switch (unit.Type)
        {
            case ChessPieceType.Pawn:
                AddPawn(unit, board, x, y, combined, moves, attacks);
                break;
            case ChessPieceType.Knight:
                AddOffsets(unit, board, x, y, KnightOffsets, combined, moves, attacks);
                break;
            case ChessPieceType.King:
                AddOffsets(unit, board, x, y, KingOffsets, combined, moves, attacks);
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
        List<Cell> combined, List<Cell> moves, List<Cell> attacks)
    {
        int dir = Forward(unit.Team);

        // Ход вперёд (только пустые) — ТЗ 3.1 / 4.1
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

        // Атака только по диагонали вперёд на врага — ТЗ 3.1
        foreach (int dx in new[] { -1, 1 })
        {
            var diag = board.GetCell(x + dx, y + dir);
            if (diag?.Unit != null && diag.Unit.Team != unit.Team)
                AddAttack(diag, combined, attacks);
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

            // Союзная клетка — недоступна (ТЗ 4.2 для прыжков: просто нельзя встать)
            if (c.Unit != null && c.Unit.Team == unit.Team)
                continue;

            if (c.Unit == null)
                AddMove(c, combined, moves);
            else
                AddAttack(c, combined, attacks);
        }
    }

    /// <summary>
    /// Скользящие фигуры: пустые клетки + первая вражеская; союзник блокирует луч (ТЗ 4.2).
    /// </summary>
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
                    // Враг — можно взять, луч дальше не идёт
                    if (c.Unit.Team != unit.Team)
                        AddAttack(c, combined, attacks);
                    // Союзник — клетка недоступна, луч стоп
                    break;
                }

                cx += dx;
                cy += dy;
            }
        }
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
