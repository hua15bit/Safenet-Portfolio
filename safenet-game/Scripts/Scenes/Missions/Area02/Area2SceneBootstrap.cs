using UnityEngine;

/// <summary>
/// Builds Area 2's communications-city set dressing and the shared phone
/// presentation used by both smishing encounters.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area2SceneBootstrap : MonoBehaviour
{
    [Header("Existing Prize Scenario")]
    [SerializeField] private Area2StoryboardVisuals prizeScenario;
    [SerializeField] private NPC1 deliveryDialogue;

    [Header("Phone Story Artwork")]
    [SerializeField] private Sprite prizePhone;
    [SerializeField] private Sprite deliveryPhone;
    [SerializeField] private Sprite prizeChoices;
    [SerializeField] private Sprite deliveryChoices;
    [SerializeField] private Sprite prizeCorrect;
    [SerializeField] private Sprite prizeIncorrect;
    [SerializeField] private Sprite deliveryCorrect;
    [SerializeField] private Sprite deliveryIncorrect;

    [Header("Communications City Props")]
    [SerializeField] private Sprite deliveryStationSprite;
    [SerializeField] private Sprite leftTowerSprite;
    [SerializeField] private Sprite centerTowerSprite;

    [Header("Message Routing Simulation")]
    [SerializeField] private Sprite routingInstructions;
    [SerializeField] private Sprite dragInstructions;
    [SerializeField] private Sprite routingPhoneFrame;
    [SerializeField] private Sprite routingTimer;
    [SerializeField] private Sprite safeChannel;
    [SerializeField] private Sprite reportChannel;
    [SerializeField] private Sprite[] routingMessages;

    private Dialogue1 prizeInteraction;
    private Dialogue1 deliveryInteraction;
    private BoxCollider2D deliveryTrigger;
    private SpriteRenderer deliveryRenderer;
    private Area2MessageRoutingMinigame routingMinigame;
    private bool prizeFinished;
    private bool deliveryFinished;

    private const int PhoneSortingOrder = 30;
    private const int PanelSortingOrder = 31;

    private void Awake()
    {
        VoiceLinePlayer.Preload(
            "level2_delivery_01_arrival",
            "level2_delivery_02_message",
            "level2_delivery_03_correct",
            "level2_delivery_04_correct_advice",
            "level2_delivery_05_unsafe",
            "level2_delivery_06_unsafe_advice");

        Area2MessageRoutingMinigame.ResetProgress();

        if (GameManager.Instance != null)
            GameManager.Instance.scoreRequiredToPass = 10;

        SpriteRenderer phoneOverlay = CreateRenderer(
            "Area 2 Phone Story Overlay",
            null,
            new Vector3(0.49f, 0.1f, 0f),
            PhoneSortingOrder);

        SpriteRenderer panelOverlay = CreateRenderer(
            "Area 2 Choice Artwork",
            null,
            new Vector3(0.49f, -3.25f, 0f),
            PanelSortingOrder);

        phoneOverlay.enabled = false;
        panelOverlay.enabled = false;

        if (prizeScenario != null)
        {
            prizeScenario.Configure(
                Area2StoryboardVisuals.ScenarioType.PrizeMessage,
                phoneOverlay,
                panelOverlay,
                prizePhone,
                prizeChoices,
                prizeCorrect,
                prizeIncorrect,
                13.5f,
                13.5f);
        }

        CreateDeliveryScenario(phoneOverlay, panelOverlay);
        CreateCityProp("Left Phone Tower", leftTowerSprite, new Vector3(-5.2f, -4.49f, 0f));
        CreateCityProp("Center Phone Tower", centerTowerSprite, new Vector3(3.5f, -4.34f, 0f));
        CreateRoutingMinigame();

        prizeInteraction = prizeScenario != null
            ? prizeScenario.GetComponent<Dialogue1>()
            : null;
        if (prizeInteraction != null)
            prizeInteraction.DialogueEnded += HandlePrizeDialogueEnded;

        if (deliveryInteraction != null)
            deliveryInteraction.DialogueEnded += HandleDeliveryDialogueEnded;

        SetDeliveryLocked(true);
    }

    private void CreateDeliveryScenario(SpriteRenderer phoneOverlay, SpriteRenderer panelOverlay)
    {
        GameObject station = new GameObject("Delivery Phone Station");
        station.transform.position = new Vector3(7.2f, -4.58f, 0f);

        deliveryRenderer = station.AddComponent<SpriteRenderer>();
        deliveryRenderer.sprite = deliveryStationSprite;
        deliveryRenderer.sortingOrder = 3;

        deliveryTrigger = station.AddComponent<BoxCollider2D>();
        deliveryTrigger.isTrigger = true;
        deliveryTrigger.size = deliveryStationSprite != null
            ? deliveryStationSprite.bounds.size
            : new Vector2(3.75f, 4.85f);
        deliveryTrigger.offset = deliveryStationSprite != null
            ? (Vector2)deliveryStationSprite.bounds.center
            : new Vector2(1.875f, 2.425f);

        deliveryInteraction = station.AddComponent<Dialogue1>();
        deliveryInteraction.dialogueData = deliveryDialogue;

        Area2StoryboardVisuals visuals = station.AddComponent<Area2StoryboardVisuals>();
        visuals.Configure(
            Area2StoryboardVisuals.ScenarioType.DeliveryFee,
            phoneOverlay,
            panelOverlay,
            deliveryPhone,
            deliveryChoices,
            deliveryCorrect,
            deliveryIncorrect,
            13.5f,
            11.5f);
    }

    private void CreateRoutingMinigame()
    {
        GameObject minigameObject = new GameObject("Area 2 Message Routing Controller");
        routingMinigame = minigameObject.AddComponent<Area2MessageRoutingMinigame>();
        routingMinigame.Configure(
            routingInstructions,
            dragInstructions,
            routingPhoneFrame,
            routingTimer,
            safeChannel,
            reportChannel,
            routingMessages);
    }

    private void HandlePrizeDialogueEnded(Dialogue1 dialogue)
    {
        if (prizeFinished)
            return;

        prizeFinished = true;
        SetDeliveryLocked(false);
    }

    private void HandleDeliveryDialogueEnded(Dialogue1 dialogue)
    {
        if (deliveryFinished)
            return;

        deliveryFinished = true;
        routingMinigame?.Show();
    }

    private void SetDeliveryLocked(bool locked)
    {
        if (deliveryTrigger != null)
            deliveryTrigger.enabled = !locked;
        if (deliveryInteraction != null)
            deliveryInteraction.enabled = !locked;
        if (deliveryRenderer != null)
            deliveryRenderer.color = locked
                ? new Color(0.36f, 0.44f, 0.5f, 0.55f)
                : Color.white;
    }

    private void OnDestroy()
    {
        if (prizeInteraction != null)
            prizeInteraction.DialogueEnded -= HandlePrizeDialogueEnded;
        if (deliveryInteraction != null)
            deliveryInteraction.DialogueEnded -= HandleDeliveryDialogueEnded;
    }

    private static SpriteRenderer CreateCityProp(string objectName, Sprite sprite, Vector3 position)
    {
        if (sprite == null)
            return null;

        return CreateRenderer(objectName, sprite, position, 1);
    }

    private static SpriteRenderer CreateRenderer(
        string objectName,
        Sprite sprite,
        Vector3 position,
        int sortingOrder)
    {
        GameObject storyObject = new GameObject(objectName);
        storyObject.transform.position = position;

        SpriteRenderer renderer = storyObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }
}
