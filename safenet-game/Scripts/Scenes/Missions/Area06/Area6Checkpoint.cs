using UnityEngine;

/// <summary>
/// A physical, ordered interaction point in Mission 6. The trigger can only be
/// used when this is the current checkpoint, and its blocker prevents the
/// player from walking into later challenges early.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area6Checkpoint : MonoBehaviour, IInteractable
{
    private Area6SceneBootstrap mission;
    private int checkpointIndex;
    private GameObject blocker;
    private GameObject artwork;
    private Collider2D interactionTrigger;
    private bool resolved;

    public int CheckpointIndex => checkpointIndex;

    public void Configure(
        Area6SceneBootstrap owner,
        int index,
        GameObject blockerObject,
        GameObject artworkObject,
        Collider2D trigger)
    {
        mission = owner;
        checkpointIndex = index;
        blocker = blockerObject;
        artwork = artworkObject;
        interactionTrigger = trigger;
        resolved = false;
    }

    public bool CanInteract()
    {
        return !resolved && mission != null &&
               mission.IsCurrentCheckpoint(checkpointIndex);
    }

    public void Interact()
    {
        if (CanInteract())
            mission.OpenCheckpoint(this);
    }

    public void MarkResolved()
    {
        if (resolved)
            return;

        resolved = true;
        if (blocker != null)
            blocker.SetActive(false);
        if (artwork != null)
            artwork.SetActive(false);
        if (interactionTrigger != null)
            interactionTrigger.enabled = false;
    }
}
