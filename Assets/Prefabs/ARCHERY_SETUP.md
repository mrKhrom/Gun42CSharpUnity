# Archery shot setup (BlackQween / Silvana)

## Implemented in project

| Item | Path |
|------|------|
| ArcheryWeapon | `Assets/Scripts/Unit/ArcheryWeapon.cs` |
| ArrowProjectile | `Assets/Scripts/Unit/ArrowProjectile.cs` |
| ArcheryShotArrowBehaviour (SMB) | `Assets/Scripts/Animation/ArcheryShotArrowBehaviour.cs` |
| ArcheryWeaponProxy (for Animation Events) | `Assets/Scripts/Unit/ArcheryWeaponProxy.cs` |
| Projectile prefab | `Assets/Prefabs/ArrowProjectile.prefab` |
| BlackQween | ArrowSocket + Arrow (inactive) under RightHand + ArcheryWeapon |
| Sylvana.controller | Trigger `Attack`, Idle↔Archery transitions, SMB release @ 0.55 |

## Hierarchy (BlackQween)

```
BlackQween (Unit, ArcheryWeapon, CapsuleCollider)
└── Sylvana (Animator → Sylvana.controller)
    └── … → RightHand (bone)
            └── ArrowSocket
                └── Arrow (in-hand mesh, default inactive)
    └── … → (other hand) BowElf
```

## How it works

1. `PlayArcheryAttack()` / Trigger **Attack** → transition to **Archery_Shot_1**.
2. SMB `ArcheryShotArrowBehaviour` OnStateEnter → `OnArcheryStart()` (show Arrow).
3. At **normalizedTime ≥ 0.55** → `OnArrowRelease()` (hide Arrow, spawn projectile, fly).
4. OnStateExit → `OnArcheryEnd()`.
5. Exit time → back to **Idle_6**.

## Play Mode test

1. Open GameScene, ensure BlackQween instance exists.
2. On BlackQween → Unit: **Test Archery On Click** is enabled (or call `PlayArcheryAttack()`).
3. Enter Play Mode, click the unit (with Physics Raycaster + EventSystem).
4. Or select child Animator → set Trigger Attack in Animator window.

## Manual tuning checklist

- [ ] **Release timing**: select state Archery_Shot_1 → behaviour Archery Shot Arrow → `releaseNormalizedTime` (0.45–0.7).
- [ ] **Flight direction**: ArcheryWeapon → `Use Socket Forward` on/off; rotate ArrowSocket if arrow flies sideways.
- [ ] **Speed / distance**: ArcheryWeapon `_speed`, `_maxDistance`.
- [ ] **In-hand grip**: tweak Arrow local TRS under ArrowSocket.
- [ ] Optional Animation Events on a **duplicate** `.anim` of Archery_Shot_1 calling `ArcheryWeaponProxy.OnArrowRelease` (SMB already covers release).
- [ ] If clips Missing after FBX replace: Tools → Sylvana → Repair Controller Clips Now.

## Optional Animation Events path

1. Duplicate clip Archery_Shot_1 from Sylvana.fbx → `Assets/Models/Orcs/Sylvana/Archery_Shot_1.anim`.
2. Assign to controller state Motion.
3. Add events: OnArcheryStart / OnArrowRelease / OnArcheryEnd.
4. Add `ArcheryWeaponProxy` on the GameObject that has the Animator (Sylvana child).

Script guids (for reference):
- ArcheryWeapon: 59a0d58f30564ae89911a6df71b02e8f
- ArrowProjectile: ee1c6e71d78e4b9c83d56d2fed687386
- SMB: d609786880204a5d9a0b9854467d1341
- Prefab: beb8889e7bde416abd3534ecf55e2838
