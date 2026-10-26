using UnityEngine;

/// <summary>
/// Drives the full-screen phone and choice artwork used by Area 2's two
/// smishing scenarios while the normal dialogue system handles input.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area2StoryboardVisuals : MonoBehaviour
{
    public enum ScenarioType
    {
        PrizeMessage,
        DeliveryFee
    }

    [SerializeField] private ScenarioType scenario;
    [SerializeField] private SpriteRenderer phoneRenderer;
    [SerializeField] private SpriteRenderer panelRenderer;

    [Header("Scenario Artwork")]
    [SerializeField] private Sprite messagePhone;
    [SerializeField] private Sprite choicesPanel;
    [SerializeField] private Sprite correctResult;
    [SerializeField] private Sprite incorrectResult;

    [Header("Presentation Size")]
    [SerializeField] private float phoneHeight = 13.5f;
    [SerializeField] private float panelWidth = 13.5f;

    private void Awake()
    {
        HidePresentation();
    }

    /// <summary>
    /// Connects one scenario to the shared story overlay. Area 2 creates the
    /// overlay once so both NPC encounters appear in the same screen position.
    /// </summary>
    public void Configure(
        ScenarioType scenarioType,
        SpriteRenderer sharedPhoneRenderer,
        SpriteRenderer sharedPanelRenderer,
        Sprite incomingPhone,
        Sprite choiceArtwork,
        Sprite correctArtwork,
        Sprite incorrectArtwork,
        float targetPhoneHeight,
        float targetPanelWidth)
    {
        scenario = scenarioType;
        phoneRenderer = sharedPhoneRenderer;
        panelRenderer = sharedPanelRenderer;
        messagePhone = incomingPhone;
        choicesPanel = choiceArtwork;
        correctResult = correctArtwork;
        incorrectResult = incorrectArtwork;
        phoneHeight = targetPhoneHeight;
        panelWidth = targetPanelWidth;
        HidePresentation();
    }

    public void ShowIncomingMessage()
    {
        ShowPhone(messagePhone);
        SetPanelVisible(false);
    }

    public void ShowChoices()
    {
        ShowPhone(messagePhone);

        // Answer choices are real Unity UI buttons. Keeping the old artwork
        // hidden guarantees that the visible button and its touch area match.
        SetPanelVisible(false);
    }

    public void ShowChoiceResult(int scoreModifier)
    {
        bool correct = scoreModifier > 0;

        // The dialogue already explains whether the delivery choice was safe.
        // Do not stack the old correct/incorrect Choices 1 artwork over it.
        if (scenario == ScenarioType.DeliveryFee)
        {
            ShowPhone(messagePhone);
            SetPanelVisible(false);
            return;
        }

        if (scenario == ScenarioType.PrizeMessage)
        {
            SetPanelVisible(false);
            ShowPhone(correct ? correctResult : incorrectResult);
            return;
        }
    }

    public void HidePresentation()
    {
        if (phoneRenderer != null) phoneRenderer.enabled = false;
        if (panelRenderer != null) panelRenderer.enabled = false;
    }

    private void ShowPhone(Sprite sprite)
    {
        if (phoneRenderer == null || sprite == null)
            return;

        phoneRenderer.sprite = sprite;
        phoneRenderer.enabled = true;
        float spriteHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
        float scale = phoneHeight / spriteHeight;
        phoneRenderer.transform.localScale = Vector3.one * scale;
    }

    private void ShowPanel(Sprite sprite)
    {
        if (panelRenderer == null || sprite == null)
            return;

        panelRenderer.sprite = sprite;
        panelRenderer.enabled = true;
        float spriteWidth = Mathf.Max(0.01f, sprite.bounds.size.x);
        float scale = panelWidth / spriteWidth;
        panelRenderer.transform.localScale = Vector3.one * scale;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRenderer != null)
            panelRenderer.enabled = visible;
    }
}
