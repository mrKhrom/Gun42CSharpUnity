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

    private void Awake()
    {
        // До OnEnable чужих ES / после load: один EventSystem
        EventSystemGuard.CleanupDuplicates();
        EventSystemGuard.EnsureOneActive();
    }

    private void Start()
    {
        RunBootstrap();
    }

public void RunBootstrap()
    {
        if (_started)
            return;

        RunBootstrapInternal();
        _started = true;
    }

public void ForceRunBootstrap()
    {
        _started = false;
        RunBootstrap();
    }

    private void RunBootstrapInternal()
    {
        EventSystemGuard.CleanupDuplicates();
        EventSystemGuard.EnsureOneActive();

        if (_board == null)
        {
            Debug.LogError("[GameBootstrap] Battlefield not injected");
            return;
        }

        // 1. Доска (клетки, граф, link scene units — фигуры уже в сцене)
        _board.EnsureInitialized();

        // 2. Опциональный spawn — только если ChessSetup настроен на auto-spawn.
        //    При расстановке в сцене TrySpawnIfConfigured — no-op.
        if (_chessSetup != null)
            _chessSetup.TrySpawnIfConfigured();

        // 2b. Меньше shadow casters на multi-mesh моделях (тени остаются у основного mesh)
        GraphicsPerformance.OptimizeUnitShadowCasters();

        // 3. Подписка кликов (на случай раннего OnEnable до готовности доски)
        _battleController?.EnsureSubscribed();

        // 4. Первый ход + UI / оценка позиции
        Team first = ResolveFirstTeam();
        if (_command is ChessCommand chess)
        {
            chess.SetFirstTeam(first);
            Debug.Log($"[GameBootstrap] First team: {first}");
        }
        else
        {
            Debug.LogWarning("[GameBootstrap] Command is not ChessCommand");
            _turnView?.ShowTurn(first);
        }

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
        ForceRunBootstrap();
    }
#endif
}
