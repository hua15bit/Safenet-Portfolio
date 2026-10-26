using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shared mission-exit gate that validates required objectives and score before
/// saving progress and opening the configured next scene.
/// The class keeps its legacy name because existing Unity scenes serialize it.
/// </summary>
public class Level2 : MonoBehaviour, IInteractable
{
    [Header("Scene Configuration")]
    [Tooltip("Type the exact name of your scene asset here.")]
    public string sceneToLoad;

    [Header("Optional Mobile Interaction UI")]
    public Button interactButton;

    private void Start()
    {
        if (interactButton != null)
            interactButton.onClick.AddListener(Interact);
    }

    public void Interact()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "Game(area2)" &&
            !Area2MessageRoutingMinigame.IsCompleted)
        {
            Debug.LogWarning("Access Denied: Complete the Message Routing simulation first.");
            GameObject areaTwoPlayer = GameObject.FindGameObjectWithTag("MC");
            PlayerMovement areaTwoMovement = areaTwoPlayer != null
                ? areaTwoPlayer.GetComponent<PlayerMovement>()
                : null;
            areaTwoMovement?.ShowNotification(
                "Complete the Message Routing simulation first.",
                string.Empty);
            return;
        }

        if (currentScene == "Game(area3)" &&
            !Area3SceneBootstrap.IsCompleted)
        {
            Debug.LogWarning("Access Denied: Complete all social-engineering simulations first.");
            GameObject areaThreePlayer = GameObject.FindGameObjectWithTag("MC");
            PlayerMovement areaThreeMovement = areaThreePlayer != null
                ? areaThreePlayer.GetComponent<PlayerMovement>()
                : null;
            areaThreeMovement?.ShowNotification(
                "Complete all social-engineering simulations first.",
                string.Empty);
            return;
        }

        if (currentScene == "Game(area4)" &&
            !Area4SceneBootstrap.IsCompleted)
        {
            Debug.LogWarning("Access Denied: Secure every password scenario first.");
            GameObject areaFourPlayer = GameObject.FindGameObjectWithTag("MC");
            PlayerMovement areaFourMovement = areaFourPlayer != null
                ? areaFourPlayer.GetComponent<PlayerMovement>()
                : null;
            areaFourMovement?.ShowNotification(
                "Secure every password scenario before entering Area 5.",
                string.Empty);
            return;
        }

        // A failed bootstrap already disables its mission requirements, allowing
        // the player to leave instead of becoming trapped in a broken scene.
        bool areaThreeSetupFailed = currentScene == "Game(area3)" &&
                                    Area3SceneBootstrap.SetupFailed;
        bool areaFourSetupFailed = currentScene == "Game(area4)" &&
                                   Area4SceneBootstrap.SetupFailed;
        if (GameManager.Instance != null && !areaThreeSetupFailed && !areaFourSetupFailed)
        {
            if (GameManager.Instance.CurrentMissionScore < GameManager.Instance.scoreRequiredToPass)
            {
                Debug.LogWarning("Access Denied: Score threshold not satisfied.");

                GameObject blockedPlayer = GameObject.FindGameObjectWithTag("MC");
                if (blockedPlayer != null)
                {
                    PlayerMovement movement = blockedPlayer.GetComponent<PlayerMovement>();
                    movement?.ShowNotification(
                        $"Access Denied! Need {GameManager.Instance.scoreRequiredToPass} points to pass.",
                        string.Empty);
                }
                return;
            }
        }

        SaveCurrentMission();

        GameObject player = GameObject.FindGameObjectWithTag("MC");
        if (player != null)
        {
            PlayerMovement playerMove = player.GetComponent<PlayerMovement>();
            if (playerMove != null)
            {
                playerMove.ShowNotification("Entering Next Level...", sceneToLoad);
                return;
            }
        }
        if (sceneToLoad == "Game Complete")
            GameCompleteController.LoadScene();
        else
            SceneManager.LoadScene(sceneToLoad);
    }

    private void SaveCurrentMission()
    {
        SafeNetApiClient api = SafeNetApiClient.Instance;
        if (api == null)
            return;

        string sceneName = SceneManager.GetActiveScene().name;
        string missionKey = sceneName.Replace("Game(", "").Replace(")", "").Replace(" ", "-").ToLowerInvariant();
        int score = GameManager.Instance != null ? GameManager.Instance.CurrentMissionScore : 0;
        api.SaveMission(
            missionKey,
            score,
            true,
            SafeNetConstants.GradedMissionCount);
    }

    public bool CanInteract()
    {
        return true;
    }
}
