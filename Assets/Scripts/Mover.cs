using System.Collections;
using UnityEngine;

namespace Netologia.Homework
{
	/// <summary>
	/// Двигает кинематический Rigidbody между _start и _end
	/// </summary>
	[RequireComponent(typeof(Rigidbody))]
	public class Mover : MonoBehaviour
	{
		[SerializeField] private Vector3 _start;
		[SerializeField] private Vector3 _end;
		[SerializeField, Min(0.01f)] private float _speed = 5f;
		[SerializeField, Min(0f)] private float _delay = 1f;

		private Rigidbody _body;
		// Мировая матрица объекта в момент старта — для точек без родителя.</summary>
		private Matrix4x4 _initialLocalToWorld;

		private IEnumerator Start()
		{
			_body = GetComponent<Rigidbody>();
			_initialLocalToWorld = transform.localToWorldMatrix;

			while (true)
			{
				yield return Move(_start, _end);
				yield return new WaitForSeconds(_delay);

				yield return Move(_end, _start);
				yield return new WaitForSeconds(_delay);
			}
		}

		private IEnumerator Move(Vector3 fromLocal, Vector3 toLocal)
		{
			Vector3 from = ToWorld(fromLocal);
			Vector3 to = ToWorld(toLocal);

			_body.MovePosition(from);

			float distance = Vector3.Distance(from, to);
			if (distance < 0.001f)
				yield break;

			float duration = distance / _speed;
			float time = 0f;

			while (time < duration)
			{
				time += Time.fixedDeltaTime;
				float t = Mathf.Clamp01(time / duration);
				_body.MovePosition(Vector3.Lerp(from, to, t));
				yield return new WaitForFixedUpdate();
			}

			_body.MovePosition(to);
		}

		private Vector3 ToWorld(Vector3 local)
		{
			if (transform.parent != null)
				return transform.parent.TransformPoint(local);

			// Edit Mode (Gizmos) — от текущей позы; Play — от зафиксированной на Start
			if (Application.isPlaying)
				return _initialLocalToWorld.MultiplyPoint3x4(local);

			return transform.localToWorldMatrix.MultiplyPoint3x4(local);
		}

		private void OnDrawGizmos()
		{
			Vector3 a = ToWorld(_start);
			Vector3 b = ToWorld(_end);

			Gizmos.color = Color.green;
			Gizmos.DrawSphere(a, 0.3f);

			Gizmos.color = Color.red;
			Gizmos.DrawSphere(b, 0.3f);

			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(a, b);
		}
	}
}
