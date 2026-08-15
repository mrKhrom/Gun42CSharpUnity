using UnityEngine;
using Zenject;

public class SceneInstaller : MonoInstaller
{
    [Header("Настройки (этап 13)")]
    [SerializeField] private GameSettings _gameSettings;

    private Controls _controls;

    public override void InstallBindings()
    {
        // --- Ввод ---
        _controls = new Controls();
        _controls.Enable();

        Container.Bind<Controls>()
            .FromInstance(_controls)
            .AsSingle();

        Container.Bind<Controls.GameActions>()
            .FromInstance(_controls.Game)
            .AsSingle();

        // --- Настройки ---
        if (_gameSettings == null)
            _gameSettings = Resources.Load<GameSettings>("GameSettings");

#if UNITY_EDITOR
        if (_gameSettings == null)
        {
            _gameSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<GameSettings>(
                "Assets/ScriptableObjects/GameSettings.asset");
        }
#endif

        if (_gameSettings != null)
        {
            Container.Bind<GameSettings>()
                .FromInstance(_gameSettings)
                .AsSingle();
        }
        else
        {
            Debug.LogWarning(
                "[SceneInstaller] GameSettings не найден — default-значения в коде.");
        }

        // --- En passant (необяз. ТЗ: взятие на проходе) ---
        Container.Bind<EnPassantState>()
            .AsSingle();

        // --- Основные компоненты сцены ---
        BindFromHierarchy<Battlefield>();
        BindFromHierarchy<PlayerController>();
        BindFromHierarchy<BattleController>();
        BindFromHierarchyOptional<ChessSetup>();
        BindFromHierarchyOptional<GameBootstrap>();
        BindFromHierarchyOptional<InputManager>();
        BindFromHierarchyOptional<CellManager>();

        // --- UI (этап 14) ---
        var turnView = FindObjectOfType<TurnInfoView>(true);
        if (turnView == null)
            turnView = CreateTurnInfoView();

        Container.Bind<ITurnInfoView>()
            .FromInstance(turnView)
            .AsSingle();

        Container.Bind<TurnInfoView>()
            .FromInstance(turnView)
            .AsSingle();

        // --- UI: превращение пешки (необяз. ТЗ п.6) ---
        var promotion = FindObjectOfType<PromotionPanel>(true);
        if (promotion == null)
            promotion = CreatePromotionPanel();

        Container.Bind<IPromotionUI>()
            .FromInstance(promotion)
            .AsSingle();

        Container.Bind<PromotionPanel>()
            .FromInstance(promotion)
            .AsSingle();

        // --- Команда: один instance как интерфейс и класс ---
        Container.BindInterfacesAndSelfTo<ChessCommand>()
            .AsSingle();
    }

    private void BindFromHierarchy<T>() where T : Component
    {
        var c = FindObjectOfType<T>(true);
        if (c == null)
        {
            Debug.LogError($"[SceneInstaller] Не найден обязательный {typeof(T).Name}");
            return;
        }

        Container.Bind<T>().FromInstance(c).AsSingle();
    }

    private void BindFromHierarchyOptional<T>() where T : Component
    {
        var c = FindObjectOfType<T>(true);
        if (c == null)
            return; // опционально — без warning в Console

        Container.Bind<T>().FromInstance(c).AsSingle();
    }

    private static TurnInfoView CreateTurnInfoView()
    {
        var canvas = EnsureCanvas();

        var go = new GameObject("TurnInfoView", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var view = go.AddComponent<TurnInfoView>();
        Debug.Log("[SceneInstaller] TurnInfoView создан runtime");
        return view;
    }

    private static PromotionPanel CreatePromotionPanel()
    {
        var canvas = EnsureCanvas();

        var go = new GameObject("PromotionPanel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var panel = go.AddComponent<PromotionPanel>();
        Debug.Log("[SceneInstaller] PromotionPanel создан runtime");
        return panel;
    }

    private static Canvas EnsureCanvas()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
            return canvas;

        var canvasGo = new GameObject(
            "Canvas",
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas;
    }

    private void OnDestroy()
    {
        if (_controls == null)
            return;

        _controls.Disable();
        _controls.Dispose();
        _controls = null;
    }
}
