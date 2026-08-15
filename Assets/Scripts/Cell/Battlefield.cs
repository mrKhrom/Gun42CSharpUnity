using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Игровое поле 8×8: инициализация клеток, граф соседей,
/// связь клеток с фигурами, API подсветки и соседей.
/// </summary>
public class Battlefield : MonoBehaviour
{
    public const int Size = 8;
    private const float GridStep = 1f;
    private const float UnitLinkMaxDistance = 0.75f;

    [Header("Подсветка (материалы Select)")]
    [SerializeField] private Material _selectedMaterial;
    [SerializeField] private Material _moveMaterial;
    [SerializeField] private Material _attackMaterial;

    private readonly Cell[,] _cells = new Cell[Size, Size];
    private readonly List<Unit> _units = new();
    private bool _ready;
    private bool _clicksSubscribed;

    /// <summary>Select: клик по клетке (и по фигуре через прокидывание Unit → Cell).</summary>
    public event Action<Cell> OnCellClicked;

    public bool IsReady => _ready;
    public IReadOnlyList<Unit> Units => _units;

    private void Awake()
    {
        TryAutoAssignHighlightMaterials();
        InitializeFromScene();
    }

    private void OnDestroy()
    {
        UnsubscribeCellClicks();
    }

    /// <summary>
    /// Находит Cell на сцене, проставляет координаты, строит граф,
    /// связывает фигуры, подписывается на клики.
    /// x = world X, y = world Z.
    /// </summary>
    public void InitializeFromScene()
    {
        if (_ready)
            return;

        for (int x = 0; x < Size; x++)
        for (int y = 0; y < Size; y++)
            _cells[x, y] = null;

        var found = FindObjectsOfType<Cell>();
        int placed = 0;

        foreach (var cell in found)
        {
            if (cell == null) continue;

            Vector3 p = cell.transform.position;
            int x = Mathf.RoundToInt(p.x / GridStep);
            int y = Mathf.RoundToInt(p.z / GridStep);

            if (x < 0 || x >= Size || y < 0 || y >= Size)
            {
                Debug.LogWarning($"[Battlefield] Cell вне 0..7: {cell.name} -> ({x},{y})");
                continue;
            }

            if (_cells[x, y] != null && _cells[x, y] != cell)
            {
                Debug.LogWarning(
                    $"[Battlefield] Дубликат координаты ({x},{y}): {_cells[x, y].name} и {cell.name}");
            }

            cell.Init(x, y);
            _cells[x, y] = cell;
            placed++;
        }

        LinkNeighbours();
        LinkUnitsToCells();
        SubscribeCellClicks();

        _ready = true;
        Debug.Log($"[Battlefield] Готово: клеток={placed}, фигур={_units.Count}");

        if (placed != Size * Size)
            Debug.LogWarning($"[Battlefield] Ожидалось {Size * Size} клеток, найдено {placed}");
    }

    /// <summary>Повторная привязка фигур (после спавна / перезагрузки).</summary>
    public void RelinkUnits()
    {
        LinkUnitsToCells();
    }

    public void EnsureInitialized()
    {
        if (!_ready)
            InitializeFromScene();
    }

