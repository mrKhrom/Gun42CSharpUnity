using UnityEngine;

/// <summary>
/// Flying arrow instance. Spawned by ArcheryWeapon at release; not parented to the hand.
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    [SerializeField] private float _defaultSpeed = 12f;
    [SerializeField] private float _defaultMaxDistance = 5f;
    [SerializeField] private float _lifetimeFallback = 8f;

    private Vector3 _direction;
    private float _speed;
    private float _maxDistance;
    private Vector3 _start;
    private bool _launched;
    private float _spawnTime;

    public void Launch(Vector3 direction, float speed, float maxDistance)
    {
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        _speed = speed > 0f ? speed : _defaultSpeed;
        _maxDistance = maxDistance > 0f ? maxDistance : _defaultMaxDistance;
        _start = transform.position;
        _launched = true;
        _spawnTime = Time.time;

        if (_direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
    }

    private void Update()
    {
        if (!_launched) return;

        transform.position += _direction * (_speed * Time.deltaTime);

        if (Vector3.Distance(_start, transform.position) >= _maxDistance
            || Time.time - _spawnTime >= _lifetimeFallback)
        {
            Destroy(gameObject);
        }
    }
}
