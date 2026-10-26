using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the shared dialogue view in each gameplay scene, including speaker
/// details, dialogue text, dynamically created choices, and scene-specific layouts.
/// </summary>
public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }
    public static bool IsDialogueActive { get; private set; }
    public static bool IsAwaitingChoice { get; private set; }

    public GameObject dialoguePanel;
    public TMP_Text dialogueText, nameText;
    public Image portraitImage;
    public Transform choiceContainer;
    public GameObject choiceButtonPrefab;
    public bool useImmersiveChoiceLayout;
    public bool useSpriteBackedChoiceLayout;
    public bool useLightStoryChoiceButtons;
    public bool useWhiteReadingSurface;

    private RectTransform dialoguePanelRect;
    private RectTransform dialogueTextRect;
    private RectTransform choicePanelRect;
    private Image dialoguePanelImage;
    private Image choicePanelImage;
    private Vector2 originalPanelPosition;
    private Vector2 originalPanelSize;
    private Vector2 originalTextPosition;
    private Vector2 originalTextSize;
    private Vector2 originalChoicePosition;
    private Vector2 originalChoiceSize;
    private Color originalPanelColor;
    private Color originalChoicePanelColor;
    private bool originalPanelRaycastTarget;
    private bool originalChoicePanelRaycastTarget;
    private TextAlignmentOptions originalTextAlignment;
    private FontStyles originalTextStyle;
    private bool portraitWasActive;
    private bool nameWasActive;
    private bool textWasActive;
    private bool immersiveLayoutCaptured;
    private bool immersiveLayoutVisible;
    private int preparedChoiceCount;
    private int createdChoiceCount;
    private VerticalLayoutGroup choiceLayoutGroup;
    private bool originalChoiceLayoutGroupEnabled;
    private Image whiteReadingSurface;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            IsDialogueActive = dialoguePanel != null && dialoguePanel.activeSelf;
            IsAwaitingChoice = false;
            if (useWhiteReadingSurface)
                ConfigureWhiteReadingSurface();
            CaptureImmersiveLayout();
            SetChoicePanelVisible(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        IsDialogueActive = false;
        IsAwaitingChoice = false;
    }

    public void ShowDialogueUI(bool show)
    {
        IsDialogueActive = show;
        if (!show)
            IsAwaitingChoice = false;

        if (show)
            SetChoicePanelVisible(false);
        else
            ClearChoices();

        if (dialoguePanel != null) dialoguePanel.SetActive(show);
    }

    public void SetNPCInfo(string npcName, Sprite portrait)
    {
        if (nameText != null) nameText.text = npcName;
        if (portraitImage != null) portraitImage.sprite = portrait;
    }

    public void setDialogueText(string text)
    {
        if (dialogueText != null) dialogueText.text = text;
    }

    /// <summary>
    /// Adds a high-contrast white reading area inside the dialogue artwork.
    /// Area 5 enables this without changing dialogue presentation elsewhere.
    /// </summary>
    public void ConfigureWhiteReadingSurface()
    {
        useWhiteReadingSurface = true;
        if (dialoguePanel == null || dialogueText == null || nameText == null)
            return;

        Transform existing = dialoguePanel.transform.Find("Dialogue Reading Surface");
        if (existing != null)
            whiteReadingSurface = existing.GetComponent<Image>();

        if (whiteReadingSurface == null)
        {
            GameObject surface = new GameObject(
                "Dialogue Reading Surface",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            surface.layer = dialoguePanel.layer;
            surface.transform.SetParent(dialoguePanel.transform, false);
            whiteReadingSurface = surface.GetComponent<Image>();
        }

        whiteReadingSurface.transform.SetAsFirstSibling();
        whiteReadingSurface.sprite = null;
        whiteReadingSurface.color = new Color(0.985f, 0.995f, 1f, 0.98f);
        whiteReadingSurface.raycastTarget = false;

        RectTransform surfaceRect = whiteReadingSurface.rectTransform;
        surfaceRect.anchorMin = surfaceRect.anchorMax = surfaceRect.pivot = new Vector2(0.5f, 0.5f);
        // Keep the white fill within the frame's right-hand reading opening.
        // The portrait and decorative border remain fully visible on the left.
        surfaceRect.anchoredPosition = new Vector2(85f, 0f);
        surfaceRect.sizeDelta = new Vector2(820f, 250f);

        if (portraitImage != null)
        {
            RectTransform portraitRect = portraitImage.rectTransform;
            portraitRect.anchoredPosition = new Vector2(-420f, 0f);
            portraitRect.sizeDelta = new Vector2(150f, 170f);
            portraitImage.raycastTarget = false;
        }

        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchoredPosition = new Vector2(85f, 90f);
        nameRect.sizeDelta = new Vector2(790f, 44f);
        nameText.color = new Color(0.02f, 0.22f, 0.3f, 1f);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 22f;
        nameText.fontSizeMax = 30f;
        nameText.raycastTarget = false;

        RectTransform textRect = dialogueText.rectTransform;
        textRect.anchoredPosition = new Vector2(85f, -10f);
        textRect.sizeDelta = new Vector2(790f, 130f);
        dialogueText.color = new Color(0.015f, 0.035f, 0.05f, 1f);
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMin = 22f;
        dialogueText.fontSizeMax = 31f;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.overflowMode = TextOverflowModes.Ellipsis;
        dialogueText.margin = new Vector4(12f, 8f, 12f, 8f);
        dialogueText.raycastTarget = false;
    }

    /// <summary>
    /// Restores a simple built-in dialogue presentation with no custom frame
    /// sprite or additional reading overlay.
    /// </summary>
    public void ConfigureDefaultDialogueSurface()
    {
        useWhiteReadingSurface = false;
        if (dialoguePanel == null || dialogueText == null || nameText == null)
            return;

        Transform existing = dialoguePanel.transform.Find("Dialogue Reading Surface");
        if (existing != null)
        {
            whiteReadingSurface = existing.GetComponent<Image>();
            existing.gameObject.SetActive(false);
        }

        RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchoredPosition = new Vector2(0f, 215f);
            panelRect.sizeDelta = new Vector2(1120f, 330f);
        }

        Image panel = dialoguePanel.GetComponent<Image>();
        if (panel != null)
        {
            panel.sprite = null;
            panel.type = Image.Type.Simple;
            panel.preserveAspect = false;
            panel.color = new Color(0.018f, 0.045f, 0.065f, 0.96f);
            panel.raycastTarget = true;
        }

        if (portraitImage != null)
        {
            RectTransform portraitRect = portraitImage.rectTransform;
            portraitRect.anchoredPosition = new Vector2(-455f, 0f);
            portraitRect.sizeDelta = new Vector2(150f, 150f);
            portraitImage.raycastTarget = false;
        }

        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchoredPosition = new Vector2(85f, 105f);
        nameRect.sizeDelta = new Vector2(790f, 42f);
        nameText.color = new Color(0.35f, 0.94f, 1f, 1f);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 22f;
        nameText.fontSizeMax = 30f;
        nameText.raycastTarget = false;

        RectTransform textRect = dialogueText.rectTransform;
        textRect.anchoredPosition = new Vector2(85f, 0f);
        textRect.sizeDelta = new Vector2(790f, 145f);
        dialogueText.color = Color.white;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.fontStyle = FontStyles.Normal;
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMin = 21f;
        dialogueText.fontSizeMax = 30f;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.overflowMode = TextOverflowModes.Ellipsis;
        dialogueText.margin = new Vector4(12f, 8f, 12f, 8f);
        dialogueText.raycastTarget = false;

        RectTransform choices = choiceContainer as RectTransform;
        if (choices != null)
        {
            choices.anchoredPosition = new Vector2(85f, -105f);
            choices.sizeDelta = new Vector2(790f, 130f);
        }
        Image choicesImage = choiceContainer != null
            ? choiceContainer.GetComponent<Image>()
            : null;
        if (choicesImage != null)
        {
            Color transparent = choicesImage.color;
            transparent.a = 0f;
            choicesImage.color = transparent;
            choicesImage.raycastTarget = false;
        }
    }

    public void ClearChoices()
    {
        IsAwaitingChoice = false;
        if (choiceContainer == null) return;
        foreach (Transform child in choiceContainer) Destroy(child.gameObject);
        SetChoicePanelVisible(false);
    }

    public void PrepareChoiceButtons(int choiceCount)
    {
        IsAwaitingChoice = false;
        preparedChoiceCount = Mathf.Max(1, choiceCount);
        createdChoiceCount = 0;
    }

    public GameObject CreateChoiceButton(string choiceText, UnityEngine.Events.UnityAction onClick)
    {
        if (choiceButtonPrefab == null || choiceContainer == null) return null;
        IsAwaitingChoice = true;
        SetChoicePanelVisible(true);
        GameObject choiceButton = Instantiate(choiceButtonPrefab, choiceContainer);
        TMP_Text label = choiceButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = choiceText;

        if (useSpriteBackedChoiceLayout)
        {
            StyleLightChoiceButton(choiceButton, label, true);
            PositionSpriteBackedChoiceButton(choiceButton, createdChoiceCount);
            createdChoiceCount++;
        }
        else if (useLightStoryChoiceButtons)
        {
            StyleLightChoiceButton(choiceButton, label, false);
        }

        Button button = choiceButton.GetComponent<Button>();
        if (button != null)
        {
            SafeNetSfxPlayer.HookButton(button);
            button.onClick.AddListener(onClick);
        }
        return choiceButton;
    }

    private void SetChoicePanelVisible(bool visible)
    {
        if (choiceContainer == null || choiceContainer.gameObject == dialoguePanel)
            return;

        choiceContainer.gameObject.SetActive(visible);

        if (useSpriteBackedChoiceLayout)
            ApplySpriteBackedChoiceLayout(visible);
        else
            ApplyImmersiveChoiceLayout(visible);
    }

    private void CaptureImmersiveLayout()
    {
        if ((!useImmersiveChoiceLayout && !useSpriteBackedChoiceLayout && !useLightStoryChoiceButtons) || dialoguePanel == null ||
            dialogueText == null || choiceContainer == null)
            return;

        dialoguePanelRect = dialoguePanel.GetComponent<RectTransform>();
        dialogueTextRect = dialogueText.rectTransform;
        choicePanelRect = choiceContainer as RectTransform;
        dialoguePanelImage = dialoguePanel.GetComponent<Image>();
        choicePanelImage = choiceContainer.GetComponent<Image>();
        choiceLayoutGroup = choiceContainer.GetComponent<VerticalLayoutGroup>();
        originalChoiceLayoutGroupEnabled = choiceLayoutGroup != null && choiceLayoutGroup.enabled;

        if (dialoguePanelRect == null || dialogueTextRect == null || choicePanelRect == null)
            return;

        originalPanelPosition = dialoguePanelRect.anchoredPosition;
        originalPanelSize = dialoguePanelRect.sizeDelta;
        originalTextPosition = dialogueTextRect.anchoredPosition;
        originalTextSize = dialogueTextRect.sizeDelta;
        originalChoicePosition = choicePanelRect.anchoredPosition;
        originalChoiceSize = choicePanelRect.sizeDelta;
        originalTextAlignment = dialogueText.alignment;
        originalTextStyle = dialogueText.fontStyle;
        portraitWasActive = portraitImage != null && portraitImage.gameObject.activeSelf;
        nameWasActive = nameText != null && nameText.gameObject.activeSelf;
        textWasActive = dialogueText.gameObject.activeSelf;

        if (dialoguePanelImage != null)
        {
            originalPanelColor = dialoguePanelImage.color;
            originalPanelRaycastTarget = dialoguePanelImage.raycastTarget;
        }

        if (choicePanelImage != null)
        {
            originalChoicePanelColor = choicePanelImage.color;
            originalChoicePanelRaycastTarget = choicePanelImage.raycastTarget;
        }

        immersiveLayoutCaptured = true;
    }

    private void ApplyImmersiveChoiceLayout(bool visible)
    {
        if (!immersiveLayoutCaptured || immersiveLayoutVisible == visible)
            return;

        immersiveLayoutVisible = visible;

        if (visible)
        {
            if (useLightStoryChoiceButtons)
            {
                dialoguePanelRect.anchoredPosition = new Vector2(0f, 310f);
                dialoguePanelRect.sizeDelta = new Vector2(1260f, 500f);
                dialogueTextRect.anchoredPosition = new Vector2(0f, 145f);
                dialogueTextRect.sizeDelta = new Vector2(960f, 70f);
                choicePanelRect.anchoredPosition = new Vector2(0f, -60f);
                choicePanelRect.sizeDelta = new Vector2(960f, 240f);
            }
            else
            {
                dialoguePanelRect.anchoredPosition = new Vector2(0f, 270f);
                dialoguePanelRect.sizeDelta = new Vector2(1180f, 300f);
                dialogueTextRect.anchoredPosition = new Vector2(0f, 118f);
                dialogueTextRect.sizeDelta = new Vector2(1100f, 52f);
                choicePanelRect.anchoredPosition = new Vector2(0f, -30f);
                choicePanelRect.sizeDelta = new Vector2(1100f, 205f);
            }

            if (portraitImage != null) portraitImage.gameObject.SetActive(false);
            if (nameText != null) nameText.gameObject.SetActive(false);
            dialogueText.gameObject.SetActive(true);
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.fontStyle = FontStyles.Bold;

            if (useWhiteReadingSurface)
            {
                ApplyWhiteChoiceSurfaceLayout();
                dialogueText.color = new Color(0.015f, 0.035f, 0.05f, 1f);
                if (dialoguePanelImage != null)
                    dialoguePanelImage.color = Color.white;
                if (choicePanelImage != null)
                {
                    Color transparent = choicePanelImage.color;
                    transparent.a = 0f;
                    choicePanelImage.color = transparent;
                    choicePanelImage.raycastTarget = false;
                }
            }
            else if (dialoguePanelImage != null)
                dialoguePanelImage.color = new Color(0.01f, 0.025f, 0.045f, 0.94f);

            return;
        }

        RestoreOriginalChoiceLayout();
    }

    private void ApplySpriteBackedChoiceLayout(bool visible)
    {
        if (!immersiveLayoutCaptured || immersiveLayoutVisible == visible)
            return;

        immersiveLayoutVisible = visible;

        if (!visible)
        {
            RestoreOriginalChoiceLayout();
            return;
        }

        dialoguePanelRect.anchoredPosition = new Vector2(0f, 540f);
        dialoguePanelRect.sizeDelta = new Vector2(1920f, 1080f);
        choicePanelRect.anchoredPosition = preparedChoiceCount >= 3
            ? new Vector2(0f, -269f)
            : new Vector2(0f, -285f);
        choicePanelRect.sizeDelta = preparedChoiceCount >= 3
            ? new Vector2(820f, 238f)
            : new Vector2(700f, 171f);

        // Area 2 uses real visible buttons instead of invisible controls over an
        // image. Manual layout keeps every displayed row and touch area identical.
        if (choiceLayoutGroup != null)
            choiceLayoutGroup.enabled = false;

        if (portraitImage != null) portraitImage.gameObject.SetActive(false);
        if (nameText != null) nameText.gameObject.SetActive(false);
        dialogueText.gameObject.SetActive(false);

        if (dialoguePanelImage != null)
        {
            Color transparent = dialoguePanelImage.color;
            transparent.a = 0f;
            dialoguePanelImage.color = transparent;
            dialoguePanelImage.raycastTarget = false;
        }

        if (choicePanelImage != null)
        {
            Color transparent = choicePanelImage.color;
            transparent.a = 0f;
            choicePanelImage.color = transparent;
            choicePanelImage.raycastTarget = false;
        }
    }

    private void StyleLightChoiceButton(GameObject choiceButton, TMP_Text label, bool manualLayout)
    {
        Image image = choiceButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.97f, 0.99f, 1f, 1f);
            image.raycastTarget = true;

            Outline border = choiceButton.GetComponent<Outline>();
            if (border == null)
                border = choiceButton.AddComponent<Outline>();
            border.effectColor = new Color(0.015f, 0.045f, 0.065f, 1f);
            border.effectDistance = new Vector2(4f, -4f);
            border.useGraphicAlpha = true;
        }

        if (label != null)
        {
            label.color = new Color(0.015f, 0.045f, 0.065f, 1f);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = preparedChoiceCount >= 3 ? 30f : 32f;
            label.raycastTarget = false;
        }

        Button button = choiceButton.GetComponent<Button>();
        if (button != null)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.78f, 0.97f, 1f, 1f);
            colors.pressedColor = new Color(0.25f, 0.9f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.62f, 0.66f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        LayoutElement layout = choiceButton.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.enabled = !manualLayout;
            if (!manualLayout)
            {
                float rowHeight = preparedChoiceCount >= 3 ? 86f : 105f;
                layout.minHeight = rowHeight;
                layout.preferredHeight = rowHeight;
                layout.flexibleHeight = 0f;
            }
        }
    }

    private void PositionSpriteBackedChoiceButton(GameObject choiceButton, int choiceIndex)
    {
        RectTransform buttonRect = choiceButton.GetComponent<RectTransform>();
        if (buttonRect == null) return;

        bool hasThreeChoices = preparedChoiceCount >= 3;
        float rowHeight = hasThreeChoices ? 70f : 78f;
        float rowGap = hasThreeChoices ? 14f : 15f;
        float buttonWidth = hasThreeChoices ? 820f : 700f;
        float totalHeight = preparedChoiceCount * rowHeight +
                            Mathf.Max(0, preparedChoiceCount - 1) * rowGap;
        float rowY = totalHeight * 0.5f - rowHeight * 0.5f -
                     choiceIndex * (rowHeight + rowGap);

        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, rowY);
        buttonRect.sizeDelta = new Vector2(buttonWidth, rowHeight);
    }

    private void RestoreOriginalChoiceLayout()
    {
        dialoguePanelRect.anchoredPosition = originalPanelPosition;
        dialoguePanelRect.sizeDelta = originalPanelSize;
        dialogueTextRect.anchoredPosition = originalTextPosition;
        dialogueTextRect.sizeDelta = originalTextSize;
        choicePanelRect.anchoredPosition = originalChoicePosition;
        choicePanelRect.sizeDelta = originalChoiceSize;

        if (choiceLayoutGroup != null)
            choiceLayoutGroup.enabled = originalChoiceLayoutGroupEnabled;

        if (portraitImage != null) portraitImage.gameObject.SetActive(portraitWasActive);
        if (nameText != null) nameText.gameObject.SetActive(nameWasActive);
        dialogueText.gameObject.SetActive(textWasActive);
        dialogueText.alignment = originalTextAlignment;
        dialogueText.fontStyle = originalTextStyle;

        if (dialoguePanelImage != null)
        {
            dialoguePanelImage.color = originalPanelColor;
            dialoguePanelImage.raycastTarget = originalPanelRaycastTarget;
        }

        if (choicePanelImage != null)
        {
            choicePanelImage.color = originalChoicePanelColor;
            choicePanelImage.raycastTarget = originalChoicePanelRaycastTarget;
        }

        if (useWhiteReadingSurface)
        {
            ConfigureWhiteReadingSurface();
            dialogueTextRect.anchoredPosition = originalTextPosition;
            dialogueTextRect.sizeDelta = originalTextSize;
        }
    }

    private void ApplyWhiteChoiceSurfaceLayout()
    {
        if (whiteReadingSurface == null)
            ConfigureWhiteReadingSurface();
        if (whiteReadingSurface == null)
            return;

        RectTransform surfaceRect = whiteReadingSurface.rectTransform;
        surfaceRect.anchoredPosition = new Vector2(0f, -15f);
        surfaceRect.sizeDelta = new Vector2(1080f, 360f);
        whiteReadingSurface.color = new Color(0.985f, 0.995f, 1f, 0.99f);
    }
}
