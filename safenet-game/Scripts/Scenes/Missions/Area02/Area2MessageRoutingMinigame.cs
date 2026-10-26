using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mobile-friendly drag-and-drop simulation that follows Area 2's two
/// smishing conversations. Players route five messages to Safe or Report.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area2MessageRoutingMinigame : MonoBehaviour
{
    public static bool IsCompleted { get; private set; }
    public event Action Completed;

    private readonly List<Area2MessageCardDrag> cards = new();

    private Sprite instructionOverview;
    private Sprite dragInstruction;
    private Sprite phoneFrame;
    private Sprite timerFrame;
    private Sprite safeChannel;
    private Sprite reportChannel;
    private Sprite[] messageSprites;

    private Canvas simulationCanvas;
    private GameObject canvasObject;
    private GameObject introRoot;
    private GameObject gameplayRoot;
    private GameObject successRoot;
    private RectTransform safeTarget;
    private RectTransform reportTarget;
    private TMP_Text timerText;
    private TMP_Text progressText;
    private TMP_Text feedbackText;
    private Button retryButton;
    private PlayerMovement playerMovement;
    private bool playerMovementWasEnabled;
    private bool built;
    private bool running;
    private bool routingInstructionPlayed;
    private bool routingInstructionVoiceActive;
    private float timeRemaining;
    private int routedCount;

    private static readonly bool[] SafeAnswers =
    {
        false, // Unexpected prize
        true,  // Normal account-login security alert with no link request
        false, // Delivery fee link
        false, // Unsolicited urgency-based promotional spam
        true   // Known classmate
    };

    public static void ResetProgress()
    {
        IsCompleted = false;
    }

    public void Configure(
        Sprite overview,
        Sprite instruction,
        Sprite frame,
        Sprite timer,
        Sprite safe,
        Sprite report,
        Sprite[] messages)
    {
        instructionOverview = overview;
        dragInstruction = instruction;
        phoneFrame = frame;
        timerFrame = timer;
        safeChannel = safe;
        reportChannel = report;
        messageSprites = messages;
        VoiceLinePlayer.Preload("level2_routing_instruction");
    }

    public void Show()
    {
        if (IsCompleted)
            return;

        if (!built)
            BuildInterface();

        canvasObject.SetActive(true);
        introRoot.SetActive(true);
        gameplayRoot.SetActive(false);
        successRoot.SetActive(false);
        PausePlayerMovement();

        // Show() can be reached more than once while the same routing attempt
        // is active. Keep the spoken instructions tied to the first reveal so
        // retries and duplicate interaction taps do not restart the recording.
        if (!routingInstructionPlayed)
        {
            VoiceLinePlayer.Play("level2_routing_instruction");
            routingInstructionPlayed = true;
            routingInstructionVoiceActive = true;
        }
    }

    private void Update()
    {
        if (!running)
            return;

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.unscaledDeltaTime);
        UpdateTimerText();

        if (timeRemaining <= 0f)
            HandleTimeout();
    }

    internal float CanvasScaleFactor => simulationCanvas != null
        ? Mathf.Max(0.01f, simulationCanvas.scaleFactor)
        : 1f;

    internal bool CanDrag => running && !IsCompleted;

    internal bool TryRoute(Area2MessageCardDrag card, Vector2 screenPosition)
    {
        if (!CanDrag || card == null)
            return false;

        bool droppedOnSafe = RectTransformUtility.RectangleContainsScreenPoint(
            safeTarget, screenPosition, null);
        bool droppedOnReport = RectTransformUtility.RectangleContainsScreenPoint(
            reportTarget, screenPosition, null);

        if (!droppedOnSafe && !droppedOnReport)
            return false;

        bool selectedSafe = droppedOnSafe;
        if (selectedSafe != card.IsSafeMessage)
        {
            feedbackText.text = "WRONG CHANNEL! Review the sender and urgency, then try again.";
            feedbackText.color = new Color(1f, 0.25f, 0.28f, 1f);
            ApplyIntegrityDamage("message routed to the wrong channel");
            return false;
        }

        card.MarkRouted();
        routedCount++;
        feedbackText.text = selectedSafe
            ? $"SAFE CHANNEL confirmed  •  {routedCount}/5 routed"
            : $"REPORT CHANNEL confirmed  •  {routedCount}/5 routed";
        feedbackText.color = selectedSafe
            ? new Color(0.35f, 1f, 0.5f, 1f)
            : new Color(1f, 0.52f, 0.3f, 1f);

        if (routedCount >= cards.Count)
        {
            CompleteSimulation();
        }
        else
        {
            cards[routedCount].gameObject.SetActive(true);
            UpdateProgressText();
        }

        return true;
    }

    private void BuildInterface()
    {
        built = true;
        canvasObject = new GameObject(
            "Area 2 Message Routing Simulation",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);

        simulationCanvas = canvasObject.GetComponent<Canvas>();
        simulationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        simulationCanvas.sortingOrder = 90;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        introRoot = CreateContainer("Routing Instructions", canvasObject.transform);
        gameplayRoot = CreateContainer("Routing Challenge", canvasObject.transform);
        successRoot = CreateContainer("Routing Complete", canvasObject.transform);

        BuildIntro();
        BuildChallenge();
        BuildSuccessPanel();
        gameplayRoot.SetActive(false);
        successRoot.SetActive(false);
    }

    private void BuildIntro()
    {
        CreateText(
            "MESSAGE ROUTING SIMULATION",
            introRoot.transform,
            new Vector2(980f, 100f),
            new Vector2(0f, 430f),
            54f,
            new Color(0.35f, 0.95f, 1f, 1f));

        Image overview = CreateImage(
            "Classification Instructions",
            introRoot.transform,
            instructionOverview,
            new Vector2(980f, 700f),
            new Vector2(0f, 25f),
            false);
        overview.preserveAspect = true;

        CreateButton(
            "Begin Routing",
            introRoot.transform,
            new Vector2(460f, 90f),
            new Vector2(0f, -420f),
            "START SIMULATION",
            StartChallenge);
    }

    private void BuildChallenge()
    {
        Image frame = CreateImage(
            "Phone Frame",
            gameplayRoot.transform,
            phoneFrame,
            new Vector2(780f, 1060f),
            Vector2.zero,
            false);
        frame.preserveAspect = true;

        Image header = CreateImage(
            "Message Routing Header",
            gameplayRoot.transform,
            timerFrame,
            new Vector2(620f, 126f),
            new Vector2(0f, 400f),
            false);
        header.preserveAspect = true;

        timerText = CreateText(
            "01:30",
            gameplayRoot.transform,
            new Vector2(130f, 66f),
            new Vector2(235f, 400f),
            35f,
            new Color(0.03f, 0.12f, 0.2f, 1f));

        Image instruction = CreateImage(
            "Drag Instruction",
            gameplayRoot.transform,
            dragInstruction,
            new Vector2(620f, 142f),
            new Vector2(0f, 285f),
            false);
        instruction.preserveAspect = true;

        progressText = CreateText(
            "MESSAGE 1 OF 5",
            gameplayRoot.transform,
            new Vector2(600f, 48f),
            new Vector2(0f, 190f),
            27f,
            new Color(0.4f, 0.9f, 1f, 1f));

        cards.Clear();
        int messageCount = Mathf.Min(5, messageSprites != null ? messageSprites.Length : 0);
        for (int index = 0; index < messageCount; index++)
        {
            Image cardImage = CreateImage(
                $"Incoming Message {index + 1}",
                gameplayRoot.transform,
                messageSprites[index],
                new Vector2(600f, 150f),
                new Vector2(0f, 85f),
                true);
            cardImage.preserveAspect = true;

            CanvasGroup group = cardImage.gameObject.AddComponent<CanvasGroup>();
            Area2MessageCardDrag drag = cardImage.gameObject.AddComponent<Area2MessageCardDrag>();
            drag.Initialize(this, SafeAnswers[index], group);
            cards.Add(drag);
        }

        feedbackText = CreateText(
            "Route every message to the correct channel.",
            gameplayRoot.transform,
            new Vector2(600f, 72f),
            new Vector2(0f, -395f),
            23f,
            new Color(0.75f, 0.9f, 1f, 1f));

        Image safe = CreateImage(
            "Safe Channel Drop Zone",
            gameplayRoot.transform,
            safeChannel,
            new Vector2(250f, 210f),
            new Vector2(-145f, -215f),
            true);
        safe.preserveAspect = true;
        safeTarget = safe.rectTransform;

        Image report = CreateImage(
            "Report Channel Drop Zone",
            gameplayRoot.transform,
            reportChannel,
            new Vector2(250f, 210f),
            new Vector2(145f, -215f),
            true);
        report.preserveAspect = true;
        reportTarget = report.rectTransform;

        retryButton = CreateButton(
            "Retry Routing",
            gameplayRoot.transform,
            new Vector2(300f, 70f),
            new Vector2(0f, -430f),
            "RETRY",
            StartChallenge);
        retryButton.gameObject.SetActive(false);
    }

    private void BuildSuccessPanel()
    {
        Image frame = CreateImage(
            "Completed Phone Frame",
            successRoot.transform,
            phoneFrame,
            new Vector2(780f, 1060f),
            Vector2.zero,
            false);
        frame.preserveAspect = true;

        Image panel = CreateImage(
            "Completion Panel",
            successRoot.transform,
            null,
            new Vector2(600f, 560f),
            Vector2.zero,
            false);
        panel.color = new Color(0.025f, 0.15f, 0.18f, 0.98f);

        CreateText(
            "✓  ROUTING COMPLETE",
            successRoot.transform,
            new Vector2(570f, 110f),
            new Vector2(0f, 155f),
            48f,
            new Color(0.3f, 1f, 0.55f, 1f));

        CreateText(
            "5 / 5 messages classified\nAll communication channels are secure.",
            successRoot.transform,
            new Vector2(550f, 180f),
            new Vector2(0f, 20f),
            31f,
            Color.white);

        CreateButton(
            "Continue Area 2",
            successRoot.transform,
            new Vector2(420f, 90f),
            new Vector2(0f, -175f),
            "CONTINUE",
            CloseSimulation);
    }

    private void StartChallenge()
    {
        introRoot.SetActive(false);
        gameplayRoot.SetActive(true);
        successRoot.SetActive(false);
        retryButton.gameObject.SetActive(false);
        routedCount = 0;
        timeRemaining = 90f;
        running = true;
        feedbackText.text = "Route every message to the correct channel.";
        feedbackText.color = new Color(0.75f, 0.9f, 1f, 1f);

        for (int index = 0; index < cards.Count; index++)
        {
            cards[index].ResetForRetry();
            cards[index].gameObject.SetActive(index == 0);
        }

        UpdateTimerText();
        UpdateProgressText();
    }

    private void HandleTimeout()
    {
        // Update can observe zero for more than one frame. Stop the attempt
        // before applying damage so one expired timer can never be counted
        // more than once.
        if (!running)
            return;

        running = false;
        feedbackText.text = "TIME EXPIRED — retry the five messages.";
        feedbackText.color = new Color(1f, 0.3f, 0.32f, 1f);
        retryButton.gameObject.SetActive(true);
        ApplyIntegrityDamage("message-routing timer expired");
    }

    private static void ApplyIntegrityDamage(string reason)
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning(
                $"Area 2 could not record a wrong answer because GameManager is unavailable ({reason}).");
            return;
        }

        Debug.Log($"Area 2 wrong answer: {reason}.");
        manager.TakeDamage();
    }

    private void CompleteSimulation()
    {
        running = false;
        StopRoutingInstructionVoice();
        IsCompleted = true;
        gameplayRoot.SetActive(false);
        successRoot.SetActive(true);
        Completed?.Invoke();
    }

    private void CloseSimulation()
    {
        running = false;
        StopRoutingInstructionVoice();
        canvasObject.SetActive(false);
        RestorePlayerMovement();
    }

    private void StopRoutingInstructionVoice()
    {
        if (!routingInstructionVoiceActive)
            return;

        VoiceLinePlayer.Stop();
        routingInstructionVoiceActive = false;
    }

    private void UpdateTimerText()
    {
        if (timerText == null)
            return;

        int totalSeconds = Mathf.CeilToInt(timeRemaining);
        timerText.text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        timerText.color = totalSeconds <= 15
            ? new Color(1f, 0.22f, 0.25f, 1f)
            : new Color(0.03f, 0.12f, 0.2f, 1f);
    }

    private void UpdateProgressText()
    {
        if (progressText != null)
            progressText.text = $"MESSAGE {Mathf.Min(routedCount + 1, cards.Count)} OF {cards.Count}";
    }

    private void PausePlayerMovement()
    {
        // Only one controllable character exists in a gameplay scene.
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        if (playerMovement == null)
            return;

        playerMovementWasEnabled = playerMovement.enabled;
        playerMovement.enabled = false;
        if (playerMovement.rb != null)
            playerMovement.rb.linearVelocity = Vector2.zero;
    }

    private void RestorePlayerMovement()
    {
        if (playerMovement != null)
            playerMovement.enabled = playerMovementWasEnabled;
    }

    private void OnDestroy()
    {
        StopRoutingInstructionVoice();
        RestorePlayerMovement();
    }

    private static GameObject CreateContainer(string objectName, Transform parent)
    {
        GameObject container = new GameObject(objectName, typeof(RectTransform));
        container.layer = 5;
        container.transform.SetParent(parent, false);
        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return container;
    }

    private static Image CreateImage(
        string objectName,
        Transform parent,
        Sprite sprite,
        Vector2 size,
        Vector2 position,
        bool raycastTarget)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = raycastTarget;
        image.color = Color.white;
        return image;
    }

    private static TMP_Text CreateText(
        string value,
        Transform parent,
        Vector2 size,
        Vector2 position,
        float fontSize,
        Color color)
    {
        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        Vector2 size,
        Vector2 position,
        string label,
        UnityEngine.Events.UnityAction onClick)
    {
        Image image = CreateImage(objectName, parent, null, size, position, true);
        image.color = new Color(0.04f, 0.42f, 0.62f, 0.98f);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.1f, 0.75f, 0.9f, 1f);
        colors.pressedColor = new Color(0.02f, 0.28f, 0.48f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);
        SafeNetSfxPlayer.HookButton(button);

        TMP_Text text = CreateText(
            label,
            button.transform,
            size,
            Vector2.zero,
            31f,
            Color.white);
        text.fontStyle = FontStyles.Bold;
        return button;
    }
}

