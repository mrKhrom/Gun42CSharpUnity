using System;
using UnityEngine;

namespace DefaultNamespace
{
	[RequireComponent(typeof(PositionSaver))]
	public class ReplayMover : MonoBehaviour
	{
		private PositionSaver _save;

		private int _index;
		private PositionSaver.Data _prev;
		private float _duration;

		private void Start()
		{
			////todo comment: зачем нужны эти проверки?
			//answer: проверяем компонет на существоание, чтобы не получить ошибку при обращении к пустому объекту.
			// Проверяем количество записей, чтобы не получить ошибку при обращении к пустому списку.
			if (!TryGetComponent(out _save) || _save.Records.Count == 0)
			{
				Debug.LogError("Records incorrect value", this);
				//todo comment: Для чего выключается этот компонент?
				//answer: компонент выключается, чтобы не вызывать Update и не получать ошибку при обращении к пустому списку.
				enabled = false;
			}
		}

		private void Update()
		{
			var curr = _save.Records[_index];
			//todo comment: Что проверяет это условие (с какой целью)?
			//answer: условие проверяет, прошло ли время, чтобы перейти к следующей позиции в списке. Если текущее время больше времени записи, то мы переходим к следующей позиции.
			if (Time.time > curr.Time)
			{
				_prev = curr;
				_index++;
				//todo comment: Для чего нужна эта проверка?
				//answer: проверка нужна, чтобы не выйти за пределы списка и не получить ошибку при обращении к пустому списку.
				if (_index >= _save.Records.Count)
				{
					enabled = false;
					Debug.Log($"<b>{name}</b> finished", this);
				}
			}
			//todo comment: Для чего производятся эти вычисления (как в дальнейшем они применяются)?
			//answer: вычисляется дельта времени между текущей и предыдущей позицией, чтобы определить, насколько далеко мы должны переместиться между этими двумя позициями. 
			// В дальнейшем это  используется для плавного перемещения объекта между этими позициями с помощью Lerp.
			var delta = (Time.time - _prev.Time) / (curr.Time - _prev.Time);
			//todo comment: Зачем нужна эта проверка?
			//answer: проверка нужна, чтобы избежать ошибки при делении на ноль, если время между текущей и предыдущей позицией равно нулю. 
			// В этом случае дельта будет NaN, и мы устанавливаем её в 0, чтобы объект оставался на предыдущей позиции.
			if (float.IsNaN(delta)) delta = 0f;
			//todo comment: Опишите, что происходит в этой строчке так подробно, насколько это возможно
			//answer: в этой строчке происходит плавное перемещение объекта между предыдущей и текущей позицией с помощью линейной интерполяции (Lerp).
			// Линейная интерполяция вычисляет позицию объекта между двумя точками (предыдущей и текущей) на основе значения delta, которое 
			// представляет собой процентное соотношение времени, прошедшего между этими двумя точками.
			transform.position = Vector3.Lerp(_prev.Position, curr.Position, delta);
		}
	}
}