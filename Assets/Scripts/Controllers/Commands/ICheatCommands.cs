/// <summary>
/// Читы Play Mode: смена хода, убийство выбранного врага, отмена.
/// Методы: CheatNextTurn — отдать ход сопернику; CheatKillSelectedEnemy — убить выбранного врага;
/// CheatUndo — отменить последний ход. Свойство: IsBusy — идёт анимация хода.
/// </summary>
public interface ICheatCommands
{
    bool IsBusy { get; }
    void CheatNextTurn();
    bool CheatKillSelectedEnemy();
    void CheatUndo();
}
