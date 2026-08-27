using UnityEngine;
using Zenject;

/// <summary>
/// Zenject-установщик MainScene: регистрирует SceneController.
/// Методы: InstallBindings — объявить зависимости меню.
/// </summary>
public class MainInstaller : MonoInstaller
{
    [SerializeField] private SceneController _sceneController;

    public override void InstallBindings()
    {
        Container.Bind<SceneController>()
            .FromInstance(_sceneController)
            .AsSingle();
    }
}