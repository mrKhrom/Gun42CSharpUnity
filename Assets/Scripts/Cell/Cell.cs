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

public CellHighlight HighlightMode { get; private set; } = CellHighlight.None;

    public event Action<Cell> OnPointerClickEvent;
    public event Action<Cell> OnPointerEnterEvent;
    public event Action<Cell> Clicked
    {
        add => OnPointerClickEvent += value;
        remove => OnPointerClickEvent -= value;
    }

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

    public bool TryGetNeighbour(NeighbourType type, out Cell neighbour)
    {
        return Neighbours.TryGetValue(type, out neighbour) && neighbour != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnPointerEnterEvent?.Invoke(this);

        // Не перебиваем Selected / Move / Attack
        if (IsPersistentHighlight())
            return;

        SetFocusVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (IsPersistentHighlight())
            return;

        SetFocusVisible(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        OnPointerClickEvent?.Invoke(this);
    }

public void SetHighlight(CellHighlight mode, Material material = null)
    {
        if (mode == CellHighlight.Hover)
        {
            if (!IsPersistentHighlight())
                SetFocusVisible(true);
            return;
        }

        HighlightMode = mode;

        switch (mode)
        {
            case CellHighlight.None:
                ClearHighlight();
                break;

            case CellHighlight.Selected:
            case CellHighlight.Move:
            case CellHighlight.Attack:
                SetFocusVisible(false);
                ApplySelect(material, mode);
                break;
        }
    }

    public void SetFocusVisible(bool visible)
    {
        if (_focusRenderer == null)
            return;

        if (!_focusRenderer.gameObject.activeSelf)
            _focusRenderer.gameObject.SetActive(true);

        _focusRenderer.enabled = visible;
    }

    public void SetSelect(Material material)
    {
        ApplySelect(material, HighlightMode == CellHighlight.None
            ? CellHighlight.Selected
            : HighlightMode);
    }

    public void ResetSelect()
    {
        if (_selectRenderer == null) return;
        _selectRenderer.enabled = false;
    }

    public void ClearHighlight()
    {
        HighlightMode = CellHighlight.None;
        SetFocusVisible(false);
        ResetSelect();
    }

    private void ApplySelect(Material material, CellHighlight mode)
    {
        if (_selectRenderer == null) return;

        if (!_selectRenderer.gameObject.activeSelf)
            _selectRenderer.gameObject.SetActive(true);

        _selectRenderer.enabled = true;

        if (material != null)
        {
            _selectRenderer.sharedMaterial = material;
            return;
        }

        // Fallback-цвет, если материал не передан
        var mat = _selectRenderer.material;
        mat.color = mode switch
        {
            CellHighlight.Selected => new Color(0.2f, 0.45f, 1f, 0.65f),
            CellHighlight.Move => new Color(0.2f, 0.85f, 0.3f, 0.55f),
            CellHighlight.Attack => new Color(1f, 0.25f, 0.2f, 0.6f),
            _ => Color.white
        };
    }

    private bool IsPersistentHighlight()
    {
        return HighlightMode == CellHighlight.Selected
            || HighlightMode == CellHighlight.Move
            || HighlightMode == CellHighlight.Attack;
    }

    private void Awake()
    {
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
