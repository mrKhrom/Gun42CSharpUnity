/// <summary>
/// Контракт игровой команды: клик по клетке, отмена выбора, подтверждение.
/// Методы: Interact — обработать клик; Cancel — сбросить выбор; Confirm — подтвердить действие.
/// </summary>
public interface IGameplayCommand
{
    void Interact(Cell cell);
    void Cancel();
    void Confirm();
}