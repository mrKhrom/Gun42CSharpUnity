using System.Collections;
using UnityEngine;

namespace Netologia.Homework
{
    [RequireComponent(typeof(Rigidbody))]

    public class Rotator : MonoBehaviour
    {
        [SerializeField] private Vector3 _rotate;
        private readonly WaitForFixedUpdate _fixedUpdateWait = new WaitForFixedUpdate();

        private IEnumerator Start()
        {
            Rigidbody body = GetComponent<Rigidbody>();

            while (true)
            {
                body.MoveRotation(body.rotation * Quaternion.Euler(_rotate * Time.fixedDeltaTime));

                yield return _fixedUpdateWait;
            }
        }
    }

}