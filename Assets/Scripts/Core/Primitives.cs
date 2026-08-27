using System;

// Общие enum’ы проекта: соседи клетки, стороны, подсветка, типы фигур.

/// <summary>
/// Направление соседа на сетке (ортогональ и диагональ).
/// </summary>
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

/// <summary>
/// Сторона игрока: White / Black.
/// </summary>
public enum Team
{
    White = 0,
    Black = 1
}

/// <summary>
/// Режим подсветки клетки: hover, выбор, ход, атака.
/// </summary>
public enum CellHighlight
{
    None,
    Hover,
    Selected,
    Move,
    Attack
}

/// <summary>
/// Тип шахматной фигуры: пешка, ладья, конь, слон, ферзь, король.
/// </summary>
public enum ChessPieceType
{
    Pawn = 0,
    Rook = 1,
    Knight = 2,
    Bishop = 3,
    Queen = 4,
    King = 5
}