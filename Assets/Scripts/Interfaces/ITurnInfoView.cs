/// <summary>
/// Этап 14: UI текущего хода + статус (шах / мат / пат).
/// </summary>
public interface ITurnInfoView
{
    void ShowTurn(Team team);

    void ShowCheck(Team teamInCheck);

    void ShowCheckmate(Team winner);

    void ShowStalemate();

    void ShowStatus(string message);
}
