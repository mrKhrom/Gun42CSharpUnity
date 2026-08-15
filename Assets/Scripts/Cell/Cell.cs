using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Cell : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("Визуалы")]
    [SerializeField] private MeshRenderer _focusRenderer;
    [SerializeField] private MeshRenderer _selectRenderer;

    [Header("Сетка")]
    [SerializeField] private int _x;
    [SerializeField] private int _y;

    public Dictionary<NeighbourType, Cell> Neighbours { get; } = new();
    public Unit Unit { get; set; }

    public int X => _x;
    public int Y => _y;

    public event Action<Cell> OnPointerClickEvent;

    private bool _isFocused;

    public void Init(int x, int y)
    {
        _x = x;
        _y = y;
        name = $"Cell_{x}_{y}";
    }

    public void SetNeighbour(NeighbourType type, Cell cell)
    {
        if (cell == null) return;
        Neighbours[type] = cell;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetFocusVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetFocusVisible(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnPointerClickEvent?.Invoke(this);
    }

    /// <summary>
    /// Hover highlight (Focus child). Also called from Unit when ray hits figure collider.
    /// </summary>
    public void SetFocusVisible(bool visible)
    {
        _isFocused = visible;
        if (_focusRenderer == null)
            return;

        // Keep GO active so references stay valid; toggle renderer only.
        if (!_focusRenderer.gameObject.activeSelf)
            _focusRenderer.gameObject.SetActive(true);

        _focusRenderer.enabled = visible;
    }

    public void SetSelect(Material material)
    {
        if (_selectRenderer == null) return;

        if (!_selectRenderer.gameObject.activeSelf)
            _selectRenderer.gameObject.SetActive(true);

        _selectRenderer.enabled = true;
        if (material != null)
            _selectRenderer.material = material;
    }

    public void ResetSelect()
    {
        if (_selectRenderer == null) return;
        _selectRenderer.enabled = false;
    }

    private void Awake()
    {
        // Auto-wire if Inspector refs lost
        if (_focusRenderer == null)
        {
            var t = transform.Find("Focus");
            if (t != null) _focusRenderer = t.GetComponent<MeshRenderer>();
        }

        if (_selectRenderer == null)
        {
            var t = transform.Find("Select");
            if (t != null) _selectRenderer = t.GetComponent<MeshRenderer>();
        }

        if (_focusRenderer != null)
            _focusRenderer.enabled = false;
        if (_selectRenderer != null)
            _selectRenderer.enabled = false;

        // Focus/Select must NOT have colliders — they steal raycasts from Cell.
        // Hover is delivered only to the hit collider's object; click walks parents.
        DisableHighlightColliders(_focusRenderer);
        DisableHighlightColliders(_selectRenderer);
    }

    private static void DisableHighlightColliders(MeshRenderer renderer)
    {
        if (renderer == null) return;
        foreach (var col in renderer.GetComponents<Collider>())
            col.enabled = false;
    }
}
