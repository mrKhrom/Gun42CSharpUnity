using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

public class BattleController : MonoBehaviour
{
    private IGameplayCommand _command;
    private Battlefield _board;
    private Controls.GameActions _game;
    private bool _injected;

    private bool _subscribedToBoard;
    private bool _inputBound;

    [Inject]
    private void Construct(
        IGameplayCommand command,
        Battlefield board,
        Controls.GameActions gameActions)
    {
        _command = command;
        _board = board;
        _game = gameActions;
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
        _inputBound = true;
        SubscribeBoard();
    }

    private void UnbindInputAndBoard()
    {
        if (_inputBound)
        {
            _game.Cancel.performed -= OnCancelPerformed;
            _game.Confirm.performed -= OnConfirmPerformed;
            _inputBound = false;
        }

        UnsubscribeBoard();
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
}
