using TMPro;
using UnityEngine;

/// <summary>
/// Stores the small amount of progression used by the tutorial scene.
/// The visual setup and Aegis conversation are handled by dedicated components.
/// </summary>
public sealed class TutorialFlow : MonoBehaviour
{
    public static TutorialFlow Instance { get; private set; }

    [SerializeField] private TMP_Text objectiveText;

    public bool HowToDismissed { get; private set; }
    public bool PracticeCompleted { get; private set; }
    public bool ChoseSafeResponse { get; private set; }

    private void Awake()
    {
        Instance = this;
        HowToDismissed = false;
        PracticeCompleted = false;
        ChoseSafeResponse = false;

        UpdateObjective();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void BeginTutorial()
    {
        HowToDismissed = true;
        UpdateObjective();
    }

    public void CompletePractice(bool choseSafeResponse)
    {
        if (PracticeCompleted)
            return;

        PracticeCompleted = true;
        ChoseSafeResponse = choseSafeResponse;
        UpdateObjective();
    }

    public void SetObjectiveVisible(bool visible)
    {
        if (objectiveText == null)
            return;

        Transform panel = objectiveText.transform.parent;
        if (panel != null)
            panel.gameObject.SetActive(visible);
        else
            objectiveText.gameObject.SetActive(visible);
    }

    public void UpdateObjective()
    {
        if (objectiveText == null)
            return;

        if (!HowToDismissed)
            objectiveText.text = "HOW TO PLAY";
        else if (!PracticeCompleted)
            objectiveText.text = "OBJECTIVE: Approach Aegis and complete the phishing practice.";
        else
            objectiveText.text = "OBJECTIVE: Enter the secured door to begin Mission 1.";
    }

    public void RecordTutorialCompletion()
    {
        // Tutorial completion remains local because only Areas 1-6 contribute
        // to the professor dashboard's score and mission-completion totals.
    }
}
