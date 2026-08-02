using UnityEngine;
using Zenject;

public class MainInstaller : MonoInstaller
{
    [SerializeField] private SceneController _sceneController;

    public override void InstallBindings()
    {
        // ===== С Zenject =====
        // Говорим контейнеру: "когда кто-то попросит ScoreService — создай один экземпляр".
        Container.Bind<ScoreService>().AsSingle();

        // SceneController тоже кладём в контейнер (как у вас уже было).
        Container.Bind<SceneController>()
            .FromInstance(_sceneController)
            .AsSingle();

        // ===== БЕЗ Zenject (для сравнения, не используем) =====
        // ScoreService scoreService = new ScoreService();
        // _sceneController.Init(scoreService); // пришлось бы вручную передавать всем
    }
}