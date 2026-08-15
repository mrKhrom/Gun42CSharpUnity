using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// UI текущего хода. Если Label не задан — создаёт Text под Canvas в runtime.
/// </summary>
public class TurnInfoView : MonoBehaviour, ITurnInfoView
{
    [SerializeField] private Text _label;
    [SerializeField] private string _format = "Ход: {0}";

    private GameSettings _settings;

    // Optional только на параметре: [Inject(Optional=true)] на методе ломает Zenject Install.
    [Inject]
    private void Construct([InjectOptional] GameSettings settings)
    {
        _settings = settings;
        if (_settings != null && !string.IsNullOrEmpty(_settings.turnLabelFormat))
            _format = _settings.turnLabelFormat;
    }

    private void Awake()
    {
        EnsureLabel();
    }

    public void ShowTurn(Team team)
    {
        EnsureLabel();
        if (_label == null)
            return;

        _label.text = string.Format(_format, team == Team.White ? "White" : "Black");
    }

    private void EnsureLabel()
    {
        if (_label != null)
            return;

        _label = GetComponentInChildren<Text>(true);
        if (_label != null)
            return;

        // Runtime UI: поверх Canvas (или собственный Canvas)
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        Transform parent = canvas != null ? canvas.transform : transform;

        var textGo = new GameObject("TurnLabel", typeof(RectTransform));
        textGo.transform.SetParent(parent, false);

        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -24f);
        rt.sizeDelta = new Vector2(480f, 48f);

        _label = textGo.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_label.font == null)
            _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _label.fontSize = 28;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
        _label.raycastTarget = false;
        _label.text = string.Format(_format, "White");

        // Обводка для читаемости на светлой доске
        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }
}
