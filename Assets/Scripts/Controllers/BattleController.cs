using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

/// <summary>
/// Ввод партии: клик по клетке, Cancel (Esc), Confirm (Space), Ctrl+Z.
/// Методы: Construct — получить зависимости Zenject; EnsureSubscribed — подписаться на клики доски.
/// Клики уходят в IGameplayCommand.Interact.
/// </summary>
public class BattleController : MonoBehaviour
{
    private IGameplayCommand _command;
    private ICheatCommands _cheats;
    private Battlefield _board;
    private Controls.GameActions _game;
    private InputAction _undo;
    private bool _injected;

    private bool _subscribedToBoard;
    private bool _inputBound;

    // Получаем доступ через DI (Zenject) к объектам, которые находятся в сцене. 
    // Например: BattleController не знает, что где-то есть класс ChessCommand. 
    // Он знает только: «мне нужен кто-то, у кого есть Interact / Cancel / Confirm».
    [Inject]
    private void Construct(
        IGameplayCommand command,
        Battlefield board,
        Controls.GameActions gameActions,
        [InjectOptional] ICheatCommands cheats)
    {
        _command = command;
        _board = board;
        _game = gameActions;
        _cheats = cheats;
        _injected = true;

        // OnEnable мог сработать до Inject — подпишемся сейчас.
        if (isActiveAndEnabled)
            BindInputAndBoard();
    }

    private void OnEnable()
    {
        BindInputAndBoard();
    }

    private void OnDisable()
    {
        UnbindInputAndBoard();
    }

    private void BindInputAndBoard()
    {
        // До Inject _game — default struct, Cancel/Restart бросают NRE.
        if (!_injected || _inputBound)
            return;

        _game.Cancel.performed += OnCancelPerformed;
        _game.Confirm.performed += OnConfirmPerformed;
        BindUndoAction();
        _inputBound = true;
        SubscribeBoard();
    }

    private void UnbindInputAndBoard()
    {
        if (_inputBound)
        {
            _game.Cancel.performed -= OnCancelPerformed;
            _game.Confirm.performed -= OnConfirmPerformed;
            UnbindUndoAction();
            _inputBound = false;
        }

        UnsubscribeBoard();
    }

    private void BindUndoAction()
    {
        if (_undo != null)
            return;

        _undo = new InputAction("UndoMove", InputActionType.Button);
        _undo.AddCompositeBinding("OneModifier")
            .With("modifier", "<Keyboard>/ctrl")
            .With("binding", "<Keyboard>/z");
        _undo.performed += OnUndoPerformed;
        _undo.Enable();
    }

    private void UnbindUndoAction()
    {
        if (_undo == null)
            return;

        _undo.performed -= OnUndoPerformed;
        _undo.Disable();
        _undo.Dispose();
        _undo = null;
    }

    private void Start()
    {
        if (_board != null)
            _board.EnsureInitialized();

        EnsureSubscribed();

        if (_command == null)
            Debug.LogError("[BattleController] IGameplayCommand not injected!");
        if (_board == null)
            Debug.LogError("[BattleController] Battlefield not injected!");
    }

public void EnsureSubscribed()
    {
        SubscribeBoard();
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

    private void OnUndoPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;
        if (_cheats == null || _cheats.IsBusy)
            return;

        _cheats.CheatUndo();
    }
}
