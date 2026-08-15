using System;
using System.Collections;
using UnityEngine;
using Zenject;

public class PlayerController : MonoBehaviour
{
    private GameSettings _settings;
    private IPromotionUI _promotionUI;

    public bool IsBusy { get; private set; }

    // Optional только на параметре: [Inject(Optional=true)] на методе ломает Zenject Install.
    [Inject]
    private void Construct(
        [InjectOptional] GameSettings settings,
        [InjectOptional] IPromotionUI promotionUI)
    {
        _settings = settings;
        _promotionUI = promotionUI;
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

    private IEnumerator MoveRoutine(Unit unit, Cell target, Action onCompleted)
    {
        IsBusy = true;

        // 1) Взятие
        if (target.Unit != null && target.Unit != unit)
        {
            var enemy = target.Unit;
            enemy.BindToCell(null, snap: false);
            Destroy(enemy.gameObject);
        }

        // 2) Логика клетки сразу (генератор ходов видит новую позицию)
        unit.BindToCell(target, snap: false);

        // 3) Анимация
        yield return AnimateUnitTo(unit, target.transform.position);

        // 4) Превращение пешки (ТЗ необяз. п.6 — UI; fallback: авто-Queen)
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

        // Логика клеток сразу
        king.BindToCell(kingTo, snap: false);
        rook.BindToCell(rookTo, snap: false);

        // Параллельная анимация обеих фигур
        bool kingDone = false;
        bool rookDone = false;

        StartCoroutine(AnimateUnitToThen(king, kingTo.transform.position, () => kingDone = true));
        StartCoroutine(AnimateUnitToThen(rook, rookTo.transform.position, () => rookDone = true));

        while (!kingDone || !rookDone)
            yield return null;

        king.HasMoved = true;
        rook.HasMoved = true;

        IsBusy = false;
        onCompleted?.Invoke();
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
