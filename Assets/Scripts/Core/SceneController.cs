using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [Header("UI меню")]
    [Tooltip("Кнопка Play — скроется после загрузки GameScene")]
    [SerializeField] private GameObject _playButton;

    [Header("Камера меню (MainScene)")]
    [Tooltip("Камера MainScene: выключится вместе с AudioListener, чтобы не было двух слушателей")]
    [SerializeField] private Camera _mainMenuCamera;

    public void OpenMainScene()
    {
        // Сцена с индексом 0 (MainScene)
        SceneManager.LoadScene(0);
    }

    public void OpenGameScene()
    {
        if (SceneManager.GetSceneByName("GameScene").isLoaded)
        {
            Debug.Log("[SceneController] GameScene already loaded");
            return;
        }

        // Запоминаем камеру меню ДО загрузки — после additive Camera.main может смениться
        Camera menuCamera = _mainMenuCamera != null ? _mainMenuCamera : Camera.main;

        Debug.Log("[SceneController] Loading GameScene (additive)...");
        SceneManager.LoadScene(1, LoadSceneMode.Additive);

        HidePlayButton();
        DisableMenuCameraAndAudio(menuCamera);
    }

    private void HidePlayButton()
    {
        GameObject button = _playButton;

        if (button == null)
            button = GameObject.Find("PlayButton");

        if (button != null)
            button.SetActive(false);
        else
            Debug.LogWarning("[SceneController] PlayButton not found — assign it in Inspector");
    }

    private static void DisableMenuCameraAndAudio(Camera menuCamera)
    {
        if (menuCamera == null)
        {
            Debug.LogWarning("[SceneController] Main menu camera not found");
            return;
        }

        // Выключаем весь объект камеры: и Camera, и AudioListener (ровно один слушатель останется в GameScene)
        menuCamera.gameObject.SetActive(false);
    }
}
