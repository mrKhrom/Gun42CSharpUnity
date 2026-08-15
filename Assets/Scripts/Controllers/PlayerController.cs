using System;
using System.Collections;
using UnityEngine;
using Zenject;

public class PlayerController : MonoBehaviour
{
    private GameSettings _settings;

    public bool IsBusy { get; private set; }

    [Inject(Optional = true)]
    private void Construct(GameSettings settings)
    {
        _settings = settings;
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
