using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public class SceneController : MonoBehaviour
{
    // ===== С Zenject =====
    // Zenject сам найдёт ScoreService в контейнере и подставит сюда.
    [Inject]
    private ScoreService _scoreService;

    // ===== БЕЗ Zenject (для сравнения) =====
    // private ScoreService _scoreService;
    //
    // public void Init(ScoreService scoreService)
    // {
    //     _scoreService = scoreService; // вручную прокинули ссылку
    // }
    //
    // void Awake()
    // {
    //     // Или так — жёсткая связь, сами создаём:
    //     // _scoreService = new ScoreService();
    // }

    private void Start()
    {
        // Проверка, что DI сработал
        _scoreService.Add(10);
        Debug.Log($"SceneController: текущие очки = {_scoreService.Score}");
    }

    public void OpenMainScene()
    {
        SceneManager.LoadScene(0);
    }

    public void OpenGameScene()
    {
        SceneManager.LoadScene(1, LoadSceneMode.Additive);
    }
}