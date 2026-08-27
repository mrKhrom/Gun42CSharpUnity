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

    [Header("UI audio")]
    [SerializeField] private AudioClip _playClickClip;

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

        StartCoroutine(OpenGameSceneRoutine());
    }

    System.Collections.IEnumerator OpenGameSceneRoutine()
    {
        Camera menuCamera = _mainMenuCamera != null ? _mainMenuCamera : Camera.main;

        // Клик, пока AudioListener меню ещё включён. Камеру меню не трогаем
        // до BindGameplayCamera в SceneInstaller.
        yield return PlayPlayButtonClickRoutine();

        Debug.Log("[SceneController] Loading GameScene (additive)...");
        SceneManager.LoadScene(1, LoadSceneMode.Additive);

        HidePlayButton();
        // Якоря хода уже сняты с камеры доски — меню можно выключать.
        DisableMenuCameraAndAudio(menuCamera);
        yield return EnsureSingleEventSystemNextFrame();
    }

    System.Collections.IEnumerator PlayPlayButtonClickRoutine()
    {
        var clip = ResolvePlayClickClip();
        if (clip == null)
        {
            Debug.LogWarning("[SceneController] Нет клипа клика Play (button-press)");
            yield break;
        }

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            clip.LoadAudioData();
            float wait = 1f;
            while (clip.loadState == AudioDataLoadState.Loading && wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            Debug.LogWarning("[SceneController] Клип клика не загрузился: " + clip.name);
            yield break;
        }

        var go = new GameObject("PlayButtonClick");
        DontDestroyOnLoad(go);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.spatialize = false;
        src.volume = 1f;
        src.ignoreListenerPause = true;
        src.PlayOneShot(clip, 1f);

        // Слышимый старт клика на слушателе меню, до LoadScene и выключения камеры.
        float audible = clip.length > 0.01f ? Mathf.Min(0.2f, clip.length) : 0.2f;
        yield return new WaitForSecondsRealtime(audible);

        float life = clip.length > 0.05f ? clip.length + 0.2f : 1.5f;
        Destroy(go, life);
    }

    AudioClip ResolvePlayClickClip()
    {
        if (_playClickClip != null)
            return _playClickClip;

#if UNITY_EDITOR
        _playClickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/UiAudio/button-press.mp3");
#endif
        return _playClickClip;
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
