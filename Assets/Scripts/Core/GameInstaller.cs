using Zenject;

/// <summary>
/// Регистрация входа и зависимостей сцены.
/// </summary>
public class GameInstaller : MonoInstaller
{
    // Сохраняем Controls, чтобы выключить ввод при удалении объекта.
    private Controls _controls;

    public override void InstallBindings()
    {
        // Создаём экземпляр Input System.
        _controls = new Controls();

        // Включаем ввод.
        _controls.Enable();

        // Регистрируем Controls как общий объект сцены.
        Container.Bind<Controls>()
            .FromInstance(_controls)
            .AsSingle();

        // Подключаем карту экшенов Game.
        Container.Bind<Controls.GameActions>()
            .FromInstance(_controls.Game)
            .AsSingle();

        // Регистрируем менеджеры, которые уже есть в сцене.
        Container.Bind<InputManager>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<CellManager>()
            .FromComponentInHierarchy()
            .AsSingle();
    }

    private void OnDestroy()
    {
        // Выключаем ввод и освобождаем ресурсы.
        if (_controls == null)
            return;

        _controls.Disable();
        _controls.Dispose();
        _controls = null;
    }
}