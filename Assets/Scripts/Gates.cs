using UnityEngine;

namespace Netologia.Homework
{
    public class Gates: MonoBehaviour
    {
        private int _score;

        private void OnTriggerEnter(Collider other)
        {
            // Ищем скрипт Ball на объекте, который вошел в ворота
            other.TryGetComponent<Ball>(out var ball);

            if (ball == null)
                return;

            _score++;
            Debug.Log($"Счёт: {_score} ");

            Destroy(ball.gameObject);
        }
    }
}