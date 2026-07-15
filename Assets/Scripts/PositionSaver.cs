using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DefaultNamespace
{
	public class PositionSaver : MonoBehaviour
	{
		[Serializable]
		public struct Data
		{
			public Vector3 Position;
			public float Time;
		}

		[SerializeField]
		[ReadOnly]
		[Tooltip("Для заполнения этого поля используйте контекстное меню компонента → Create File")]
		private TextAsset _json;

		[field: SerializeField]
		[field: HideInInspector]
		public List<Data> Records { get; private set; }

		private void Awake()
		{
			//todo comment: Что будет, если в теле этого условия не сделать выход из метода?
			//answer: если не выйти из этого метода, при отсутсвии данных json мы всё ранво будем пытаться десериализовать null, что приведёт к ошибке.
			if (_json == null)
			{
				gameObject.SetActive(false);
				Debug.LogError("Please, create TextAsset and add in field _json");
				return;
			}
			
			JsonUtility.FromJsonOverwrite(_json.text, this);
			//todo comment: Для чего нужна эта проверка (что она позволяет избежать)?
			//answer: Records проверяются на null, чтобы избежать ошибки при первом создании списка с данными.
			if (Records == null)
				Records = new List<Data>(10);
		}

		private void OnDrawGizmos()
		{
			//todo comment: Зачем нужны эти проверки (что они позволляют избежать)?
			//answer: проверки позволяют избежать ошибки при отрисовке гизмо, если список null или просто не выполнять логику, когда список пуст.
			if (Records == null || Records.Count == 0) return;
			var data = Records;
			var prev = data[0].Position;
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(prev, 0.3f);
			//todo comment: Почему итерация начинается не с нулевого элемента?
			// нулевой элемент уже отрисован выше, он является исхдной точкой.
			for (int i = 1; i < data.Count; i++)
			{
				var curr = data[i].Position;
				Gizmos.DrawWireSphere(curr, 0.3f);
				Gizmos.DrawLine(prev, curr);
				prev = curr;
			}
		}
		
#if UNITY_EDITOR
		[ContextMenu("Create File")]
		private void CreateFile()
		{
			//todo comment: Что происходит в этой строке?
			//answer: создаётся новый файл Path.txt в папке Assets, если он уже существует, то он перезаписывается.
			var stream = File.Create(Path.Combine(Application.dataPath, "Path.txt"));
			//todo comment: Подумайте для чего нужна эта строка? (а потом проверьте догадку, закомментировав)
			//answer: эта строка закрывает поток, чтобы не было утечки памяти и чтобы файл был доступен для дальнейшей работы (например, для записи в него). 
			stream.Dispose();
			UnityEditor.AssetDatabase.Refresh();
			//В Unity можно искать объекты по их типу, для этого используется префикс "t:"
			//После нахождения, Юнити возвращает массив гуидов (которые в мета-файлах задаются, например).
			var guids = UnityEditor.AssetDatabase.FindAssets("t:TextAsset");
			foreach (var guid in guids)
			{
				//Этой командой можно получить путь к ассету через его гуид
				var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
				//Этой командой можно загрузить сам ассет
				var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				//todo comment: Для чего нужны эти проверки?
				//answer: проверки позволяют избежать ошибки при загрузке ассета, если он не найден или имеет неправильное имя.
				if(asset != null && asset.name == "Path")
				{
					_json = asset;
					UnityEditor.EditorUtility.SetDirty(this);
					UnityEditor.AssetDatabase.SaveAssets();
					UnityEditor.AssetDatabase.Refresh();
					//todo comment: Почему мы здесь выходим, а не продолжаем итерироваться?
					//answer: мы выходим, потому что нашли нужный ассет и дальнейшая итерация не имеет смысла.
					return;
				}
			}
		}


		//EditorMover пишет точки в Records в памяти
		//объекты уничтожаются и вызывается OnDestroy
		//список сохраняется в файл Path.txt
		//Awake читает файл и Records снова заполнен
		private void OnDestroy()
		{
			if (_json == null)
				return;

			string json = JsonUtility.ToJson(this, true);

			string assetPath = UnityEditor.AssetDatabase.GetAssetPath(_json);
			string fullPath = Path.Combine(
				Directory.GetParent(Application.dataPath)!.FullName,
				assetPath);

			File.WriteAllText(fullPath, json);

			UnityEditor.AssetDatabase.Refresh();
		}
#endif
	}
}