    public Cell GetCell(int x, int y)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size)
            return null;
        return _cells[x, y];
    }

    /// <summary>Сосед по типу (если граф построен).</summary>
    public Cell GetNeighbour(Cell cell, NeighbourType type)
    {
        if (cell == null) return null;
        return cell.TryGetNeighbour(type, out var n) ? n : null;
    }

    /// <summary>Все 8 соседей (null-слоты пропускаются в перечислении значений).</summary>
    public IEnumerable<Cell> GetNeighbours(Cell cell)
    {
        if (cell == null) yield break;
        foreach (var pair in cell.Neighbours)
        {
            if (pair.Value != null)
                yield return pair.Value;
        }
    }

    public IEnumerable<Cell> AllCells()
    {
        for (int x = 0; x < Size; x++)
        for (int y = 0; y < Size; y++)
        {
            if (_cells[x, y] != null)
                yield return _cells[x, y];
        }
    }

    public void ClearAllHighlights()
    {
        foreach (var c in AllCells())
            c.ClearHighlight();
    }

    public void HighlightCells(IEnumerable<Cell> cells, CellHighlight mode)
    {
        if (cells == null) return;
        var mat = MaterialFor(mode);
        foreach (var c in cells)
        {
            if (c != null)
                c.SetHighlight(mode, mat);
        }
    }

    public void HighlightCell(Cell cell, CellHighlight mode)
    {
        if (cell == null) return;
        cell.SetHighlight(mode, MaterialFor(mode));
    }

    /// <summary>
    /// Подсветка возможных ходов фигуры (этапы 5+6 вместе, для проверки / Command).
    /// Selected = клетка фигуры, Move = пустые, Attack = враг.
    /// </summary>
    public void HighlightMovesFor(Unit unit)
    {
        ClearAllHighlights();
        if (unit == null || unit.Cell == null)
            return;

        HighlightCell(unit.Cell, CellHighlight.Selected);

        // Debug: легальные ходы (без EP state — EP в debug не учитывается)
        foreach (var target in ChessLegality.GetLegalTargets(unit, this, null))
        {
            bool attack = ChessMoveGenerator.IsCapture(unit, target);
            HighlightCell(target, attack ? CellHighlight.Attack : CellHighlight.Move);
        }
    }

    private Material MaterialFor(CellHighlight mode)
    {
        return mode switch
        {
            CellHighlight.Selected => _selectedMaterial,
            CellHighlight.Move => _moveMaterial,
            CellHighlight.Attack => _attackMaterial,
            _ => null
        };
    }

    private void LinkNeighbours()
    {
        for (int x = 0; x < Size; x++)
        for (int y = 0; y < Size; y++)
        {
            var cell = _cells[x, y];
            if (cell == null) continue;

            cell.Neighbours.Clear();

            TryLink(x, y,  0,  1, NeighbourType.Top);
            TryLink(x, y,  0, -1, NeighbourType.Bottom);
            TryLink(x, y, -1,  0, NeighbourType.Left);
            TryLink(x, y,  1,  0, NeighbourType.Right);
            TryLink(x, y, -1,  1, NeighbourType.TopLeft);
            TryLink(x, y,  1,  1, NeighbourType.TopRight);
            TryLink(x, y, -1, -1, NeighbourType.BottomLeft);
            TryLink(x, y,  1, -1, NeighbourType.BottomRight);
        }
    }

    private void TryLink(int x, int y, int dx, int dy, NeighbourType type)
    {
        var n = GetCell(x + dx, y + dy);
        if (n != null)
            _cells[x, y].SetNeighbour(type, n);
    }

    private void LinkUnitsToCells()
    {
        // Сброс старых связей unit → cell
        foreach (var cell in AllCells())
            cell.Unit = null;

        _units.Clear();
        _units.AddRange(FindObjectsOfType<Unit>());

        foreach (var unit in _units)
        {
            if (unit == null) continue;

            Cell closest = null;
            float bestDist = float.MaxValue;

            foreach (var cell in AllCells())
            {
                float dist = HorizontalDistance(unit.transform.position, cell.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    closest = cell;
                }
            }

            if (closest != null && bestDist <= UnitLinkMaxDistance)
            {
                unit.BindToCell(closest, snap: false);
            }
            else
            {
                unit.BindToCell(null, snap: false);
                Debug.LogWarning(
                    $"[Battlefield] Не удалось привязать {unit.name} (dist={bestDist:F2})");
            }
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    private void SubscribeCellClicks()
    {
        if (_clicksSubscribed) return;

        foreach (var cell in AllCells())
            cell.OnPointerClickEvent += HandleCellClicked;

        _clicksSubscribed = true;
    }

    private void UnsubscribeCellClicks()
    {
        if (!_clicksSubscribed) return;

        foreach (var cell in AllCells())
        {
            if (cell != null)
                cell.OnPointerClickEvent -= HandleCellClicked;
        }

        _clicksSubscribed = false;
    }

    private void HandleCellClicked(Cell cell)
    {
        OnCellClicked?.Invoke(cell);
    }

    private void TryAutoAssignHighlightMaterials()
    {
#if UNITY_EDITOR
        if (_selectedMaterial == null)
            _selectedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/Select_Selected.mat");
        if (_moveMaterial == null)
            _moveMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/Select_MoveOrAttack.mat");
        if (_attackMaterial == null)
            _attackMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Materials/Select_MoveAndAttack.mat");
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Highlight moves of nearest unit to (0,0)")]
    private void DebugHighlightFirstUnit()
    {
        EnsureInitialized();
        Unit any = null;
        foreach (var u in _units)
        {
            if (u != null && u.Cell != null)
            {
                any = u;
                break;
            }
        }

        if (any == null)
        {
            Debug.LogWarning("[Battlefield] Нет фигур для теста подсветки");
            return;
        }

        HighlightMovesFor(any);
        Debug.Log($"[Battlefield] Подсветка ходов: {any.Team} {any.Type} @ ({any.Cell.X},{any.Cell.Y})");
    }
#endif
}
