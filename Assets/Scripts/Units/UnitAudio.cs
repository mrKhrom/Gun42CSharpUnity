using UnityEngine;

// Аудио фигуры: select / move start / attack declare / attack / death.
// Клипы — массивы (random). Пустой массив = тишина.
// Death: one-shot на temporary AudioSource, чтобы sink/Destroy не обрезал звук.
[DisallowMultipleComponent]
[RequireComponent(typeof(Unit))]
public class UnitAudio : MonoBehaviour
{
    [Header("Select")]
    [SerializeField] private AudioClip[] _selectClips;

    [Header("Move")]
    [SerializeField] private AudioClip[] _moveStartClips;

    [Header("Attack intent")]
    [SerializeField] private AudioClip[] _attackDeclareClips;

    [Header("Attack")]
    [SerializeField] private AudioClip[] _attackClips;

    [Header("Death")]
    [SerializeField] private AudioClip[] _deathClips;

    [Header("Playback")]
    [SerializeField] private AudioSource _source;
    [SerializeField] [Range(0f, 1f)] private float _volume = 1f;
    [SerializeField] private bool _ignoreIfEmpty = true;
    [SerializeField] [Range(0f, 1f)] private float _spatialBlend;

    void Awake()
    {
        EnsureSource();
    }

    public void PlaySelect() => PlayRandom(_selectClips);
    public void PlayMoveStart() => PlayRandom(_moveStartClips);
    public void PlayAttackDeclare() => PlayRandom(_attackDeclareClips);
    public void PlayAttack() => PlayRandom(_attackClips);

    // Death на temporary emitter — не зависит от Destroy фигуры
    public void PlayDeath()
    {
        var clip = PickRandom(_deathClips);
        if (clip == null)
            return;

        PlayClipDetached(clip, transform.position);
    }

    void PlayRandom(AudioClip[] clips)
    {
        var clip = PickRandom(clips);
        if (clip == null)
            return;

        EnsureSource();
        if (_source == null)
            return;

        _source.PlayOneShot(clip, _volume);
    }

    AudioClip PickRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            if (!_ignoreIfEmpty)
                Debug.LogWarning($"[UnitAudio] {name}: пустой массив клипов");
            return null;
        }

        if (clips.Length == 1)
            return clips[0];

        // пропускаем null-слоты
        int guard = 0;
        AudioClip clip = null;
        while (guard++ < 8)
        {
            clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
                break;
        }

        return clip;
    }

    void EnsureSource()
    {
        if (_source != null)
            return;

        _source = GetComponent<AudioSource>();
        if (_source == null)
            _source = gameObject.AddComponent<AudioSource>();

        _source.playOnAwake = false;
        _source.spatialBlend = _spatialBlend;
        _source.volume = _volume;
    }

    // Отдельный GO: PlayOneShot + Destroy по длине клипа
    static void PlayClipDetached(AudioClip clip, Vector3 worldPos)
    {
        if (clip == null)
            return;

        var go = new GameObject("UnitDeathSFX");
        go.transform.position = worldPos;
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.PlayOneShot(clip, 1f);
        Object.Destroy(go, Mathf.Max(0.05f, clip.length) + 0.05f);
    }
}
