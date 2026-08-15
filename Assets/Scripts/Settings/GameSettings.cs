using UnityEngine;

/// <summary>
/// Этап 13: общие настройки прототипа (ScriptableObject).
/// </summary>
[CreateAssetMenu(
    fileName = "GameSettings",
    menuName = "Chess/Game Settings")]
public class GameSettings : ScriptableObject
{
    [Header("Доска")]
    [Tooltip("Шаг сетки (должен совпадать с расстановкой клеток)")]
    public float cellSize = 1f;

    [Header("Фигуры")]
    [Tooltip("Скорость перемещения Unit (юниты/сек)")]
    public float unitMoveSpeed = 3f;

    [Header("Партия")]
    [Tooltip("Если true — первый ход White/Black случайно, иначе firstTeam")]
    public bool randomFirstPlayer = false;

    public Team firstTeam = Team.White;

    [Header("Restart (hold)")]
    [Tooltip("Секунды удержания Restart до перезагрузки сцены")]
    public float restartHoldDuration = 1.5f;

    [Header("UI")]
    [Tooltip("Формат строки хода. {0} = White/Black")]
    public string turnLabelFormat = "Ход: {0}";
}
