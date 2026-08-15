using UnityEngine;
using Zenject;

public class GameBootstrap : MonoBehaviour
{
    private Battlefield _board;
    private IGameplayCommand _command;
    private ITurnInfoView _turnView;
    private GameSettings _settings;
    private ChessSetup _chessSetup;
    private BattleController _battleController;

    private bool _started;

    [Inject]
    private void Construct(
        Battlefield board,
        IGameplayCommand command,
        ITurnInfoView turnView,
        [InjectOptional] GameSettings settings,
        [InjectOptional] ChessSetup chessSetup,
        [InjectOptional] BattleController battleController)
    {
        _board = board;
        _command = command;
        _turnView = turnView;
        _settings = settings;
        _chessSetup = chessSetup;
        _battleController = battleController;
    }

    private void Start()
    {
        RunBootstrap();
    }

    /// <summary>Можно вызвать повторно после reload логики без смены сцены.</summary>
    public void RunBootstrap()
    {
        if (_board == null)
        {
            Debug.LogError("[GameBootstrap] Battlefield not injected");
            return;
        }

        // 1. Доска (клетки, граф, link scene units)
        _board.EnsureInitialized();

        // 2. Опциональный spawn — только если ChessSetup сам настроен на auto-spawn.
        //    Не форсируем: пользователь может держать фигуры в сцене.
        if (_chessSetup != null)
            _chessSetup.TrySpawnIfConfigured();

        // 3. Подписка кликов (на случай раннего OnEnable до готовности доски)
        _battleController?.EnsureSubscribed();

        // 4. Первый ход
        Team first = ResolveFirstTeam();
        if (_command is ChessCommand chess)
        {
            chess.SetFirstTeam(first);
            Debug.Log($"[GameBootstrap] First team: {first}");
        }
        else
        {
            Debug.LogWarning("[GameBootstrap] Command is not ChessCommand");
        }

        // 5. UI
        _turnView?.ShowTurn(first);

        _started = true;
        Debug.Log("[GameBootstrap] Ready");
    }

    private Team ResolveFirstTeam()
    {
        if (_settings == null)
            return Team.White;

        if (_settings.randomFirstPlayer)
            return Random.value < 0.5f ? Team.White : Team.Black;

        return _settings.firstTeam;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Run Bootstrap Again")]
    private void DebugRerun()
    {
        _started = false;
        RunBootstrap();
    }
#endif
}
