using UnityEngine;

/// <summary>
/// Палитра материалов для подсветки клеток (Cell.SetSelect).
/// </summary>
[CreateAssetMenu(
    fileName = "CellPaletteSettings",
    menuName = "Game/Cell Palette Settings")]
public class CellPaletteSettings : ScriptableObject
{
    [Header("Материалы для Cell.SetSelect")]
    [Tooltip("Клетка выбрана")]
    public Material Selected;

    [Tooltip("На клетку можно пойти или ударить")]
    public Material MoveOrAttack;

    [Tooltip("На клетку можно и пойти, и ударить")]
    public Material MoveAndAttack;
}