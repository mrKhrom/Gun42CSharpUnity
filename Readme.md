# Warcraft 3 Chess — прототип пошаговой тактики

**Учебный проект.** Не предназначен для коммерческого использования и распространения.

Вдохновлён игрой **Blizzard Warcraft III**: классические шахматы на поле 8×8 в эстетике Альянса и Орды, hot-seat (два игрока за одним компьютером).

---

## 1. Репозиторий и ветка

- Репозиторий: https://github.com/mrKhrom/Gun42CSharp.git  
- Ветка: `Tactics`

---

## 2. Какая команда реализована?

**Шахматы** (не шашки).

---

## 10.3. Какие паттерны проектирования использовались?

- **Command** — `IGameplayCommand` / `ChessCommand`: клик по клетке → выбор, ход, рокировка, взятие на проходе.  
- **Dependency Injection** — Zenject, `SceneInstaller`: доска, ввод, UI, команда.  
- **MVC (упрощённо)** — ввод (`BattleController`), логика (`ChessCommand`), визуал хода (`PlayerController`), UI (`TurnInfoView`, `PromotionPanel`).  
- **Observer** — клики клеток (`IPointer*`), Input System (`Cancel` / `Confirm`).  
- **Memento** — снимок доски и отмена хода (Ctrl+Z).  
- **Strategy (в генераторе ходов)** — разные правила фигур в `ChessMoveGenerator`.

---

## 4. Использованные ассеты

Сторонние материалы используются **только в учебных целях**, без коммерции.

| Источник | Что |
|---|---|
| [Nort3D (Patreon)](https://www.patreon.com/cw/Nort3D/home?utm_source=join_link&utm_medium=unknown&utm_campaign=creatorshare_creator&utm_content=copyLink) | Модели персонажей |
| [Hive Workshop](https://www.hiveworkshop.com) | Модели персонажей для модов Warcraft III |
| [Nature Starter Kit 2](https://assetstore.unity.com/packages/3d/environments/nature-starter-kit-2-52977) | Окружение (бесплатный Asset Store) |
| [In-game Debug Console](https://assetstore.unity.com/packages/tools/gui/in-game-debug-console-68068) | Консоль логов в билде (разрешённый код) |
| [Extenject](https://assetstore.unity.com/packages/tools/utilities/extenject-dependency-injection-ioc-157735) | DI / IoC (разрешённый код) |
| Warcraft III (Blizzard) | Визуальный и музыкальный референс; треки меню/боя — для учёбы, не для продажи |
| Unity TextMesh Pro | UI-текст (пакет Unity Registry) |
| Varnyx (demo, personal use) | Шрифт, только personal / учебное использование |

**Сделано самостоятельно:** игровая логика шахмат, скрипты сцены, расстановка, подсветка клеток, UI хода/превращения, настройки (`GameSettings`).

---

## 5. Необязательные правила шахмат

Реализованы **все четыре** необязательных пункта ТЗ:

1. **Превращение пешки** — панель выбора фигуры (ферзь / ладья / слон / конь), визуал меняется на префаб.  
2. **Рокировка** — по классике (король и ладья не ходили, путь свободен, нет шаха).  
3. **Взятие на проходе** — после хода пешки на две клетки.  
4. **Шах** — ходы, после которых свой король остаётся под шахом, недоступны.

Первый ход по ТЗ отдан **белым** (не случайный).

Дополнительно (сверх ТЗ): мат и пат с UI, отмена хода (Ctrl+Z), камера на сторону текущего игрока.
