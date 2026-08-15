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
    private GameSettings _settings;
    private bool _injected;
    private bool _restartBound;
    private bool _isRestartHeld;
    private float _fillProgress;

    [Inject]
    private void Construct(
        Controls.GameActions gameActions,
        [InjectOptional] GameSettings settings)
    {
        _gameActions = gameActions;
        _settings = settings;
        _injected = true;
        if (_settings != null && _settings.restartHoldDuration > 0f)
            _fillDuration = _settings.restartHoldDuration;

        // OnEnable мог сработать до Inject.
        if (isActiveAndEnabled)
            BindRestartActions();
    }

    private void OnEnable()
    {
        BindRestartActions();
    }

    private void OnDisable()
    {
        UnbindRestartActions();
    }

    private void BindRestartActions()
    {
        // До Inject GameActions default → Restart бросает NRE.
        if (!_injected || _restartBound)
            return;

        _gameActions.Restart.started += OnRestartStarted;
        _gameActions.Restart.canceled += OnRestartCanceled;
        _restartBound = true;
    }

    private void UnbindRestartActions()
    {
        if (!_restartBound)
            return;

        _gameActions.Restart.started -= OnRestartStarted;
        _gameActions.Restart.canceled -= OnRestartCanceled;
        _restartBound = false;
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