using System;
using System.Collections;

public interface IPromotionUI
{
IEnumerator WaitForSelection(Team team, Action<ChessPieceType> onSelected);

    void Hide();
}
