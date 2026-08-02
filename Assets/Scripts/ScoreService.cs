using UnityEngine;

// Простой сервис — "суть" того, что мы раздаём через DI.
public class ScoreService
{
    private int _score;

    public int Score => _score;

    public void Add(int value)
    {
        _score += value;
        Debug.Log($"[ScoreService] Очки: {_score}");
    }
}