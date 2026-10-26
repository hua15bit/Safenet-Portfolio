using UnityEngine;

/// <summary>
/// Connects one Area 3 dialogue encounter to the shared story overlay.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area3StoryboardVisuals : MonoBehaviour
{
    private Area3StoryOverlay overlay;
    private Texture incomingArtwork;
    private Texture choiceArtwork;
    private Texture correctOutcomeSheet;
    private Texture incorrectOutcomeSheet;
    private bool useCountdown;

    public void Configure(
        Area3StoryOverlay sharedOverlay,
        Texture incoming,
        Texture choices,
        Texture correctSheet,
        Texture incorrectSheet,
        bool countdown)
    {
        overlay = sharedOverlay;
        incomingArtwork = incoming;
        choiceArtwork = choices != null ? choices : incoming;
        correctOutcomeSheet = correctSheet;
        incorrectOutcomeSheet = incorrectSheet;
        useCountdown = countdown;
    }

    public void ShowIncomingMessage()
    {
        if (incomingArtwork != null)
            overlay?.Show(incomingArtwork, false);
        else
            overlay?.Hide();
    }

    public void ShowChoices()
    {
        if (choiceArtwork != null)
            // The urgency timer belongs to the decision, not the incoming line.
            overlay?.Show(choiceArtwork, useCountdown);
    }

    public void ShowChoiceResult(int scoreModifier)
    {
        Texture result = scoreModifier > 0 ? correctOutcomeSheet : incorrectOutcomeSheet;
        if (result != null)
            overlay?.ShowSheetFrame(result, 0, 4, 3, 700f);
        else if (choiceArtwork != null)
            overlay?.Show(choiceArtwork);
    }

    public void HidePresentation()
    {
        overlay?.Hide();
    }
}
