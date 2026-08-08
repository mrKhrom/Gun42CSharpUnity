using UnityEngine;

/// <summary>
/// Хранит 4 клипа персонажа (Idle/Walk/Attack/Dead) и даёт простые вызовы.
/// Клипы также подключены в Animator Controller на prefab.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class CharacterAnimationPlayer : MonoBehaviour
{
    [Header("Clips (заполняется Tools → Setup Silvana + Jaina)")]
    public AnimationClip idle;
    public AnimationClip walk;
    public AnimationClip attack;
    public AnimationClip dead;

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    /// <summary>0 = idle, &gt;0 = walk.</summary>
    public void SetSpeed(float speed)
    {
        if (_animator != null)
            _animator.SetFloat("Speed", speed);
    }

    public void PlayAttack()
    {
        if (_animator != null)
            _animator.SetTrigger("Attack");
    }

    public void PlayDeath()
    {
        if (_animator != null)
            _animator.SetTrigger("Die");
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Log Assigned Clips")]
    private void DebugLogClips()
    {
        Debug.Log(
            $"{name} clips:\n" +
            $"  Idle={(idle != null ? idle.name : "NULL")}\n" +
            $"  Walk={(walk != null ? walk.name : "NULL")}\n" +
            $"  Attack={(attack != null ? attack.name : "NULL")}\n" +
            $"  Dead={(dead != null ? dead.name : "NULL")}\n" +
            $"  Controller={(_animator != null && _animator.runtimeAnimatorController != null ? _animator.runtimeAnimatorController.name : "NULL")}",
            this);
    }
#endif
}
