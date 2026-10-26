using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the tutorial door locked until Aegis finishes the practice scenario.
/// </summary>
public sealed class TutorialExit : MonoBehaviour, IInteractable
{
    [SerializeField] private string nextScene = "Game(area1)";

    public bool CanInteract()
    {
        return !DialogueController.IsDialogueActive;
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        TutorialFlow flow = TutorialFlow.Instance;
        GameObject player = GameObject.FindGameObjectWithTag("MC");
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;

        if (flow == null || !flow.PracticeCompleted)
        {
            if (movement != null)
                movement.ShowNotification("ACCESS DENIED: Complete Aegis's practice scenario first.", string.Empty);
            return;
        }

        flow.RecordTutorialCompletion();
        if (movement != null)
        {
            movement.ShowNotification("Tutorial complete! Entering Mission 1...", nextScene);
            return;
        }

        SceneManager.LoadScene(nextScene);
    }
}
