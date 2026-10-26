using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runs the new Aegis-led phishing practice. Normal dialogue lines advance with
/// the Interact control; only the three decisions create on-screen UI buttons.
/// </summary>
public sealed class TutorialAegisInteractable : MonoBehaviour, IInteractable
{
    private const string WelcomeVoice = "tutorial_new_intro";
    private const string SafetyVoice = "tutorial_new_safety";
    private const string MessageVoice = "tutorial_new_message";
    private const string ChoicePromptVoice = "tutorial_new_choice_prompt";
    private const string CorrectVoice = "tutorial_new_correct";
    private const string UnsafeLinkVoice = "tutorial_new_unsafe_link";
    private const string UnsafePersonalVoice = "tutorial_new_unsafe_personal";
    private const string ConclusionVoice = "tutorial_new_conclusion";

    private const string WelcomeLine =
        "Welcome, Aira. Before your first mission, let us begin with a simple cybersecurity scenario.";
    private const string SafetyLine =
        "Read the situation carefully, and then choose the safest response.";
    private const string MessageLine =
        "A message from an unknown sender appears:\n\"Congratulations! You won a free prize! Click this shortened link to claim it.\"";
    private const string ConclusionLine =
        "Practice scenario complete. Always examine the warning signs before making a decision.";

    [SerializeField] private float typingSpeed = 0.018f;

    private TutorialFlow flow;
    private TutorialSceneBootstrap sceneBootstrap;
    private DialogueController dialogueUI;
    private Sprite portrait;
    private Coroutine typingRoutine;
    private Coroutine completionRoutine;
    private string currentLine = string.Empty;
    private LineCompletion lineCompletion;
    private bool dialogueActive;
    private bool choicePending;
    private bool transitionActive;
    private bool safeChoice;
    private int phase;

    private enum LineCompletion
    {
        None,
        ShowChoices,
        ShowConclusion,
        FinishConversation
    }

    public void Configure(
        TutorialFlow tutorialFlow,
        TutorialSceneBootstrap bootstrap,
        Sprite aegisPortrait)
    {
        flow = tutorialFlow;
        sceneBootstrap = bootstrap;
        portrait = aegisPortrait;
    }

    private void Start()
    {
        dialogueUI = DialogueController.Instance;
        VoiceLinePlayer.Preload(
            WelcomeVoice,
            SafetyVoice,
            MessageVoice,
            ChoicePromptVoice,
            CorrectVoice,
            UnsafeLinkVoice,
            UnsafePersonalVoice,
            ConclusionVoice);
    }

    public bool CanInteract()
    {
        return flow != null && flow.HowToDismissed && !flow.PracticeCompleted &&
               !choicePending && !transitionActive;
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        if (dialogueUI == null)
            dialogueUI = DialogueController.Instance;
        if (dialogueUI == null)
            return;

        if (typingRoutine != null)
        {
            FinishTypingImmediately();
            return;
        }

        // A completed result line normally advances after a short pause. The
        // same E / mobile Interact action can skip that pause immediately.
        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
            VoiceLinePlayer.Stop();
            ExecuteLineCompletion();
            return;
        }

        if (!dialogueActive)
        {
            BeginConversation();
            return;
        }

