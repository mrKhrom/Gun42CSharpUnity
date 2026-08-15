using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [Header("UI меню")]
    [Tooltip("Кнопка Play — скроется после загрузки GameScene")]
    [SerializeField] private GameObject _playButton;

    [Header("Камера меню (MainScene)")]
    [Tooltip("Камера MainScene: выключится вместе с AudioListener, чтобы не было двух слушателей")]
    [SerializeField] private Camera _mainMenuCamera;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive GameScene: один EventSystem (MainScene). GameScene ES inactive + guard.
        EventSystemGuard.CleanupDuplicates();
        EventSystemGuard.EnsureOneActive();
        EnsureSingleEventSystem();
    }

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
            EnsureSingleEventSystem();
            return;
        }

        // Запоминаем камеру меню ДО загрузки — после additive Camera.main может смениться
        Camera menuCamera = _mainMenuCamera != null ? _mainMenuCamera : Camera.main;

        Debug.Log("[SceneController] Loading GameScene (additive)...");
        SceneManager.LoadScene(1, LoadSceneMode.Additive);

        HidePlayButton();
        DisableMenuCameraAndAudio(menuCamera);
        // sceneLoaded тоже вызывает EnsureSingleEventSystem; повтор на следующий кадр
        // на случай, если EventSystem активируется после callback
        StartCoroutine(EnsureSingleEventSystemNextFrame());
    }

    private System.Collections.IEnumerator EnsureSingleEventSystemNextFrame()
    {
        yield return null;
        EnsureSingleEventSystem();
    }

public static void EnsureSingleEventSystem()
    {
        var systems = Object.FindObjectsOfType<EventSystem>(true);
        if (systems == null || systems.Length <= 1)
            return;

        // Предпочитаем EventSystem из MainScene (bootstrap).
        EventSystem keep = null;
        foreach (var es in systems)
        {
            if (es == null) continue;
            if (es.gameObject.scene.name == "MainScene")
            {
                keep = es;
                break;
            }
        }

        if (keep == null)
            keep = systems[0];

        int removed = 0;
        foreach (var es in systems)
        {
            if (es == null || es == keep)
                continue;
            Debug.Log($"[SceneController] Destroying extra EventSystem on '{es.gameObject.scene.name}/{es.gameObject.name}'");
            Object.Destroy(es.gameObject);
            removed++;
        }

        if (removed > 0)
            Debug.Log($"[SceneController] Extra EventSystems removed: {removed}. Kept: {keep.gameObject.scene.name}/{keep.name}");
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
