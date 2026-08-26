public interface ICheatCommands
{
    bool IsBusy { get; }
    void CheatNextTurn();
    bool CheatKillSelectedEnemy();
    void CheatUndo();
}
