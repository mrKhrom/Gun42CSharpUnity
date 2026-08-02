using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public void OpenMainScene()
    {
        // Сцена с индексом 0 (MainScene)
        SceneManager.LoadScene(0);
    }

    public void OpenGameScene()
    {
        // Сцена с индексом 1 (GameScene)
        SceneManager.LoadScene(1, LoadSceneMode.Additive);
    }
}