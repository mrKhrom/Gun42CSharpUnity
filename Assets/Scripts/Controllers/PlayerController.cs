using System;
using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public bool IsBusy { get; private set; }

    public void ExecuteMove(Unit unit, Cell target, Action onCompleted)
    {
        if (unit == null || target == null)
        {
            onCompleted?.Invoke();
            return;
        }

        StartCoroutine(MoveRoutine(unit, target, onCompleted));
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

        // 3) Анимация только здесь — не вызываем Unit.AnimateMoveTo
        //    (Unit.MoveRoutine / AnimateMoveTo private/public для других сценариев)
        yield return AnimateUnitTo(unit, target.transform.position);

        // 4) Превращение пешки (ТЗ п.5 — авто-ферзь)
        int lastRank = unit.Team == Team.White ? 7 : 0;
        if (unit.Type == ChessPieceType.Pawn && unit.Cell != null && unit.Cell.Y == lastRank)
        {
            unit.PromoteTo(ChessPieceType.Queen);
            Debug.Log($"[Chess] {unit.Team} pawn promoted to Queen");
        }

        unit.HasMoved = true;
        IsBusy = false;
        onCompleted?.Invoke();
    }

    /// <summary>Визуальный сдвиг фигуры. Не зависит от coroutine API на Unit.</summary>
    private static IEnumerator AnimateUnitTo(Unit unit, Vector3 worldTarget)
    {
        worldTarget.y = unit.transform.position.y;
        float speed = unit.MoveSpeed > 0f ? unit.MoveSpeed : 3f;

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
