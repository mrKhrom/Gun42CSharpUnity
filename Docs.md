# Документация проекта Warcraft 3 Chess

Учебный прототип пошаговой тактики: шахматы 8×8, hot-seat, визуал Warcraft III.  
Unity **2022.3.62f3**. Сцены в билде: `MainScene` (0) → `GameScene` (1, additive).

---

## Ключевые настройки проекта

| Параметр | Значение |
|---|---|
| Редактор | Unity 2022.3 LTS |
| Ввод | Unity Input System 1.14.2 (`Assets/Input/Controls.inputactions`) |
| UI-текст | TextMesh Pro 3.0.7 |
| DI | Extenject (Zenject) в `Assets/Plugins/Zenject` |
| Сцены билда | `Assets/Scenes/MainScene.unity`, `Assets/Scenes/GameScene.unity` |
| Партия | `GameSettings`: первый ход **White**, скорость фигур 3 |

**Input (игровой):**

- `Cancel` — Escape, сброс выбора фигуры  
- `Confirm` — Space  
- `Select` — ЛКМ по клетке (через `IPointerClickHandler`, не binding клавиатуры)  
- `Restart` — удержание R  
- Отмена хода **Ctrl+Z** — отдельный `InputAction` в `BattleController`

**Настройки партии:** ScriptableObject `Assets/ScriptableObjects/GameSettings.asset` (класс `Scripts/Settings/GameSettings.cs`).

---

## Структура `Assets` (кроме Scripts)

```
Assets/
├── Audio/              звуки персонажей и UI/музыка
├── Fonts/              шрифт Varnyx + TMP Font Asset
├── Input/              Input Action Asset и сгенерированный Controls.cs
├── Materials/          материалы клеток и подсветки хода
├── Models/             3D: фигуры, окружение, оружие, FX
├── Plugins/            Zenject и In-game Debug Console
├── Prefabs/            префабы фигур и клетки
├── Scenes/             MainScene, GameScene, SampleScene
├── ScriptableObjects/  GameSettings.asset
├── Scripts/            игровой код (см. ниже)
├── TextMesh Pro/       пакет TMP (шрифты, шейдеры, примеры)
└── UI/                 спрайты кнопки Play, полоса рестарта, видео меню
```

---

## `Assets/Scripts` 

Поток партии:

`клетка (клик)` → `Battlefield` → `BattleController` → `ChessCommand.Interact` → `PlayerController` (анимация) → смена хода / UI / камера.

### `Core/` — каркас сцен и DI

| Файл | Назначение |
|---|---|
| `Primitives.cs` | Enum: `Team`, `ChessPieceType`, `CellHighlight`, `NeighbourType` |
| `SceneInstaller.cs` | Zenject-установщик **GameScene**: бинды ввода, доски, команды, UI |
| `MainInstaller.cs` | Zenject-установщик **MainScene**: `SceneController` |
| `SceneController.cs` | Play → additive GameScene, клик кнопки, выключение камеры меню |
| `EventSystemGuard.cs` | Один `EventSystem` на две сцены |

### `Cell/` — доска

| Файл | Назначение |
|---|---|
| `Battlefield.cs` | Поле 8×8, граф соседей, подсветка, привязка `Unit` ↔ `Cell` |
| `Cell.cs` | Клетка: pointer enter/exit/click, `Cell.Unit` |

### `Controllers/` — ввод, визуал хода, расстановка

| Файл | Назначение |
|---|---|
| `BattleController.cs` | Клавиатура + клики доски → команда; Ctrl+Z |
| `PlayerController.cs` | `IsBusy` на время анимации; walk / capture / castle / en passant |
| `GameBootstrap.cs` | Старт партии: инициализация доски, первый ход White |
| `ChessSetup.cs` | Префабы фигур, опциональный spawn, источник модели для превращения |
| `TurnCameraController.cs` | Камера на сторону текущего игрока после хода |

### `Controllers/Commands/`

| Файл | Назначение |
|---|---|
| `IGameplayCommand.cs` | `Interact` / `Cancel` / `Confirm` |
| `ChessCommand.cs` | Правила: выбор, легальные ходы, рокировка, EP, шах/мат, undo restore |
| `ICheatCommands.cs` | NextTurn / Kill / Undo для editor-читов |
| `CheatCommandStack.cs` | Стек снимков доски (до 16) |

### `Units/` — фигуры и правила ходов

| Файл | Назначение |
|---|---|
| `Unit.cs` | Тип, команда, клетка, превращение пешки |
| `UnitAnimationDriver.cs` | Idle / Walk / Attack / Death |
| `UnitAudio.cs` | Select / ход / атака / смерть |
| `ChessMoveGenerator.cs` | Псевдолегальные ходы всех фигур, рокировка, EP |
| `ChessLegality.cs` | Отсев ходов, оставляющих короля под шахом |
| `ChessMove.cs` | Описание хода (в т.ч. рокировка и EP) |
| `EnPassantState.cs` | Состояние «взятие на проходе» на текущий ход |

### `Interfaces/` — игровой UI

| Файл | Назначение |
|---|---|
| `ITurnInfoView.cs` / `TurnInfoView.cs` | «Ход: White», шах, мат, пат |
| `IPromotionUI.cs` / `PromotionPanel.cs` | Выбор фигуры при превращении пешки |

