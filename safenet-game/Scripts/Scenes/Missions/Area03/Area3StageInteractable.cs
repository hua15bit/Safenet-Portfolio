using UnityEngine;

/// <summary>
/// A lockable world interaction point that forwards input to a child dialogue.
/// Keeping this component on the collider avoids duplicate IInteractable targets.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area3StageInteractable : MonoBehaviour, IInteractable
{
    private Dialogue1 dialogue;
    private Collider2D interactionCollider;
    private SpriteRenderer displayRenderer;
    private bool unlocked;
    private bool completed;

    public bool IsCompleted => completed;

    public void Configure(Dialogue1 targetDialogue, Collider2D targetCollider, SpriteRenderer renderer)
    {
        dialogue = targetDialogue;
        interactionCollider = targetCollider;
        displayRenderer = renderer;
        SetUnlocked(false);
    }

    public void SetUnlocked(bool value)
    {
        if (completed)
            return;

        unlocked = value;
        if (interactionCollider != null)
            interactionCollider.enabled = value;
        if (displayRenderer != null)
            displayRenderer.color = value
                ? Color.white
                : new Color(0.28f, 0.38f, 0.48f, 0.45f);
    }

    /// <summary>
    /// Permanently retires a finished station so it cannot award score twice or
    /// interfere with the next encounter's interaction target.
    /// </summary>
    public void Complete(bool keepVisible = false)
    {
        completed = true;
        unlocked = false;

        if (interactionCollider != null)
            interactionCollider.enabled = false;

        if (displayRenderer != null)
        {
            displayRenderer.enabled = keepVisible;
            displayRenderer.color = keepVisible
                ? new Color(0.58f, 0.75f, 0.82f, 0.75f)
                : Color.white;
        }
    }

    public bool CanInteract()
    {
        // Keep this station available for the next-line action until the
        // conversation finishes; completed stations retire their colliders.
        return !completed && unlocked && dialogue != null;
    }

    public void Interact()
    {
        if (CanInteract())
            dialogue.Interact();
    }
}
