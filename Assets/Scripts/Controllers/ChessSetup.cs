using UnityEngine;

public class ChessSetup : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Battlefield _board;
    [SerializeField] private Transform _unitsRoot;

    [Header("Префабы белых")]
    [SerializeField] private Unit _whitePawn;
    [SerializeField] private Unit _whiteRook;
    [SerializeField] private Unit _whiteKnight;
    [SerializeField] private Unit _whiteBishop;
    [SerializeField] private Unit _whiteQueen;
    [SerializeField] private Unit _whiteKing;

    [Header("Префабы чёрных")]
    [SerializeField] private Unit _blackPawn;
    [SerializeField] private Unit _blackRook;
    [SerializeField] private Unit _blackKnight;
    [SerializeField] private Unit _blackBishop;
    [SerializeField] private Unit _blackQueen;
    [SerializeField] private Unit _blackKing;

    [Header("Опции")]
    [Tooltip("Удалить все Unit на сцене перед спавном")]
    [SerializeField] private bool _clearExistingUnits = false;

    [Tooltip("Спавнить автоматически (Start / GameBootstrap). Выкл — фигуры из сцены.")]
    [SerializeField] private bool _spawnOnStart = false;

    [SerializeField] private float _heightOffset = 0f; // если модель утопает — подними

    private static readonly ChessPieceType[] BackRank =
    {
        ChessPieceType.Rook,
        ChessPieceType.Knight,
        ChessPieceType.Bishop,
        ChessPieceType.Queen,
        ChessPieceType.King,
        ChessPieceType.Bishop,
        ChessPieceType.Knight,
        ChessPieceType.Rook
    };

    private void Start()
    {
        // Если есть GameBootstrap — spawn вызовет он через TrySpawnIfConfigured.
        // Если bootstrap нет — уважаем флаг здесь.
        if (_spawnOnStart && FindObjectOfType<GameBootstrap>() == null)
            SpawnStandardPosition();
    }

public void TrySpawnIfConfigured()
    {
        if (_spawnOnStart)
            SpawnStandardPosition();
    }

[ContextMenu("Spawn Standard Position")]
    public void SpawnStandardPosition()
    {
        if (_board == null)
            _board = FindObjectOfType<Battlefield>();

        if (_board == null)
        {
            Debug.LogError("[ChessSetup] Нет Battlefield");
            return;
        }

        _board.EnsureInitialized();

        if (_clearExistingUnits)
            ClearAllUnits();

        // Белые: последний ряд + пешки
        SpawnBackRank(Team.White, y: 0);
        for (int x = 0; x < 8; x++)
            Spawn(Team.White, ChessPieceType.Pawn, x, 1);

        // Чёрные: последний ряд + пешки
        SpawnBackRank(Team.Black, y: 7);
        for (int x = 0; x < 8; x++)
            Spawn(Team.Black, ChessPieceType.Pawn, x, 6);

        _board.RelinkUnits(); // обновить список Units на доске
        Debug.Log("[ChessSetup] Стандартная позиция: 32 фигуры");
    }

    [ContextMenu("Clear All Units")]
    public void ClearAllUnits()
    {
        // Важно: DestroyImmediate в Edit Mode; в Play — Destroy
        var units = FindObjectsOfType<Unit>();
        foreach (var u in units)
        {
            if (u == null) continue;
            if (Application.isPlaying)
                Destroy(u.gameObject);
            else
                DestroyImmediate(u.gameObject);
        }
    }

    private void SpawnBackRank(Team team, int y)
    {
        for (int x = 0; x < 8; x++)
            Spawn(team, BackRank[x], x, y);
    }

    private void Spawn(Team team, ChessPieceType type, int x, int y)
    {
        var prefab = GetPrefab(team, type);
        if (prefab == null)
        {
            Debug.LogError($"[ChessSetup] Нет префаба: {team} {type}");
            return;
        }

        var cell = _board.GetCell(x, y);
        if (cell == null)
        {
            Debug.LogError($"[ChessSetup] Нет клетки ({x},{y})");
            return;
        }

        Transform parent = _unitsRoot != null ? _unitsRoot : transform;
        var unit = Instantiate(prefab, parent);

        // Логика: team, type, cell
        unit.Setup(team, type, cell, snap: true);

        // Высота (snap в Unit сохраняет Y; при необходимости выстави)
        var p = unit.transform.position;
        if (_heightOffset != 0f)
            unit.transform.position = new Vector3(p.x, p.y + _heightOffset, p.z);

        // Ориентация: чёрные лицом к белым (если модели смотрят +Z)
        if (team == Team.Black)
            unit.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        else
            unit.transform.rotation = Quaternion.identity;
    }

public Unit GetPrefab(Team team, ChessPieceType type)
    {
        if (team == Team.White)
        {
            return type switch
            {
                ChessPieceType.Pawn => _whitePawn,
                ChessPieceType.Rook => _whiteRook,
                ChessPieceType.Knight => _whiteKnight,
                ChessPieceType.Bishop => _whiteBishop,
                ChessPieceType.Queen => _whiteQueen,
                ChessPieceType.King => _whiteKing,
                _ => null
            };
        }

        return type switch
        {
            ChessPieceType.Pawn => _blackPawn,
            ChessPieceType.Rook => _blackRook,
            ChessPieceType.Knight => _blackKnight,
            ChessPieceType.Bishop => _blackBishop,
            ChessPieceType.Queen => _blackQueen,
            ChessPieceType.King => _blackKing,
            _ => null
        };
    }
}