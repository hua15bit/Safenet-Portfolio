/// <summary>
/// Common contract for world objects that respond to the player's Interact
/// control, including NPCs, terminals, checkpoints, and exit doors.
/// </summary>
public interface IInteractable
{
    void Interact();

    bool CanInteract();
}
