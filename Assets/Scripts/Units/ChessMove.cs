/// <summary>
/// Специальный ход (рокировка и т.п.).
/// </summary>
public enum SpecialMoveKind
{
    None = 0,
    CastleKingSide = 1,  // O-O
    CastleQueenSide = 2  // O-O-O
}

/// <summary>
/// Описание хода: обычный (Special=None) или рокировка (король + ладья).
/// </summary>
public readonly struct ChessMove
{
    public readonly Unit Mover;
    public readonly Cell From;
    public readonly Cell To;
    public readonly SpecialMoveKind Special;
    public readonly Unit Rook;
    public readonly Cell RookFrom;
    public readonly Cell RookTo;

    public bool IsCastle =>
        Special == SpecialMoveKind.CastleKingSide ||
        Special == SpecialMoveKind.CastleQueenSide;

    public ChessMove(
        Unit mover,
        Cell from,
        Cell to,
        SpecialMoveKind special = SpecialMoveKind.None,
        Unit rook = null,
        Cell rookFrom = null,
        Cell rookTo = null)
    {
        Mover = mover;
        From = from;
        To = to;
        Special = special;
        Rook = rook;
        RookFrom = rookFrom;
        RookTo = rookTo;
    }

    public static ChessMove Normal(Unit mover, Cell to)
    {
        return new ChessMove(mover, mover?.Cell, to);
    }
}
