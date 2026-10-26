using UnityEngine;

/// <summary>
/// Plays an occasional one-shot idle animation while the character is standing still.
/// Movement and dialogue restart the delay so the animation never interrupts gameplay.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class SpacedIdleAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float minimumDelay = 10f;
    [SerializeField, Min(0f)] private float maximumDelay = 15f;
    [SerializeField] private string movementParameter = "horizontal";
    [SerializeField] private string idleTrigger = "playIdle";
    [SerializeField] private bool suppressDuringDialogue = true;

    private float timeUntilIdle;
    private int movementParameterHash;
    private int idleTriggerHash;
    private bool delayNeedsRestart = true;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        CacheParameterHashes();
    }

    private void OnEnable()
    {
        CacheParameterHashes();
        RestartDelay();
        delayNeedsRestart = true;
    }

    private void OnValidate()
    {
        if (maximumDelay < minimumDelay)
            maximumDelay = minimumDelay;
    }

    private void Update()
    {
        if (animator == null)
            return;

        bool movementBlocked = suppressDuringDialogue && DialogueController.IsDialogueActive;
        bool isMoving = animator.GetFloat(movementParameterHash) > 0.1f;

        if (movementBlocked || isMoving)
        {
            animator.ResetTrigger(idleTriggerHash);
            delayNeedsRestart = true;
            return;
        }

        if (delayNeedsRestart)
        {
            RestartDelay();
            delayNeedsRestart = false;
        }

        timeUntilIdle -= Time.deltaTime;
        if (timeUntilIdle > 0f)
            return;

        animator.SetTrigger(idleTriggerHash);
        RestartDelay();
    }

    private void CacheParameterHashes()
    {
        movementParameterHash = Animator.StringToHash(movementParameter);
        idleTriggerHash = Animator.StringToHash(idleTrigger);
    }

    private void RestartDelay()
    {
        float safeMaximum = Mathf.Max(minimumDelay, maximumDelay);
        timeUntilIdle = Random.Range(minimumDelay, safeMaximum);
    }
}
