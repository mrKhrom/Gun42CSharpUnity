using UnityEngine;

/// <summary>
/// Каталог префабов фигур для смены визуала при promotion.
/// Слоты можно заполнить в Inspector или через Tools → Chess → Setup Piece Visual Library.
/// Если пусто — fallback на ChessSetup.GetPrefab.
/// </summary>
public class PieceVisualLibrary : MonoBehaviour
{
    public static PieceVisualLibrary Instance { get; private set; }

    [Header("White")]
    [SerializeField] private Unit _whitePawn;
    [SerializeField] private Unit _whiteRook;
    [SerializeField] private Unit _whiteKnight;
    [SerializeField] private Unit _whiteBishop;
    [SerializeField] private Unit _whiteQueen;
    [SerializeField] private Unit _whiteKing;

    [Header("Black")]
    [SerializeField] private Unit _blackPawn;
    [SerializeField] private Unit _blackRook;
    [SerializeField] private Unit _blackKnight;
    [SerializeField] private Unit _blackBishop;
    [SerializeField] private Unit _blackQueen;
    [SerializeField] private Unit _blackKing;

    private void Awake()
    {
        Instance = this;
        // Если слоты пусты — подтянуть из ChessSetup (сцена уже содержит ссылки на префабы)
        if (!HasAnyPrefabAssigned())
        {
            var setup = FindObjectOfType<ChessSetup>();
            if (setup != null)
                FillFromChessSetup(setup);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Заполнить слоты из ChessSetup (без warning, если setup null).</summary>
    public void FillFromChessSetup(ChessSetup setup)
    {
        if (setup == null)
            return;

        _whitePawn = setup.GetPrefab(Team.White, ChessPieceType.Pawn) ?? _whitePawn;
        _whiteRook = setup.GetPrefab(Team.White, ChessPieceType.Rook) ?? _whiteRook;
        _whiteKnight = setup.GetPrefab(Team.White, ChessPieceType.Knight) ?? _whiteKnight;
        _whiteBishop = setup.GetPrefab(Team.White, ChessPieceType.Bishop) ?? _whiteBishop;
        _whiteQueen = setup.GetPrefab(Team.White, ChessPieceType.Queen) ?? _whiteQueen;
        _whiteKing = setup.GetPrefab(Team.White, ChessPieceType.King) ?? _whiteKing;

        _blackPawn = setup.GetPrefab(Team.Black, ChessPieceType.Pawn) ?? _blackPawn;
        _blackRook = setup.GetPrefab(Team.Black, ChessPieceType.Rook) ?? _blackRook;
        _blackKnight = setup.GetPrefab(Team.Black, ChessPieceType.Knight) ?? _blackKnight;
        _blackBishop = setup.GetPrefab(Team.Black, ChessPieceType.Bishop) ?? _blackBishop;
        _blackQueen = setup.GetPrefab(Team.Black, ChessPieceType.Queen) ?? _blackQueen;
        _blackKing = setup.GetPrefab(Team.Black, ChessPieceType.King) ?? _blackKing;
    }

    public bool HasAnyPrefabAssigned()
    {
        return _whiteQueen != null || _whiteRook != null || _whiteKnight != null
               || _whiteBishop != null || _blackQueen != null || _blackRook != null
               || _blackKnight != null || _blackBishop != null
               || _whitePawn != null || _blackPawn != null
               || _whiteKing != null || _blackKing != null;
    }

    public Unit GetPrefab(Team team, ChessPieceType type)
    {
        var fromLib = GetFromSlots(team, type);
        if (fromLib != null)
            return fromLib;

        var setup = FindObjectOfType<ChessSetup>();
        return setup != null ? setup.GetPrefab(team, type) : null;
    }

    private Unit GetFromSlots(Team team, ChessPieceType type)
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

    /// <summary>
    /// Заменяет child-модель(и) и Animator на host, сохраняя Unit, Cell, Collider, transform root.
    /// </summary>
    public void ApplyVisual(Unit host, ChessPieceType newType)
    {
        if (host == null)
            return;

        var prefab = GetPrefab(host.Team, newType);
        if (prefab == null)
        {
            Debug.LogWarning(
                $"[PieceVisualLibrary] Нет префаба {host.Team}/{newType} — тип изменён, визуал старый");
            return;
        }

        ReplaceVisualFromPrefab(host, prefab.gameObject);
    }

    public static void ReplaceVisualFromPrefab(Unit host, GameObject prefabRoot)
    {
        if (host == null || prefabRoot == null)
            return;

        var hostTf = host.transform;

        // 1) Удалить старые child-модели сразу (Destroy отложен — мешает replace в том же кадре)
        for (int i = hostTf.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(hostTf.GetChild(i).gameObject);

        // 2) Временный instance префаба
        var temp = Object.Instantiate(prefabRoot);
        temp.name = $"{prefabRoot.name}_PromoteTemp";
        temp.transform.SetPositionAndRotation(hostTf.position, hostTf.rotation);

        // 3) Animator на root (у префабов контроллер на корне)
        var srcAnim = temp.GetComponent<Animator>();
        var dstAnim = host.GetComponent<Animator>();
        if (srcAnim != null)
        {
            if (dstAnim == null)
                dstAnim = host.gameObject.AddComponent<Animator>();

            dstAnim.runtimeAnimatorController = srcAnim.runtimeAnimatorController;
            dstAnim.avatar = srcAnim.avatar;
            dstAnim.applyRootMotion = false;
            dstAnim.cullingMode = srcAnim.cullingMode;
            dstAnim.updateMode = srcAnim.updateMode;
        }

        // 4) Перенести всех детей модели под host (локальные transform из префаба)
        while (temp.transform.childCount > 0)
        {
            var child = temp.transform.GetChild(0);
            child.SetParent(hostTf, false);
        }

        // 5) Если mesh/skinned был на root префаба (редко) — копируем как child
        var rootSmr = temp.GetComponent<SkinnedMeshRenderer>();
        var rootMf = temp.GetComponent<MeshFilter>();
        if (rootSmr != null || rootMf != null)
        {
            var visualGo = new GameObject("PromotedMesh");
            visualGo.transform.SetParent(hostTf, false);
            if (rootMf != null)
            {
                var mf = visualGo.AddComponent<MeshFilter>();
                mf.sharedMesh = rootMf.sharedMesh;
                var mrSrc = temp.GetComponent<MeshRenderer>();
                if (mrSrc != null)
                {
                    var mr = visualGo.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = mrSrc.sharedMaterials;
                }
            }
        }

        if (Application.isPlaying)
            Object.Destroy(temp);
        else
            Object.DestroyImmediate(temp);

        Debug.Log($"[PieceVisualLibrary] Visual → {prefabRoot.name} on {host.name}");
    }
}
