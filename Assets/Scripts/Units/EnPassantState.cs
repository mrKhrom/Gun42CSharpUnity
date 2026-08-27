using UnityEngine;

/// <summary>
/// Взятие на проходе на текущий ход: жертва и клетка приземления.
/// Методы: Set — открыть окно en passant; Clear — закрыть окно; Restore — восстановить из снимка;
/// MatchesLanding — клетка приземления совпала; CanCaptureBy — эта пешка может бить.
/// </summary>
public class EnPassantState
{
    public bool IsAvailable { get; private set; }
    public Unit VictimPawn { get; private set; }
    public Cell LandingCell { get; private set; }

    public void Set(Unit victim, Cell landing)
    {
        if (victim == null || landing == null || victim.Type != ChessPieceType.Pawn)
        {
            Clear();
            return;
        }

        IsAvailable = true;
        VictimPawn = victim;
        LandingCell = landing;
        Debug.Log(
            $"[EnPassant] Available: victim={victim.Team} at " +
            $"({victim.Cell?.X},{victim.Cell?.Y}), landing=({landing.X},{landing.Y})");
    }

    public void Clear()
    {
        if (IsAvailable)
            Debug.Log("[EnPassant] Cleared");

        IsAvailable = false;
        VictimPawn = null;
        LandingCell = null;
    }

    public void Restore(bool available, Unit victim, Cell landing)
    {
        if (!available || victim == null || landing == null)
        {
            Clear();
            return;
        }

        IsAvailable = true;
        VictimPawn = victim;
        LandingCell = landing;
    }

    public bool MatchesLanding(Cell cell)
    {
        return IsAvailable && cell != null && LandingCell == cell;
    }

public bool CanCaptureBy(Unit attacker)
    {
        if (!IsAvailable || attacker == null || attacker.Type != ChessPieceType.Pawn)
            return false;
        if (VictimPawn == null || VictimPawn.Cell == null || LandingCell == null)
            return false;
        if (VictimPawn.Team == attacker.Team)
            return false;
        if (attacker.Cell == null)
            return false;

        // Жертва и бьющая на одной горизонтали; соседние по x
        if (VictimPawn.Cell.Y != attacker.Cell.Y)
            return false;
        if (Mathf.Abs(VictimPawn.Cell.X - attacker.Cell.X) != 1)
            return false;

        // Landing — диагональ вперёд для attacker
        int dir = ChessMoveGenerator.Forward(attacker.Team);
        if (LandingCell.X != VictimPawn.Cell.X)
            return false;
        if (LandingCell.Y != attacker.Cell.Y + dir)
            return false;
        if (LandingCell.Unit != null)
            return false;

        return true;
    }
}
