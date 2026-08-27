using System;
using System.Collections;

/// <summary>
/// UI превращения пешки: выбор типа фигуры.
/// Методы: WaitForSelection — дождаться выбора игрока; Hide — скрыть панель.
/// </summary>
public interface IPromotionUI
{
IEnumerator WaitForSelection(Team team, Action<ChessPieceType> onSelected);

    void Hide();
}
