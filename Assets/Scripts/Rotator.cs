using System.Collections;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField] private Vector3 _rotate;
    
    private IEnumerator Start()
    {
        Rigidbody body = GetComponent<Rigidbody>();

        while (true)
        {
            body.MoveRotation(body.rotation * Quaternion.Euler(_rotate * Time.fixedDeltaTime));

            yield return new WaitForFixedUpdate();
        }
    }
}
