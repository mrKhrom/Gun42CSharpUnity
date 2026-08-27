using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фоновая музыка сцены: один клип зацикливается, несколько — очередь вперемешку.
/// Методы: StartLoop — запустить плейлист; StopLoop — остановить музыку.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SceneLoopAudio : MonoBehaviour
{
    [SerializeField] private AudioClip[] _clips;

    [Tooltip("Перемешать очередь. После полного круга — новая перетасовка.")]
    [SerializeField] private bool _shuffle = true;

    [Tooltip("Не ставить первым в новой колоде тот же трек, что только что сыграл.")]
    [SerializeField] private bool _avoidImmediateRepeat = true;

    [SerializeField] private bool _playOnStart = true;

    [Tooltip("Остановить другие SceneLoopAudio (меню не должно звучать вместе с боем).")]
    [SerializeField] private bool _stopOthersOnStart = true;

    [SerializeField] [Range(0f, 1f)] private float _volume = 1f;

    AudioSource _source;
    readonly List<AudioClip> _queue = new();
    int _queueIndex;
    AudioClip _lastPlayed;
    Coroutine _loop;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;
        _source.volume = _volume;
    }

    void OnEnable()
    {
        if (_playOnStart && Application.isPlaying)
            StartLoop();
    }

    void OnDisable()
    {
        StopLoop();
    }

    public void StartLoop()
    {
        if (_stopOthersOnStart)
            StopOtherLoopers();

        StopLoop();
        RebuildQueue();
        if (_queue.Count == 0)
            return;

        _loop = StartCoroutine(PlayLoop());
    }

    public void StopLoop()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }

        if (_source != null && _source.isPlaying)
            _source.Stop();
    }

    IEnumerator PlayLoop()
    {
        while (enabled)
        {
            if (_queueIndex >= _queue.Count)
                RebuildQueue();

            if (_queue.Count == 0)
                yield break;

            var clip = _queue[_queueIndex++];
            if (clip == null)
                continue;

            _lastPlayed = clip;
            _source.clip = clip;
            _source.loop = _queue.Count == 1 && !_shuffle;
            _source.volume = _volume;
            _source.Play();

            if (_source.loop)
                yield break;

            yield return new WaitWhile(() => _source != null && _source.isPlaying);
        }
    }

    void RebuildQueue()
    {
        _queue.Clear();
        _queueIndex = 0;

        if (_clips == null)
            return;

        foreach (var clip in _clips)
        {
            if (clip != null)
                _queue.Add(clip);
        }

        if (_queue.Count <= 1)
            return;

        if (_shuffle)
            Shuffle(_queue);

        if (_avoidImmediateRepeat && _lastPlayed != null && _queue.Count > 1 && _queue[0] == _lastPlayed)
        {
            int swap = Random.Range(1, _queue.Count);
            var tmp = _queue[0];
            _queue[0] = _queue[swap];
            _queue[swap] = tmp;
        }
    }

    static void Shuffle(List<AudioClip> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    void StopOtherLoopers()
    {
        var others = FindObjectsOfType<SceneLoopAudio>();
        foreach (var other in others)
        {
            if (other != null && other != this)
                other.StopLoop();
        }
    }
}
