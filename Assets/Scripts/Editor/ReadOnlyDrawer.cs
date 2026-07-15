using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 1. Запоминаем, был ли GUI уже выключен
        bool previousEnabled = GUI.enabled;

        // 2. Запрещаем редактирование
        GUI.enabled = false;

        // 3. Рисуем поле (значение видно, менять нельзя)
        EditorGUI.PropertyField(position, property, label, true);

        // 4. Восстанавливаем состояние GUI
        GUI.enabled = previousEnabled;
    }
}