public enum SpecialMoveKind
{
    None = 0,
    CastleKingSide = 1,  // O-O
    CastleQueenSide = 2, // O-O-O
    EnPassant = 3
}

public readonly struct ChessMove
{
    public readonly Unit Mover;
    public readonly Cell From;
    public readonly Cell To;
    public readonly SpecialMoveKind Special;
    public readonly Unit Rook;
    public readonly Cell RookFrom;
    public readonly Cell RookTo;
public readonly Unit CapturedUnit;

    public bool IsCastle =>
        Special == SpecialMoveKind.CastleKingSide ||
        Special == SpecialMoveKind.CastleQueenSide;

    public bool IsEnPassant => Special == SpecialMoveKind.EnPassant;

    public ChessMove(
        Unit mover,
        Cell from,
        Cell to,
        SpecialMoveKind special = SpecialMoveKind.None,
        Unit rook = null,
        Cell rookFrom = null,
        Cell rookTo = null,
        Unit capturedUnit = null)
    {
        Mover = mover;
        From = from;
        To = to;
        Special = special;
        Rook = rook;
        RookFrom = rookFrom;
        RookTo = rookTo;
        CapturedUnit = capturedUnit;
    }

    public static ChessMove EnPassant(Unit attacker, Cell landing, Unit victim)
    {
        return new ChessMove(
            attacker,
            attacker?.Cell,
            landing,
            SpecialMoveKind.EnPassant,
            capturedUnit: victim);
    }
}
