using System;
using System.Collections.Generic;
using UnityEngine;

public class CellManager : MonoBehaviour
{
    public event Action<Cell> OnCellClicked;

    private readonly List<Cell> _cells = new();
    private readonly List<Unit> _units = new();

    private static readonly (int dx, int dy, NeighbourType type)[] NeighbourOffsets =
    {
        ( 0,  1, NeighbourType.Top),
        ( 0, -1, NeighbourType.Bottom),
        (-1,  0, NeighbourType.Left),
        ( 1,  0, NeighbourType.Right),
        (-1,  1, NeighbourType.TopLeft),
        ( 1,  1, NeighbourType.TopRight),
        (-1, -1, NeighbourType.BottomLeft),
        ( 1, -1, NeighbourType.BottomRight),
    };

    private void Start()
    {
        FindAllCells();
        LinkNeighbours();
        SubscribeToCells();
        FindAndLinkUnits();
    }

    private void FindAllCells()
    {
        _cells.Clear();
        _cells.AddRange(FindObjectsOfType<Cell>());

        // Клетки размером 1: центры на целых координатах 0..7 по X и Z
        const float step = 1f;

        foreach (var cell in _cells)
        {
            Vector3 p = cell.transform.position;

            // X сетки = world X, Y сетки = world Z (ряды Line идут по оси Z)
            int x = Mathf.RoundToInt(p.x / step);
            int y = Mathf.RoundToInt(p.z / step);

            cell.Init(x, y);
        }

        Debug.Log($"[CellManager] Клеток: {_cells.Count}");
    }

    private void LinkNeighbours()
    {
        var map = new Dictionary<Vector2Int, Cell>();
        foreach (var cell in _cells)
            map[new Vector2Int(cell.X, cell.Y)] = cell;

        foreach (var cell in _cells)
        {
            foreach (var (dx, dy, type) in NeighbourOffsets)
            {
                var key = new Vector2Int(cell.X + dx, cell.Y + dy);
                if (map.TryGetValue(key, out var neighbour))
                    cell.SetNeighbour(type, neighbour);
            }
        }
    }

    private void SubscribeToCells()
    {
        foreach (var cell in _cells)
            cell.OnPointerClickEvent += OnAnyCellClicked;
    }

    private void OnAnyCellClicked(Cell cell)
    {
        OnCellClicked?.Invoke(cell);
        Debug.Log($"[CellManager] Клик: {cell.X},{cell.Y}");
    }

    private void FindAndLinkUnits()
    {
        _units.Clear();
        _units.AddRange(FindObjectsOfType<Unit>());

        foreach (var unit in _units)
        {
            Cell closest = null;
            float bestDist = float.MaxValue;

            foreach (var cell in _cells)
            {
                Vector3 u = unit.transform.position;
                Vector3 c = cell.transform.position;
                float dist = Vector2.Distance(new Vector2(u.x, u.z), new Vector2(c.x, c.z));

                if (dist < bestDist)
                {
                    bestDist = dist;
                    closest = cell;
                }
            }

            if (closest != null && bestDist < 0.75f)
            {
                unit.Cell = closest;
                closest.Unit = unit;

                var p = closest.transform.position;
                unit.transform.position = new Vector3(p.x, p.y + 0.5f, p.z);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var cell in _cells)
        {
            if (cell != null)
                cell.OnPointerClickEvent -= OnAnyCellClicked;
        }
    }
}