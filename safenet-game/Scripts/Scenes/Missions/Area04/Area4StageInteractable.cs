using UnityEngine;

/// <summary>
/// One lockable interaction point in Area 4. Only the current story station can
/// receive input, which prevents a nearby terminal or NPC from stealing taps.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area4StageInteractable : MonoBehaviour, IInteractable
{
    private Dialogue1 dialogue;
    private Collider2D interactionCollider;
    private SpriteRenderer displayRenderer;
    private bool unlocked;
    private bool completed;

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
                : new Color(0.26f, 0.38f, 0.48f, 0.5f);
    }

    public void Complete(Sprite completedSprite = null, bool keepVisible = true)
    {
        completed = true;
        unlocked = false;

        if (interactionCollider != null)
            interactionCollider.enabled = false;

        if (displayRenderer != null)
        {
            if (completedSprite != null)
                displayRenderer.sprite = completedSprite;
            displayRenderer.enabled = keepVisible;
            displayRenderer.color = Color.white;
        }
    }

    public bool CanInteract()
    {
        // Dialogue1.Interact is also the next-line action during a conversation.
        return !completed && unlocked && dialogue != null;
    }

    public void Interact()
    {
        if (CanInteract())
            dialogue.Interact();
    }
}
