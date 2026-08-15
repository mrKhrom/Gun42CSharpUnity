using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PromotionPanel : MonoBehaviour, IPromotionUI
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _queenButton;
    [SerializeField] private Button _rookButton;
    [SerializeField] private Button _bishopButton;
    [SerializeField] private Button _knightButton;
    [SerializeField] private Text _titleLabel;

    private ChessPieceType? _chosen;
    private bool _buttonsWired;

    private void Awake()
    {
        EnsureUiBuilt();
        WireButtons();
        Hide();
    }

    public IEnumerator WaitForSelection(Team team, Action<ChessPieceType> onSelected)
    {
        EnsureUiBuilt();
        WireButtons();

        _chosen = null;
        if (_titleLabel != null)
            _titleLabel.text = $"Превращение ({(team == Team.White ? "White" : "Black")})";

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        Debug.Log($"[PromotionUI] Open for {team}");

        while (!_chosen.HasValue)
            yield return null;

        var type = _chosen.Value;
        onSelected?.Invoke(type);
        Debug.Log($"[PromotionUI] Selected: {type}");
        Hide();
    }

    public void Hide()
    {
        _chosen = null;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void WireButtons()
    {
        if (_buttonsWired)
            return;

        Bind(_queenButton, ChessPieceType.Queen);
        Bind(_rookButton, ChessPieceType.Rook);
        Bind(_bishopButton, ChessPieceType.Bishop);
        Bind(_knightButton, ChessPieceType.Knight);
        _buttonsWired = true;
    }

    private void Bind(Button button, ChessPieceType type)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnChoice(type));
    }

    private void OnChoice(ChessPieceType type)
    {
        if (!IsPromotable(type))
        {
            Debug.LogWarning($"[PromotionUI] Invalid type: {type}");
            return;
        }

        _chosen = type;
    }

    private static bool IsPromotable(ChessPieceType type)
    {
        return type == ChessPieceType.Queen
               || type == ChessPieceType.Rook
               || type == ChessPieceType.Bishop
               || type == ChessPieceType.Knight;
    }

private void EnsureUiBuilt()
    {
        if (_panelRoot != null && _queenButton != null && _rookButton != null
            && _bishopButton != null && _knightButton != null)
            return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        if (canvas == null)
        {
            var canvasGo = new GameObject(
                "Canvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // Корень компонента под Canvas
        if (transform.parent != canvas.transform)
            transform.SetParent(canvas.transform, false);

        var selfRt = GetComponent<RectTransform>();
        if (selfRt == null)
            selfRt = gameObject.AddComponent<RectTransform>();
        selfRt.anchorMin = Vector2.zero;
        selfRt.anchorMax = Vector2.one;
        selfRt.offsetMin = Vector2.zero;
        selfRt.offsetMax = Vector2.zero;

        if (_panelRoot == null)
        {
            _panelRoot = new GameObject("PromotionRoot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _panelRoot.transform.SetParent(transform, false);
            var rootRt = _panelRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Full-screen dim blocker — ловит клики, не пропускает на доску
            var dim = _panelRoot.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = true;

            var cg = _panelRoot.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        // Диалог по центру
        Transform dialog = _panelRoot.transform.Find("Dialog");
        if (dialog == null)
        {
            var dialogGo = new GameObject("Dialog", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            dialogGo.transform.SetParent(_panelRoot.transform, false);
            dialog = dialogGo.transform;

            var drt = dialogGo.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(360f, 280f);

            var bg = dialogGo.GetComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 0.96f);
            bg.raycastTarget = true;

            var vlg = dialogGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
        }

        if (_titleLabel == null)
        {
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(dialog, false);
            titleGo.GetComponent<LayoutElement>().preferredHeight = 40f;
            _titleLabel = titleGo.GetComponent<Text>();
            ApplyTextStyle(_titleLabel, 24, FontStyle.Bold);
            _titleLabel.text = "Превращение";
            _titleLabel.alignment = TextAnchor.MiddleCenter;
            _titleLabel.raycastTarget = false;
        }

        if (_queenButton == null)
            _queenButton = CreateChoiceButton(dialog, "Queen");
        if (_rookButton == null)
            _rookButton = CreateChoiceButton(dialog, "Rook");
        if (_bishopButton == null)
            _bishopButton = CreateChoiceButton(dialog, "Bishop");
        if (_knightButton == null)
            _knightButton = CreateChoiceButton(dialog, "Knight");
    }

    private static Button CreateChoiceButton(Transform parent, string label)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 44f;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.35f, 0.48f, 1f);
        img.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.4f, 0.5f, 0.65f, 1f);
        colors.pressedColor = new Color(0.2f, 0.25f, 0.35f, 1f);
        btn.colors = colors;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<Text>();
        ApplyTextStyle(text, 22, FontStyle.Normal);
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;

        return btn;
    }

    private static void ApplyTextStyle(Text text, int size, FontStyle style)
    {
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Show Panel")]
    private void DebugShow()
    {
        EnsureUiBuilt();
        WireButtons();
        if (_panelRoot != null)
            _panelRoot.SetActive(true);
    }

    [ContextMenu("Debug/Hide Panel")]
    private void DebugHide() => Hide();
#endif
}
