using System;

[Flags]
public enum NeighbourType
{
    None = 0,

    Top = 1 << 0,
    Bottom = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,

    TopLeft = 1 << 4,
    TopRight = 1 << 5,
    BottomLeft = 1 << 6,
    BottomRight = 1 << 7,

    Horizontal = Left | Right,
    Vertical = Top | Bottom,
    Orthogonal = Top | Bottom | Left | Right,
    Diagonal = TopLeft | TopRight | BottomLeft | BottomRight,
    All = Orthogonal | Diagonal
}

public enum Team
{
    White = 0,
    Black = 1
}

public enum CellHighlight
{
    None,
    Hover,
    Selected,
    Move,
    Attack
}

public enum ChessPieceType
{
    Pawn = 0,
    Rook = 1,
    Knight = 2,
    Bishop = 3,
    Queen = 4,
    King = 5
}