/// <summary>Drag behavior for a single message card.</summary>
public sealed class Area2MessageCardDrag : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private Area2MessageRoutingMinigame owner;
    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Vector2 homePosition;
    private int homeSiblingIndex;
    private bool dragging;
    private int activePointerId = int.MinValue;

    public bool IsSafeMessage { get; private set; }

    public void Initialize(
        Area2MessageRoutingMinigame minigame,
        bool isSafeMessage,
        CanvasGroup group)
    {
        owner = minigame;
        IsSafeMessage = isSafeMessage;
        canvasGroup = group;
        rect = transform as RectTransform;
        homePosition = rect != null ? rect.anchoredPosition : Vector2.zero;
        homeSiblingIndex = transform.GetSiblingIndex();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // A card accepts only the pointer that began its current drag. This
        // prevents two fingers from producing duplicate OnEndDrag penalties
        // for one visible routing action on a phone.
        if (dragging || owner == null || !owner.CanDrag || rect == null)
            return;

        dragging = true;
        activePointerId = eventData.pointerId;
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();
        transform.localScale = Vector3.one * 1.04f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.pointerId != activePointerId || rect == null)
            return;

        rect.anchoredPosition += eventData.delta / owner.CanvasScaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || eventData.pointerId != activePointerId)
            return;

        dragging = false;
        activePointerId = int.MinValue;
        canvasGroup.blocksRaycasts = true;
        transform.localScale = Vector3.one;

        if (!owner.TryRoute(this, eventData.position))
            ReturnHome();
    }

    public void MarkRouted()
    {
        dragging = false;
        activePointerId = int.MinValue;
        gameObject.SetActive(false);
    }

    public void ResetForRetry()
    {
        dragging = false;
        activePointerId = int.MinValue;
        gameObject.SetActive(true);
        transform.SetSiblingIndex(homeSiblingIndex);
        transform.localScale = Vector3.one;
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = true;
        ReturnHome();
    }

    private void ReturnHome()
    {
        if (rect != null)
            rect.anchoredPosition = homePosition;
    }
}