        AdvanceConversation();
    }

    private void BeginConversation()
    {
        dialogueActive = true;
        phase = 0;
        dialogueUI.SetNPCInfo("Aegis", portrait);
        dialogueUI.ClearChoices();
        dialogueUI.ShowDialogueUI(true);
        ShowLine(WelcomeLine, WelcomeVoice);
    }

    private void AdvanceConversation()
    {
        switch (phase)
        {
            case 0:
                phase = 1;
                ShowLine(SafetyLine, SafetyVoice);
                break;
            case 1:
                phase = 2;
                StartCoroutine(ActivatePracticeScenario());
                break;
            case 2:
                phase = 3;
                ShowChoices();
                break;
            case 4:
                phase = 5;
                ShowLine(ConclusionLine, ConclusionVoice);
                break;
            case 5:
                FinishConversation();
                break;
        }
    }

    private IEnumerator ActivatePracticeScenario()
    {
        transitionActive = true;
        dialogueUI.ShowDialogueUI(false);
        yield return sceneBootstrap.ShowBannerAndWait("PRACTICE SCENARIO ACTIVATED.", 1.45f);
        dialogueUI.SetNPCInfo("Aegis", portrait);
        dialogueUI.ShowDialogueUI(true);
        // The decision appears as soon as the simulated message finishes.
        // This avoids a phone user being trapped waiting for an unexplained
        // extra INTERACT tap before any choices are visible.
        ShowLine(MessageLine, MessageVoice, LineCompletion.ShowChoices);
        transitionActive = false;
    }

    private void ShowChoices()
    {
        lineCompletion = LineCompletion.None;
        choicePending = true;
        VoiceLinePlayer.Stop();
        VoiceLinePlayer.Play(ChoicePromptVoice);
        dialogueUI.ClearChoices();
        dialogueUI.setDialogueText("What should you do?");
        dialogueUI.PrepareChoiceButtons(3);
        dialogueUI.useLightStoryChoiceButtons = true;
        StyleTutorialChoice(dialogueUI.CreateChoiceButton(
            "A. Open the shortened link to claim the prize.",
            () => EvaluateChoice(0)));
        StyleTutorialChoice(dialogueUI.CreateChoiceButton(
            "B. Reply with your personal information.",
            () => EvaluateChoice(1)));
        StyleTutorialChoice(dialogueUI.CreateChoiceButton(
            "C. Delete the message and report the sender.",
            () => EvaluateChoice(2)));
    }

    private static void StyleTutorialChoice(GameObject choice)
    {
        if (choice == null)
            return;

        Image image = choice.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.035f, 0.17f, 0.27f, 0.99f);

        Outline outline = choice.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(0.22f, 0.85f, 1f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        TMP_Text label = choice.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    private void EvaluateChoice(int choice)
    {
        if (!choicePending)
            return;

        choicePending = false;
        phase = 4;
        dialogueUI.ClearChoices();

        switch (choice)
        {
            case 2:
                safeChoice = true;
                ShowLine(
                    "Correct choice. The message contains common phishing signs: an unknown sender, an unexpected prize, urgent wording, and a suspicious link.",
                    CorrectVoice,
                    LineCompletion.ShowConclusion);
                break;
            case 0:
                ShowLine(
                    "Unsafe choice. Unknown links may lead to fake websites or install harmful software.",
                    UnsafeLinkVoice,
                    LineCompletion.ShowConclusion);
                break;
            default:
                ShowLine(
                    "Unsafe choice. Sharing personal information with an unknown sender may put your identity and accounts at risk.",
                    UnsafePersonalVoice,
                    LineCompletion.ShowConclusion);
                break;
        }
    }

    private void ShowLine(
        string line,
        string voiceKey = null,
        LineCompletion completion = LineCompletion.None)
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);
        if (completionRoutine != null)
            StopCoroutine(completionRoutine);

        typingRoutine = null;
        completionRoutine = null;
        currentLine = line;
        lineCompletion = completion;
        dialogueUI.ClearChoices();
        float voiceDuration = VoiceLinePlayer.Play(voiceKey);
        typingRoutine = StartCoroutine(TypeLine(voiceDuration));
    }

    private IEnumerator TypeLine(float voiceDuration)
    {
        dialogueUI.setDialogueText(string.Empty);
        float characterDelay = voiceDuration > 0f && currentLine.Length > 0
            ? Mathf.Clamp(voiceDuration / currentLine.Length, typingSpeed, 0.085f)
            : typingSpeed;

        foreach (char character in currentLine)
        {
            dialogueUI.setDialogueText(dialogueUI.dialogueText.text + character);
            yield return new WaitForSecondsRealtime(characterDelay);
        }

        typingRoutine = null;
        if (lineCompletion != LineCompletion.None)
            completionRoutine = StartCoroutine(CompleteLineWhenVoiceEnds());
    }

    private void FinishTypingImmediately()
    {
        StopCoroutine(typingRoutine);
        typingRoutine = null;
        VoiceLinePlayer.Stop();
        dialogueUI.setDialogueText(currentLine);

        // Skip means both reveal the remaining text and perform the line's
        // pending transition. This prevents post-decision feedback from
        // appearing frozen while still supporting desktop and mobile input.
        if (lineCompletion != LineCompletion.None)
            ExecuteLineCompletion();
    }

    private IEnumerator CompleteLineWhenVoiceEnds()
    {
        while (VoiceLinePlayer.IsPlaying)
            yield return null;

        yield return new WaitForSecondsRealtime(0.2f);
        completionRoutine = null;
        ExecuteLineCompletion();
    }

    private void ExecuteLineCompletion()
    {
        LineCompletion completion = lineCompletion;
        lineCompletion = LineCompletion.None;

        switch (completion)
        {
            case LineCompletion.ShowChoices:
                phase = 3;
                ShowChoices();
                break;
            case LineCompletion.ShowConclusion:
                phase = 5;
                ShowLine(ConclusionLine, ConclusionVoice, LineCompletion.FinishConversation);
                break;
            case LineCompletion.FinishConversation:
                FinishConversation();
                break;
        }
    }

    private void FinishConversation()
    {
        if (typingRoutine != null)
            StopCoroutine(typingRoutine);
        if (completionRoutine != null)
            StopCoroutine(completionRoutine);

        typingRoutine = null;
        completionRoutine = null;
        dialogueActive = false;
        lineCompletion = LineCompletion.None;
        VoiceLinePlayer.Stop();
        dialogueUI.ClearChoices();
        dialogueUI.setDialogueText(string.Empty);
        dialogueUI.ShowDialogueUI(false);
        sceneBootstrap.CompleteTutorial(safeChoice);
    }

    private void OnDestroy()
    {
        VoiceLinePlayer.Stop();
    }
}