### `Settings/`

`GameSettings.cs` — скорость фигур, первый игрок, длительность Restart.

### `Audio/`

`SceneLoopAudio.cs` — музыка сцены: один трек loop или очередь с shuffle.

### `Managers/`

| Файл | Назначение |
|---|---|
| `InputManager.cs` | Удержание R → перезагрузка сцены |
| `CellManager.cs` | Отладочный лог кликов (логика хода идёт через BattleController) |

### `Editor/` — только редактор, в билд не попадает

| Файл | Назначение |
|---|---|
| `Cheats/EditorCheatWindow.cs` | Меню Netologia: читы 1 / 2 в Play Mode |
| `Cheats/EditorControls.*` | Input Action Asset читов |
| `MdxFbxPostprocessor.cs` | Импорт FBX из MDX Warcraft III |
| `SetupChessSetupPrefabs.cs` | Заполнить слоты ChessSetup |
| `SetupRoyalAnimators.cs` | Animator короля/ферзя |
| `AddUnitAudioToPrefabs.cs` | Навесить `UnitAudio` на префабы |

---

## Zenject (Extenject)

Контейнер собирает зависимости при старте сцены. Объекты не ищут друг друга через `FindObjectOfType` в геймплее (кроме редких fallback).
Проще говоря: Zenject сам создаёт нужные объекты (или берёт их со сцены) и раздаёт ссылки скриптам, чтобы они не искали друг друга вручную.

Цепочка:
GameScene загрузилась → на объекте сработал SceneContext → он вызвал SceneInstaller → там забиндили экземпляры → в скрипте [Inject] просит тип (часто интерфейс, но можно и объект) → Zenject подставляет этот экземпляр → через него вызываете методы.

### Где живёт

- **MainScene** — компонент `SceneContext` + `MainInstaller`.  
- **GameScene** — `SceneContext` + `SceneInstaller` (обязательное имя по ТЗ).

`MonoInstaller.InstallBindings()` вызывается Zenject сам.

### Регистрация (bind)

В `SceneInstaller` сервисы кладутся в контейнер:

```csharp
// Один экземпляр на всю сцену
Container.Bind<EnPassantState>().AsSingle();

// Уже существующий объект со сцены
BindFromHierarchy<Battlefield>();
BindFromHierarchy<PlayerController>();

// Класс + все его интерфейсы (IGameplayCommand, ICheatCommands)
Container.BindInterfacesAndSelfTo<ChessCommand>().AsSingle();

// Интерфейс UI → конкретная реализация
Container.Bind<ITurnInfoView>().FromInstance(turnView).AsSingle();
```

`BindFromHierarchy<T>()` внутри — `FindObjectOfType<T>` **один раз при установке**, затем контейнер отдаёт тот же instance.

В MainScene проще:

```csharp
public class MainInstaller : MonoInstaller
{
    [SerializeField] private SceneController _sceneController;

    public override void InstallBindings()
    {
        Container.Bind<SceneController>()
            .FromInstance(_sceneController)
            .AsSingle();
    }
}
```

### Получение (inject)

На MonoBehaviour контейнер вызывает метод с `[Inject]` после `Awake` сцены:

```csharp
public class BattleController : MonoBehaviour
{
    private IGameplayCommand _command;
    private Battlefield _board;

    [Inject]
    private void Construct(
        IGameplayCommand command,
        Battlefield board,
        Controls.GameActions gameActions,
        [InjectOptional] ICheatCommands cheats)
    {
        _command = command;
        _board = board;
        // ...
    }
}
```

`ChessCommand` не MonoBehaviour: его создаёт контейнер через конструктор:

```csharp
public ChessCommand(
    Battlefield board,
    PlayerController player,
    CheatCommandStack cheats,
    [InjectOptional] ITurnInfoView turnView,
    ...)
{
    _board = board;
    _player = player;
    // ...
}
```

Цепочка: клик клетки → `BattleController` (уже с `_command`) → `_command.Interact(cell)` без поиска объектов на сцене.

`[InjectOptional]` — зависимость может отсутствовать (нет настроек, нет UI), игра не падает.

### Undo из Editor

`EditorCheatWindow` не на сцене. В Play Mode он берёт сервис из живого контейнера:

```csharp
var ctx = Object.FindObjectOfType<SceneContext>();
var cheats = ctx.Container.TryResolve<ICheatCommands>();
cheats.CheatUndo();
```

---

## In-game Debug Console

Плагин [yasirkula / UnityIngameDebugConsole](https://github.com/yasirkula/UnityIngameDebugConsole) (v1.8.9).  
Префаб `IngameDebugConsole` стоит на **GameScene**.

**Зачем:** в **билде** нет окна Console редактора. ТЗ требует, чтобы проверяющий видел логи (`Debug.Log`, Warning, Error) в запущенной сборке.

**Как пользоваться в билде:** маленькая кнопка/попап на экране открывает список логов, фильтр Info/Warning/Error, прокрутка. Игре не мешает: это оверлей UI, не часть шахматной логики.

Код плагина в `Assets/Plugins/IngameDebugConsole/` в геймплей не вплетён: достаточно префаба на сцене.
