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

        int fromX = unit.Cell != null ? unit.Cell.X : -1;
        int fromY = unit.Cell != null ? unit.Cell.Y : -1;
        Cell fromCell = unit.Cell;

        var anim = unit.GetComponent<UnitAnimationDriver>();
        Unit enemy = target.Unit != null && target.Unit != unit ? target.Unit : null;

        if (enemy != null)
            yield return CaptureSequence(unit, fromCell, target, enemy, anim);
        else
            yield return QuietMoveSequence(unit, target, anim);

        // Логика клетки (враг уже снят / уничтожен)
        unit.BindToCell(target, snap: false);

        // Превращение пешки
        int lastRank = unit.Team == Team.White ? 7 : 0;
        if (unit.Type == ChessPieceType.Pawn && unit.Cell != null && unit.Cell.Y == lastRank)
        {
            ChessPieceType chosen = ChessPieceType.Queen;

            if (_promotionUI != null)
                yield return _promotionUI.WaitForSelection(unit.Team, type => chosen = type);
            else
                Debug.LogWarning("[PlayerController] IPromotionUI missing — auto Queen");

            if (!IsPromotable(chosen))
                chosen = ChessPieceType.Queen;

            // PromoteTo может заменить GO — берём возвращённый Unit
            var promoted = unit.PromoteTo(chosen);
            if (promoted != null)
                unit = promoted;

            Debug.Log($"[Chess] {unit.Team} pawn promoted to {unit.Type}");
        }

        if (unit != null)
            unit.HasMoved = true;
        RegisterEnPassantAfterMove(unit, fromX, fromY, target);

        IsBusy = false;
        onCompleted?.Invoke();
    }

    // Тихий ход: MoveStart SFX → Face → Walk → клетка → Idle
    private IEnumerator QuietMoveSequence(Unit unit, Cell target, UnitAnimationDriver anim)
    {
        unit.GetComponent<UnitAudio>()?.PlayMoveStart();

        if (anim != null)
            yield return anim.FacePoint(target.transform.position);

        anim?.StartWalk();
        yield return AnimateUnitTo(unit, target.transform.position);
        anim?.StopWalkToIdle();
    }

    // Взятие:
    // AttackDeclare сразу → approach → Face → Attack SFX + anim → hit: Death SFX + anim → на клетку
    private IEnumerator CaptureSequence(
        Unit unit,
        Cell fromCell,
        Cell target,
        Unit enemy,
        UnitAnimationDriver anim)
    {
        var enemyAnim = enemy.GetComponent<UnitAnimationDriver>();
        var attackerAudio = unit.GetComponent<UnitAudio>();
        var victimAudio = enemy.GetComponent<UnitAudio>();
        var board = _board != null ? _board : FindObjectOfType<Battlefield>();

        // Сразу: «назначена атака» (не MoveStart)
        attackerAudio?.PlayAttackDeclare();

        Cell approach = GetApproachCell(board, fromCell, target);
        Vector3 approachPos = approach != null
            ? approach.transform.position
            : Vector3.Lerp(unit.transform.position, target.transform.position, 0.85f);

        bool needApproachWalk = approach == null
            || fromCell == null
            || approach != fromCell
            || (unit.transform.position - approachPos).sqrMagnitude > 0.01f;

        if (needApproachWalk)
        {
            if (anim != null)
                yield return anim.FacePoint(approachPos);

            anim?.StartWalk();
            yield return AnimateUnitTo(unit, approachPos);
            anim?.StopWalkToIdle();
        }

        if (anim != null)
            yield return anim.FacePoint(enemy.transform.position);

        bool deathStarted = false;
        bool deathDone = false;

        void StartDeath()
        {
            if (deathStarted || enemy == null)
                return;
            deathStarted = true;

            victimAudio?.PlayDeath();
            HideCapturedUnit(enemy, enemyAnim, () => deathDone = true);
        }

        // Стар анимации атаки + attack SFX
        attackerAudio?.PlayAttack();

        if (anim != null)
            yield return anim.PlayAttackAndWait(StartDeath);
        else
            StartDeath();

        while (!deathDone)
            yield return null;

        if (anim != null)
            yield return anim.FacePoint(target.transform.position);

        anim?.StartWalk();
        yield return AnimateUnitTo(unit, target.transform.position);
        anim?.StopWalkToIdle();
    }

    private static IEnumerator RunAndFlag(IEnumerator routine, Action onDone)
    {
        if (routine != null)
            yield return routine;
        onDone?.Invoke();
    }

    // Соседняя к target клетка «перед» жертвой (шаг назад к from).
    // Если already adjacent — fromCell. Конь — ближайшая соседняя к target к from.
    private static Cell GetApproachCell(Battlefield board, Cell from, Cell target)
    {
        if (from == null || target == null)
            return from;

        int dx = target.X - from.X;
        int dy = target.Y - from.Y;
        int adx = Mathf.Abs(dx);
        int ady = Mathf.Abs(dy);

        // Уже соседняя (в т.ч. король / пешка-взятие)
        if (adx <= 1 && ady <= 1 && (adx + ady) > 0)
            return from;

        // Конь: 2+1 — approach = клетка рядом с target ближе к from
        if ((adx == 2 && ady == 1) || (adx == 1 && ady == 2))
        {
            if (board == null)
                return from;

            Cell best = from;
            float bestDist = float.MaxValue;
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            {
                if (ox == 0 && oy == 0) continue;
                var c = board.GetCell(target.X + ox, target.Y + oy);
                if (c == null) continue;
                if (c.Unit != null && c != from) continue;
                float d = (c.X - from.X) * (c.X - from.X) + (c.Y - from.Y) * (c.Y - from.Y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }

            return best;
        }

        // Скольжение (ладья/слон/ферзь/пешка double не сюда): клетка на 1 шаг до target
        int sx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
        int sy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

        if (board != null)
        {
            var cell = board.GetCell(target.X - sx, target.Y - sy);
            if (cell != null)
                return cell;
        }

        return from;
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

        var kingAnim = king.GetComponent<UnitAnimationDriver>();
        var rookAnim = rook.GetComponent<UnitAnimationDriver>();

        // Рокировка: move start у короля и ладьи
        king.GetComponent<UnitAudio>()?.PlayMoveStart();
        rook.GetComponent<UnitAudio>()?.PlayMoveStart();

        king.BindToCell(kingTo, snap: false);
        rook.BindToCell(rookTo, snap: false);

        if (kingAnim != null)
            yield return kingAnim.FacePoint(kingTo.transform.position);
        if (rookAnim != null)
            yield return rookAnim.FacePoint(rookTo.transform.position);

        kingAnim?.StartWalk();
        rookAnim?.StartWalk();

        bool kingDone = false;
        bool rookDone = false;

        StartCoroutine(AnimateUnitToThen(king, kingTo.transform.position, () => kingDone = true));
        StartCoroutine(AnimateUnitToThen(rook, rookTo.transform.position, () => rookDone = true));

        while (!kingDone || !rookDone)
            yield return null;

        kingAnim?.StopWalkToIdle();
        rookAnim?.StopWalkToIdle();

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

        var anim = attacker.GetComponent<UnitAnimationDriver>();
        var attackerAudio = attacker.GetComponent<UnitAudio>();
        var victimAudio = victim.GetComponent<UnitAudio>();

        // Как capture: declare сразу, без MoveStart
        attackerAudio?.PlayAttackDeclare();

        if (anim != null)
            yield return anim.FacePoint(landing.transform.position);

        anim?.StartWalk();
        yield return AnimateUnitTo(attacker, landing.transform.position);
        anim?.StopWalkToIdle();

        if (anim != null)
            yield return anim.FacePoint(victim.transform.position);

        bool deathDone = false;
        void StartDeath()
        {
            victimAudio?.PlayDeath();
            var victimAnim = victim.GetComponent<UnitAnimationDriver>();
            HideCapturedUnit(victim, victimAnim, () => deathDone = true);
        }

        attackerAudio?.PlayAttack();

        if (anim != null)
            yield return anim.PlayAttackAndWait(StartDeath);
        else
            StartDeath();

        while (!deathDone)
            yield return null;

        attacker.BindToCell(landing, snap: false);
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

    // Не Destroy: undo должен вернуть фигуру. Death прячет объект.
    void HideCapturedUnit(Unit victim, UnitAnimationDriver victimAnim, Action onDone)
    {
        if (victim == null)
        {
            onDone?.Invoke();
            return;
        }

        victim.BindToCell(null, snap: false);
        var board = _board != null ? _board : FindObjectOfType<Battlefield>();
        board?.UnregisterUnit(victim);

        if (victimAnim != null && !victimAnim.IsDead)
        {
            StartCoroutine(RunAndFlag(
                victimAnim.PlayDeathSinkAndHide(destroyGameObject: false),
                onDone));
            return;
        }

        victim.gameObject.SetActive(false);
        onDone?.Invoke();
    }
}
