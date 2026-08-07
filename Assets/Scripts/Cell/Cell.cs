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
        if (_focusRenderer != null)
            _focusRenderer.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_focusRenderer != null)
            _focusRenderer.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnPointerClickEvent?.Invoke(this);
    }

    public void SetSelect(Material material)
    {
        if (_selectRenderer == null) return;
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
        if (_focusRenderer != null) _focusRenderer.enabled = false;
        if (_selectRenderer != null) _selectRenderer.enabled = false;
    }
}
