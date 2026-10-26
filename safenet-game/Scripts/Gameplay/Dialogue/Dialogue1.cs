using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs data-driven NPC dialogue, presents choices through DialogueController,
/// and forwards choice results to the current mission's storyboard visuals.
/// </summary>
public class Dialogue1 : MonoBehaviour, IInteractable
{
    public event Action<Dialogue1> DialogueEnded;

    public NPC1 dialogueData;
    [Header("Interaction Lifecycle")]
    [SerializeField] private bool retireAfterDialogue = true;
    [SerializeField] private bool hideWorldVisualsWhenComplete = true;

    private DialogueController dialogueUI;
    private int dialogueIndex;
    private bool isTyping;
    private bool isDialogueActive;
    private bool hasCompletedDialogue;
    private Area1StoryboardVisuals area1Storyboard;
    private Area2StoryboardVisuals area2Storyboard;
    private Area3StoryboardVisuals area3Storyboard;
    private Area4StoryboardVisuals area4Storyboard;

    private void Start()
    {
        dialogueUI = DialogueController.Instance;
        area1Storyboard = GetComponent<Area1StoryboardVisuals>();
        area2Storyboard = GetComponent<Area2StoryboardVisuals>();
        area3Storyboard = GetComponent<Area3StoryboardVisuals>();
        area4Storyboard = GetComponent<Area4StoryboardVisuals>();
        PreloadDialogueVoice();
    }

    /// <summary>
    /// Assigns runtime-created dialogue data and immediately prepares its
    /// recordings. Mission bootstraps create several speakers during Awake;
    /// preloading here avoids relying on Unity's later Start ordering and
    /// prevents the first line from being silent on slower Android devices.
    /// </summary>
    public void ConfigureDialogue(NPC1 data)
    {
        dialogueData = data;
        PreloadDialogueVoice();
    }

    public bool CanInteract()
    {
        // Interact also advances this speaker's current conversation. Other
        // speakers remain unavailable until that conversation has finished.
        return dialogueData != null && !hasCompletedDialogue &&
               (isDialogueActive || !DialogueController.IsDialogueActive);
    }

    public void Interact()
    {
        if (dialogueData == null)
            return;

        if (dialogueUI == null)
            dialogueUI = DialogueController.Instance;

        if (dialogueUI == null)
        {
            Debug.LogError($"Cannot start dialogue on {name}: no DialogueController is active.", this);
            return;
        }

        if (isDialogueActive)
        {
            NextLine();
        }
        else
        {
            StartDialogue();
        }
    }

    private void StartDialogue()
    {
        if (hasCompletedDialogue)
            return;

        isDialogueActive = true;
        dialogueIndex = 0;

        dialogueUI.SetNPCInfo(dialogueData.npcName, dialogueData.npcPortrait);
        dialogueUI.ShowDialogueUI(true);

        // Start the recording before presenting large scene artwork. On slower
        // phones this keeps the first spoken line tied to the interaction tap.
        DisplayCurrentLine();
        area1Storyboard?.ShowIncomingMessage();
        area2Storyboard?.ShowIncomingMessage();
        area3Storyboard?.ShowIncomingMessage();
        area4Storyboard?.ShowIncomingMessage();
    }

