using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reads desktop and mobile controls, moves and turns the main character, and
/// forwards the Interact action to the nearest compatible world object.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5;
    public Rigidbody2D rb;
    public Animator anim;
    public int facingDirection = 1;
    public float interactRange = 1.5f;
    private MonoBehaviour conversationTarget;
    private bool transitionPending;

    private void Update()
    {
        // Pausing freezes physics, but Unity still calls Update. Discard taps so
        // opening Settings cannot accidentally advance dialogue or enter a door.
        if (Time.timeScale <= 0f || transitionPending)
        {
            SafeNetMobileInput.Reset();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }
        bool dialoguePaused = DialogueController.IsDialogueActive;
        float keyboardHorizontal = SafeNetDesktopInput.Horizontal;
        float mobileHorizontal = SafeNetMobileInput.Horizontal;
        float requestedHorizontal = Mathf.Abs(mobileHorizontal) > 0.01f
            ? mobileHorizontal
            : keyboardHorizontal;
        float horizontal = dialoguePaused ? 0f : requestedHorizontal;

        if (!dialoguePaused &&
            ((horizontal > 0 && transform.localScale.x < 0) ||
             (horizontal < 0 && transform.localScale.x > 0)))
        {
            Flip();
        }

        if (anim != null) anim.SetFloat("horizontal", Mathf.Abs(horizontal));
        if (rb != null)
            rb.linearVelocity = new Vector2(dialoguePaused ? 0f : horizontal * speed, rb.linearVelocity.y);

        // Interaction remains available during dialogue so keyboard-controlled
        // conversations can still advance, but movement remains fully stopped.
        bool desktopInteractionPressed = SafeNetDesktopInput.InteractPressed;
        bool mobileInteractionPressed = SafeNetMobileInput.ConsumeInteractPressed();
        bool interactionPressed = desktopInteractionPressed || mobileInteractionPressed;
        if (interactionPressed && !DialogueController.IsAwaitingChoice)
        {
            IInteractable interactable = FindInteractionTarget();
            if (interactable != null)
            {
                conversationTarget = interactable as MonoBehaviour;
                SafeNetSfxPlayer.PlayInteract();
                interactable.Interact();
            }
        }
    }

    /// <summary>
    /// Select by distance to the collider, not physics-query order. Completed
    /// guides and disabled legacy doors must never swallow the Interact press.
    /// Keep the same speaker while a conversation is open.
    /// </summary>
    public IInteractable FindInteractionTarget()
    {
        if (DialogueController.IsDialogueActive)
            return conversationTarget != null && conversationTarget.isActiveAndEnabled &&
                   conversationTarget is IInteractable current && current.CanInteract() ? current : null;

        conversationTarget = null;
        Vector2 origin = TryGetComponent(out Collider2D body) ? body.bounds.center : transform.position;
        IInteractable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(origin, interactRange))
        {
            MonoBehaviour target = hit.GetComponent<IInteractable>() as MonoBehaviour;
            if (target == null || !target.isActiveAndEnabled ||
                !(target is IInteractable candidate) || !candidate.CanInteract()) continue;
            float distance = (hit.ClosestPoint(origin) - origin).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }

    private void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3(
            transform.localScale.x * -1,
            transform.localScale.y,
            transform.localScale.z);
    }

    [Header("Notification UI Layout")]
    public GameObject notificationBar;
    public TMP_Text notificationText;
    public float displayDuration = 2.0f;

    public void ShowNotification(string message, string nextScene)
    {
        if (transitionPending) return;
        transitionPending = !string.IsNullOrEmpty(nextScene);
        if (notificationBar != null && notificationText != null)
        {
            notificationText.text = message;
            notificationBar.SetActive(true);
            StartCoroutine(LoadLevelAfterDelay(nextScene));
        }
        else
        {
            if (!string.IsNullOrEmpty(nextScene))
                SceneManager.LoadScene(nextScene);
        }
    }

    private IEnumerator LoadLevelAfterDelay(string sceneName)
    {
        yield return new WaitForSeconds(displayDuration);
        if (!string.IsNullOrEmpty(sceneName))
        {
            if (sceneName == "Game Complete") GameCompleteController.LoadScene();
            else SceneManager.LoadScene(sceneName);
        }
        else if (notificationBar != null)
        {
            notificationBar.SetActive(false);
        }
    }
}
