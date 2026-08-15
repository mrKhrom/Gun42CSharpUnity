using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

/// <summary>
/// ТЗ: обрабатывает input (Select / Cancel / Confirm) и отдаёт в IGameplayCommand.
/// Select = клик по Cell/Unit (EventSystem).
/// Cancel = Esc, Confirm = Space (Input System).
/// Restart (R) обрабатывает InputManager — здесь не трогаем.
/// </summary>
public class BattleController : MonoBehaviour
{
    private IGameplayCommand _command;
    private Battlefield _board;
    private Controls.GameActions _game;

    private bool _subscribedToBoard;

    [Inject]
    private void Construct(
        IGameplayCommand command,
        Battlefield board,
        Controls.GameActions gameActions)
    {
        _command = command;
        _board = board;
        _game = gameActions;
    }

    private void OnEnable()
    {
        // Клавиатура
        // _game — struct; после Inject должен быть валиден
        _game.Cancel.performed += OnCancelPerformed;
        _game.Confirm.performed += OnConfirmPerformed;

        SubscribeBoard();
    }

    private void OnDisable()
    {
        _game.Cancel.performed -= OnCancelPerformed;
        _game.Confirm.performed -= OnConfirmPerformed;

        UnsubscribeBoard();
    }

    private void Start()
    {
        if (_board != null)
        {
            _board.EnsureInitialized();
            SubscribeBoard();
        }

        // Первый ход: White (по ТЗ можно random)
        if (_command is ChessCommand chess)
        {
            chess.SetFirstTeam(Team.White);
            Debug.Log($"[BattleController] First team: {chess.CurrentTeam}");
        }
        else
        {
            Debug.LogWarning(
                "[BattleController] IGameplayCommand is not ChessCommand — SetFirstTeam skipped");
        }

        if (_command == null)
            Debug.LogError("[BattleController] IGameplayCommand not injected!");
        if (_board == null)
            Debug.LogError("[BattleController] Battlefield not injected!");
    }

    private void SubscribeBoard()
    {
        if (_subscribedToBoard || _board == null)
            return;

        _board.OnCellClicked += OnCellClicked;
        _subscribedToBoard = true;
        Debug.Log("[BattleController] Subscribed to Battlefield.OnCellClicked");
    }

    private void UnsubscribeBoard()
    {
        if (!_subscribedToBoard || _board == null)
            return;

        _board.OnCellClicked -= OnCellClicked;
        _subscribedToBoard = false;
    }

    /// <summary>Select: ЛКМ по клетке или фигуре.</summary>
    private void OnCellClicked(Cell cell)
    {
        if (_command == null)
        {
            Debug.LogError("[BattleController] No command on click");
            return;
        }

        _command.Interact(cell);
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        _command?.Cancel();
        Debug.Log("[BattleController] Cancel (Esc)");
    }

    private void OnConfirmPerformed(InputAction.CallbackContext context)
    {
        _command?.Confirm();
        Debug.Log("[BattleController] Confirm (Space)");
    }
}