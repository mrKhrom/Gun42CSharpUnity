using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public class InputManager : MonoBehaviour
{
    [SerializeField] private GameObject _restartPanel;
    [SerializeField] private Image _restartFillImage;
    [SerializeField] private float _fillDuration = 1.5f;

    private Controls.GameActions _gameActions;
    private bool _isRestartHeld;
    private float _fillProgress;

    [Inject]
    private void Construct(Controls.GameActions gameActions)
    {
        _gameActions = gameActions;
    }

    private void OnEnable()
    {
        _gameActions.Restart.started += OnRestartStarted;
        _gameActions.Restart.canceled += OnRestartCanceled;
    }

    private void OnDisable()
    {
        _gameActions.Restart.started -= OnRestartStarted;
        _gameActions.Restart.canceled -= OnRestartCanceled;
    }

    private void Start() => HideRestartUI();

    private void Update()
    {
        if (!_isRestartHeld) return;

        _fillProgress += Time.deltaTime / _fillDuration;
        _restartFillImage.fillAmount = Mathf.Clamp01(_fillProgress);

        if (_fillProgress >= 1f)
        {
            _isRestartHeld = false;
            ReloadScene();
        }
    }

    private void OnRestartStarted(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        _isRestartHeld = true;
        _fillProgress = 0f;
        _restartFillImage.fillAmount = 0f;
        if (_restartPanel != null)
            _restartPanel.SetActive(true);
    }

    private void OnRestartCanceled(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (_fillProgress < 1f)
        {
            _isRestartHeld = false;
            HideRestartUI();
        }
    }

    private void HideRestartUI()
    {
        _fillProgress = 0f;
        if (_restartFillImage != null)
            _restartFillImage.fillAmount = 0f;
        if (_restartPanel != null)
            _restartPanel.SetActive(false);
    }

    private void ReloadScene()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}