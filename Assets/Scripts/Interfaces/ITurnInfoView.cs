/// <summary>
/// UI статуса партии: чей ход, шах, мат, пат.
/// Методы: ShowTurn — показать чья очередь; ShowCheck — показать шах;
/// ShowCheckmate — показать мат; ShowStalemate — показать пат; ShowStatus — произвольный текст.
/// </summary>
public interface ITurnInfoView
{
    void ShowTurn(Team team);

    void ShowCheck(Team teamInCheck);

    void ShowCheckmate(Team winner);

    void ShowStalemate();

    void ShowStatus(string message);
}
