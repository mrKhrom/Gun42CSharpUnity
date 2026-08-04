using UnityEngine;
using Zenject;

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