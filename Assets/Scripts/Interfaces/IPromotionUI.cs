using System;
using System.Collections;

/// <summary>
/// ТЗ необязательное правило 1: UI выбора превращения пешки.
/// Cancel/Esc не закрывает панель — только выбор кнопки.
/// </summary>
public interface IPromotionUI
{
    /// <summary>
    /// Показать панель и дождаться выбора (Queen / Rook / Bishop / Knight).
    /// Корутина завершается после выбора; onSelected вызывается с выбранным типом.
    /// </summary>
    IEnumerator WaitForSelection(Team team, Action<ChessPieceType> onSelected);

    void Hide();
}
