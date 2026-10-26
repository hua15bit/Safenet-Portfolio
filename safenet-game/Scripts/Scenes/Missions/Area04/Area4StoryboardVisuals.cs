using UnityEngine;

/// <summary>
/// Connects one Area 4 dialogue to the shared terminal close-up presenter.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area4StoryboardVisuals : MonoBehaviour
{
    private Area4StoryOverlay overlay;
    private Sprite incomingArtwork;
    private Sprite choiceArtwork;
    private Sprite correctArtwork;
    private Sprite incorrectArtwork;

    public void Configure(
        Area4StoryOverlay sharedOverlay,
        Sprite incoming,
        Sprite choices,
        Sprite correct,
        Sprite incorrect)
    {
        overlay = sharedOverlay;
        incomingArtwork = incoming;
        choiceArtwork = choices != null ? choices : incoming;
        correctArtwork = correct;
        incorrectArtwork = incorrect;
    }

    public void ShowIncomingMessage()
    {
        if (incomingArtwork != null)
            overlay?.Show(incomingArtwork);
        else
            overlay?.Hide();
    }

    public void ShowChoices()
    {
        if (choiceArtwork != null)
            overlay?.Show(choiceArtwork);
    }

    public void ShowChoiceResult(int scoreModifier)
    {
        Sprite result = scoreModifier > 0 ? correctArtwork : incorrectArtwork;
        if (result != null)
            overlay?.Show(result);
        else if (choiceArtwork != null)
            overlay?.Show(choiceArtwork);
    }

    public void HidePresentation()
    {
        overlay?.Hide();
    }
}
