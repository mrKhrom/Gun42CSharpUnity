using System;
using System.Collections;
using UnityEngine;
using Zenject;

public class PlayerController : MonoBehaviour
{
    private GameSettings _settings;
    private IPromotionUI _promotionUI;
    private EnPassantState _enPassant;
    private Battlefield _board;

    public bool IsBusy { get; private set; }

    // Optional только на параметре: [Inject(Optional=true)] на методе ломает Zenject Install.
    [Inject]
    private void Construct(
        [InjectOptional] GameSettings settings,
        [InjectOptional] IPromotionUI promotionUI,
        [InjectOptional] EnPassantState enPassant,
        [InjectOptional] Battlefield board)
    {
        _settings = settings;
        _promotionUI = promotionUI;
        _enPassant = enPassant;
        _board = board;
    }

    public void ExecuteMove(Unit unit, Cell target, Action onCompleted)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[PlayerController] Уже идёт ход");
            return;
        }

        if (unit == null || target == null)
        {
            onCompleted?.Invoke();
            return;
        }

        StartCoroutine(MoveRoutine(unit, target, onCompleted));
    }

    /// <summary>
    /// Рокировка: одновременно король и ладья. Без взятия и без promotion.
    /// </summary>
    public void ExecuteCastle(
        Unit king,
        Cell kingTo,
        Unit rook,
        Cell rookTo,
        Action onCompleted)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[PlayerController] Уже идёт ход (castle)");
            return;
        }

        if (king == null || kingTo == null || rook == null || rookTo == null)
        {
            onCompleted?.Invoke();
            return;
        }

        StartCoroutine(CastleRoutine(king, kingTo, rook, rookTo, onCompleted));
    }

    /// <summary>
    /// En passant: attacker → пустой landing, victim снимается со своей клетки.
    /// </summary>
    public void ExecuteEnPassant(
        Unit attacker,
        Cell landing,
        Unit victim,
        Action onCompleted)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[PlayerController] Уже идёт ход (en passant)");
            return;
        }

        if (attacker == null || landing == null || victim == null)
        {
            onCompleted?.Invoke();
            return;
        }

        StartCoroutine(EnPassantRoutine(attacker, landing, victim, onCompleted));
    }

    private IEnumerator MoveRoutine(Unit unit, Cell target, Action onCompleted)
    {
        IsBusy = true;

        // From — до BindToCell (для double-step en passant)
        int fromX = unit.Cell != null ? unit.Cell.X : -1;
        int fromY = unit.Cell != null ? unit.Cell.Y : -1;

        // 1) Взятие на клетке назначения
        if (target.Unit != null && target.Unit != unit)
        {
            var enemy = target.Unit;
            enemy.BindToCell(null, snap: false);
            Destroy(enemy.gameObject);
        }

        // 2) Логика клетки
        unit.BindToCell(target, snap: false);

        // 3) Анимация
        yield return AnimateUnitTo(unit, target.transform.position);

        // 4) Превращение пешки
        int lastRank = unit.Team == Team.White ? 7 : 0;
        if (unit.Type == ChessPieceType.Pawn && unit.Cell != null && unit.Cell.Y == lastRank)
        {
            ChessPieceType chosen = ChessPieceType.Queen;

            if (_promotionUI != null)
            {
                yield return _promotionUI.WaitForSelection(unit.Team, type => chosen = type);
            }
            else
            {
                Debug.LogWarning("[PlayerController] IPromotionUI missing — auto Queen");
            }

            if (!IsPromotable(chosen))
                chosen = ChessPieceType.Queen;

            unit.PromoteTo(chosen);
            Debug.Log($"[Chess] {unit.Team} pawn promoted to {chosen}");
        }

        unit.HasMoved = true;

        // 5) En passant state: clear + set if double pawn step
        RegisterEnPassantAfterMove(unit, fromX, fromY, target);

        IsBusy = false;
        onCompleted?.Invoke();
    }

    private IEnumerator CastleRoutine(
        Unit king,
        Cell kingTo,
        Unit rook,
        Cell rookTo,
        Action onCompleted)
    {
        IsBusy = true;

        Debug.Log(
            $"[Chess] Castle {king.Team}: King→({kingTo.X},{kingTo.Y}), Rook→({rookTo.X},{rookTo.Y})");

        king.BindToCell(kingTo, snap: false);
        rook.BindToCell(rookTo, snap: false);

        bool kingDone = false;
        bool rookDone = false;

        StartCoroutine(AnimateUnitToThen(king, kingTo.transform.position, () => kingDone = true));
        StartCoroutine(AnimateUnitToThen(rook, rookTo.transform.position, () => rookDone = true));

        while (!kingDone || !rookDone)
            yield return null;

        king.HasMoved = true;
        rook.HasMoved = true;

        _enPassant?.Clear();

        IsBusy = false;
        onCompleted?.Invoke();
    }

    private IEnumerator EnPassantRoutine(
        Unit attacker,
        Cell landing,
        Unit victim,
        Action onCompleted)
    {
        IsBusy = true;

        Debug.Log(
            $"[Chess] En passant: {attacker.Team} → ({landing.X},{landing.Y}), " +
            $"captures {victim.Team} pawn");

        // Жертва не на landing
        victim.BindToCell(null, snap: false);
        Destroy(victim.gameObject);

        attacker.BindToCell(landing, snap: false);
        yield return AnimateUnitTo(attacker, landing.transform.position);

        attacker.HasMoved = true;
        _enPassant?.Clear();

        IsBusy = false;
        onCompleted?.Invoke();
    }

    private void RegisterEnPassantAfterMove(Unit unit, int fromX, int fromY, Cell to)
    {
        if (_enPassant == null)
            return;

        _enPassant.Clear();

        if (unit == null || to == null)
            return;
        if (unit.Type != ChessPieceType.Pawn)
            return;
        if (fromX < 0 || fromY < 0)
            return;

        // Double step: |Δy| == 2, same file
        if (fromX != to.X)
            return;
        if (Mathf.Abs(to.Y - fromY) != 2)
            return;

        int midY = (fromY + to.Y) / 2;
        var board = _board != null ? _board : UnityEngine.Object.FindObjectOfType<Battlefield>();
        var landing = board != null ? board.GetCell(to.X, midY) : null;
        if (landing == null)
            return;

        _enPassant.Set(unit, landing);
    }

    private IEnumerator AnimateUnitToThen(Unit unit, Vector3 worldTarget, Action onDone)
    {
        yield return AnimateUnitTo(unit, worldTarget);
        onDone?.Invoke();
    }

    private static bool IsPromotable(ChessPieceType type)
    {
        return type == ChessPieceType.Queen
               || type == ChessPieceType.Rook
               || type == ChessPieceType.Bishop
               || type == ChessPieceType.Knight;
    }

    private IEnumerator AnimateUnitTo(Unit unit, Vector3 worldTarget)
    {
        worldTarget.y = unit.transform.position.y;

        float speed = unit.MoveSpeed > 0f ? unit.MoveSpeed : 3f;
        if (_settings != null && _settings.unitMoveSpeed > 0f)
            speed = _settings.unitMoveSpeed;

        while ((unit.transform.position - worldTarget).sqrMagnitude > 0.0001f)
        {
            unit.transform.position = Vector3.MoveTowards(
                unit.transform.position,
                worldTarget,
                speed * Time.deltaTime);
            yield return null;
        }

        unit.transform.position = worldTarget;
    }
}
