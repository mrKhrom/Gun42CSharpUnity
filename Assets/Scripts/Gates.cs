using Netologia.Homework;
using UnityEngine;

namespace Homework.Netologia
{
    public class Gates: MonoBehaviour
    {
        private int _score;

        private void OnTriggerEnter(Collider other)
        {
            // Ищем скрипт Ball на объекте, который вошел в ворота
            Ball ball = other.GetComponent<Ball>();

            if (ball == null)
                return;

            _score++;
            Debug.Log($"Счёт: {_score} ");

            Destroy(ball.gameObject);
        }
    }
}