using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs Area 5's privacy-sharing decision as a short staged conversation. Real
/// UI Buttons are created by DialogueController, avoiding overlapping image
/// hitboxes on phones and tablets.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area5PrivacyInteraction : MonoBehaviour, IInteractable
{
    private enum Step
    {
        Ready,
        AiraWarning,
        NpcQuestion,
        WaitingForChoice,
        Feedback,
        Complete
    }

    private Area5SceneBootstrap mission;
    private Collider2D interactionCollider;
    private DialogueController dialogue;
    private Step step;
    private bool answerWasCorrect;
    private bool voiceActive;

    private void Start()
    {
        VoiceLinePlayer.Preload(
            "level5_privacy_question",
            "level5_aira_warning",
            "level5_choice_prompt",
            "level5_correct",
            "level5_unsafe");
    }

    public void Configure(Area5SceneBootstrap owner, Collider2D targetCollider)
    {
        mission = owner;
        interactionCollider = targetCollider;
        step = Step.Ready;
        if (interactionCollider != null)
            interactionCollider.enabled = true;
    }

    public bool CanInteract()
    {
        return mission != null && step != Step.Complete;
    }

    public void Interact()
    {
        if (!CanInteract())
            return;

        if (dialogue == null)
            dialogue = DialogueController.Instance;
        if (dialogue == null)
        {
            Debug.LogError("Area 5 cannot start: DialogueController is unavailable.", this);
            return;
        }

        switch (step)
        {
            case Step.Ready:
                mission.SetNpcState(Area5SceneBootstrap.NpcState.Corrupted);
                ShowLine(
                    "Aira",
                    mission.PlayerPortrait,
                    "Personal information is escaping into untrusted areas.",
                    "level5_aira_warning");
                step = Step.AiraWarning;
                break;

            case Step.AiraWarning:
                mission.SetNpcState(Area5SceneBootstrap.NpcState.Questioning);
                ShowLine(
                    "Privacy Guide",
                    mission.NpcPortrait,
                    "Is it safe to post this?",
                    "level5_privacy_question");
                step = Step.NpcQuestion;
                break;

            case Step.NpcQuestion:
                ShowChoices();
                step = Step.WaitingForChoice;
                break;

            case Step.Feedback:
                StopVoice();
                dialogue.ShowDialogueUI(false);
                step = Step.Complete;
                if (interactionCollider != null)
                    interactionCollider.enabled = false;
                mission.CompleteMission(answerWasCorrect);
                break;
        }
    }

    private void ShowLine(
        string speaker,
        Sprite portrait,
        string message,
        string voiceClipKey)
    {
        dialogue.ClearChoices();
        dialogue.SetNPCInfo(speaker, portrait);
        dialogue.setDialogueText(message);
        dialogue.ShowDialogueUI(true);
        PlayVoice(voiceClipKey);
    }

    private void ShowChoices()
    {
        dialogue.SetNPCInfo("Privacy Decision", mission.NpcPortrait);
        dialogue.setDialogueText("Choose the safest way to handle the sensitive data.");
        dialogue.PrepareChoiceButtons(2);
        dialogue.CreateChoiceButton(
            "A. Remove or cover private information.",
            () => ResolveChoice(true));
        dialogue.CreateChoiceButton(
            "B. Upload the original image publicly.",
            () => ResolveChoice(false));
        PlayVoice("level5_choice_prompt");
    }

    private void ResolveChoice(bool correct)
    {
        if (step != Step.WaitingForChoice)
            return;

        // A fast answer must not leave the question playing over its feedback.
        StopVoice();
        answerWasCorrect = correct;
        dialogue.ClearChoices();

        GameManager manager = GameManager.Instance;
        if (correct)
        {
            manager?.AddScore(5);
            mission.SetNpcState(Area5SceneBootstrap.NpcState.Safe);
            ShowLine(
                "Aira",
                mission.PlayerPortrait,
                "Correct! Always review images and documents before sharing them.",
                "level5_correct");
        }
        else
        {
            manager?.TakeDamage();
            if (SceneManager.GetActiveScene().name != "Game(area5)")
                return;

            manager?.AddScore(0);
            mission.SetNpcState(Area5SceneBootstrap.NpcState.Blocked);
            ShowLine(
                "Aira",
                mission.PlayerPortrait,
                "That is not safe. A careful assessment is crucial for security. " +
                "Always review images and documents before sharing them.",
                "level5_unsafe");
        }

        step = Step.Feedback;
    }

    private void PlayVoice(string voiceClipKey)
    {
        VoiceLinePlayer.Play(voiceClipKey);
        voiceActive = true;
    }

    private void StopVoice()
    {
        if (!voiceActive)
            return;
        VoiceLinePlayer.Stop();
        voiceActive = false;
    }

    private void OnDestroy()
    {
        StopVoice();
    }
}
