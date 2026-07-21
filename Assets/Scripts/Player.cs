using System.Collections;
using UnityEngine;

namespace Netologia.Homework
{
	public class Player : MonoBehaviour
	{
		private bool _ready;
		private Rigidbody _ball;
		private Vector3 _prefabScale = Vector3.one;

		[SerializeField]
		private Rigidbody _ballPrefab;
		[SerializeField]
		private float _startVelocity;
		[SerializeField]
		private float _lifetime;
		[SerializeField]
		private float _respawnDelay;
		[SerializeField]
		private Vector3 _spawnLocalPosition = new Vector3(0f, 0.5f, 0.75f);

		private void Awake()
		{
			if (_ballPrefab != null)
				_prefabScale = _ballPrefab.transform.localScale;
		}

		private void Update()
		{
			if (!_ready) return;
			if (Input.GetKeyDown(KeyCode.Space))
			{
				StartCoroutine(Reloader());
				_ball.isKinematic = false;
				_ball.transform.SetParent(null, true);
				_ball.linearVelocity = transform.forward * _startVelocity;
				Destroy(_ball.gameObject, _lifetime);
			}
		}

		private IEnumerator Reloader()
		{
			_ready = false;
			yield return new WaitForSeconds(_respawnDelay);
			Spawn();
		}

		private void Spawn()
		{
			_ball = Instantiate(_ballPrefab, transform);
			_ball.isKinematic = true;
			_ball.linearVelocity = Vector3.zero;
			_ball.angularVelocity = Vector3.zero;

			Transform ballTransform = _ball.transform;
			ballTransform.localPosition = _spawnLocalPosition;
			ballTransform.localRotation = Quaternion.identity;

			Vector3 parentScale = transform.lossyScale;
			ballTransform.localScale = new Vector3(
				_prefabScale.x / parentScale.x,
				_prefabScale.y / parentScale.y,
				_prefabScale.z / parentScale.z);

			_ready = true;
		}

		private void Start()
		{
			Spawn();
		}
	}
}