    private void NextLine()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            VoiceLinePlayer.Stop();
            dialogueUI.setDialogueText(dialogueData.dialogueLines[dialogueIndex]);
            isTyping = false;
            return;
        }

        dialogueUI.ClearChoices();
        
        if (dialogueData.endDialogueLines.Length > dialogueIndex &&
            dialogueData.endDialogueLines[dialogueIndex])
        {
            EndDialogue();
            return;
        }

        foreach (DialogueChoice dialogueChoice in dialogueData.choices)
        {
            if (dialogueChoice.dialogueIndex == dialogueIndex)
            {
                DisplayChoices(dialogueChoice);
                return;
            }
        }

        if (++dialogueIndex < dialogueData.dialogueLines.Length)
        {
            DisplayCurrentLine();
        }
        else
        {
            EndDialogue();
        }
    }

    private IEnumerator TypeLine()
    {
        isTyping = true;
        dialogueUI.setDialogueText("");
        string line = dialogueData.dialogueLines[dialogueIndex];
        string clipKey = ResolveLineVoiceKey();
        VoiceLinePlayer.Stop();
        float voiceDuration = string.IsNullOrWhiteSpace(clipKey)
            ? 0f
            : VoiceLinePlayer.Play(clipKey, dialogueData.voicePitch);
        float characterDelay = voiceDuration > 0f && line.Length > 0
            ? Mathf.Clamp(voiceDuration / line.Length, 0.012f, 0.07f)
            : dialogueData.typingSpeed;

        foreach (char letter in line)
        {
            dialogueUI.setDialogueText(dialogueUI.dialogueText.text += letter);
            yield return new WaitForSecondsRealtime(characterDelay);
        }

        isTyping = false;

        if (dialogueData.autoProgressLines.Length > dialogueIndex &&
            dialogueData.autoProgressLines[dialogueIndex])
        {
            // Keep this line visible until its own recording finishes, then
            // use only a short reading pad. The old fixed 1.5-second wait was
            // added after every recording and caused the perceived audio lag.
            while (VoiceLinePlayer.IsPlaying)
                yield return null;

            float readingPad = voiceDuration > 0f
                ? Mathf.Min(dialogueData.autoProgressDelay, 0.2f)
                : dialogueData.autoProgressDelay;
            if (readingPad > 0f)
                yield return new WaitForSecondsRealtime(readingPad);
            NextLine();
        }
    }

    private void DisplayChoices(DialogueChoice choice)
    {
        area1Storyboard?.ShowChoices();
        area2Storyboard?.ShowChoices();
        area3Storyboard?.ShowChoices();
        area4Storyboard?.ShowChoices();
        VoiceLinePlayer.Stop();
        if (!string.IsNullOrWhiteSpace(choice.voiceClipKey))
            VoiceLinePlayer.Play(choice.voiceClipKey, dialogueData.voicePitch);

        dialogueUI.PrepareChoiceButtons(choice.choices.Length);

        for (int i = 0; i < choice.choices.Length; i++)
        {
            int nextIndex = choice.nextDialogueIndexes[i];

            int scoreModifier = choice.scoreModifiers != null && choice.scoreModifiers.Length > i
                ? choice.scoreModifiers[i]
                : 0;

            dialogueUI.CreateChoiceButton(
                choice.choices[i],
                () => ChooseOption(nextIndex, scoreModifier));
        }
    }

    private void ChooseOption(int nextIndex, int scoreModifier)
    {
        area1Storyboard?.ShowChoiceResult(scoreModifier);
        area2Storyboard?.ShowChoiceResult(scoreModifier);
        area3Storyboard?.ShowChoiceResult(scoreModifier);
        area4Storyboard?.ShowChoiceResult(scoreModifier);

        if (GameManager.Instance != null)
        {
            if (scoreModifier < 0)
                GameManager.Instance.TakeDamage();

            GameManager.Instance.AddScore(scoreModifier);
        }

        dialogueIndex = nextIndex;
        dialogueUI.ClearChoices();
        DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        StopAllCoroutines();
        ApplyCurrentSpeaker();
        StartCoroutine(TypeLine());
    }

    /// <summary>
    /// Changes the visible speaker at the same moment the matching recording
    /// starts. Conversations that use one character continue to fall back to
    /// the original NPC name and portrait.
    /// </summary>
    private void ApplyCurrentSpeaker()
    {
        if (dialogueUI == null || dialogueData == null)
            return;

        string speakerName = dialogueData.npcName;
        if (dialogueData.lineSpeakerNames != null &&
            dialogueIndex >= 0 &&
            dialogueIndex < dialogueData.lineSpeakerNames.Length &&
            !string.IsNullOrWhiteSpace(dialogueData.lineSpeakerNames[dialogueIndex]))
        {
            speakerName = dialogueData.lineSpeakerNames[dialogueIndex];
        }

        Sprite portrait = dialogueData.npcPortrait;
        if (dialogueData.lineSpeakerPortraits != null &&
            dialogueIndex >= 0 &&
            dialogueIndex < dialogueData.lineSpeakerPortraits.Length &&
            dialogueData.lineSpeakerPortraits[dialogueIndex] != null)
        {
            portrait = dialogueData.lineSpeakerPortraits[dialogueIndex];
        }

        dialogueUI.SetNPCInfo(speakerName, portrait);
    }

    /// <summary>
    /// Uses explicit per-line dubbing when a dialogue supplies it. Older
    /// dialogue assets keep their historic asset-name/index convention.
    /// </summary>
    private string ResolveLineVoiceKey()
    {
        if (dialogueData.voiceClipKeys != null && dialogueData.voiceClipKeys.Length > 0)
        {
            return dialogueIndex < dialogueData.voiceClipKeys.Length
                ? dialogueData.voiceClipKeys[dialogueIndex]
                : string.Empty;
        }

        return dialogueData.name.ToLowerInvariant() + "_" + dialogueIndex.ToString("00");
    }

    private void PreloadDialogueVoice()
    {
        if (dialogueData == null)
            return;

        List<string> keys = new List<string>();
        if (dialogueData.voiceClipKeys != null && dialogueData.voiceClipKeys.Length > 0)
        {
            keys.AddRange(dialogueData.voiceClipKeys);
        }
        else if (dialogueData.dialogueLines != null)
        {
            for (int index = 0; index < dialogueData.dialogueLines.Length; index++)
                keys.Add(dialogueData.name.ToLowerInvariant() + "_" + index.ToString("00"));
        }

        if (dialogueData.choices != null)
        {
            foreach (DialogueChoice choice in dialogueData.choices)
                if (choice != null) keys.Add(choice.voiceClipKey);
        }

        VoiceLinePlayer.Preload(keys.ToArray());
    }

    public void EndDialogue()
    {
        if (!isDialogueActive)
            return;

        StopAllCoroutines();
        VoiceLinePlayer.Stop();
        isDialogueActive = false;
        hasCompletedDialogue = retireAfterDialogue;
        dialogueUI.setDialogueText("");
        dialogueUI.ShowDialogueUI(false);
        area2Storyboard?.HidePresentation();
        area3Storyboard?.HidePresentation();
        area4Storyboard?.HidePresentation();

        if (hasCompletedDialogue && hideWorldVisualsWhenComplete)
        {
            // Keep the GameObject and mission references alive, but remove the
            // finished character from the world as requested by the story flow.
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = false;
        }

        DialogueEnded?.Invoke(this);
    }
}

