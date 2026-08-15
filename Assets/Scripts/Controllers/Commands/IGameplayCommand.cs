public interface IGameplayCommand
{
    void Interact(Cell cell);
    void Cancel();
    void Confirm();
}