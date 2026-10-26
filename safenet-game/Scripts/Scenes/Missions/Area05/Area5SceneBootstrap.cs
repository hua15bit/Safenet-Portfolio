using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds Area 5 as the personal-data protection mission while preserving the
/// persistent GameManager, integrity, score, player controller, pause menu, and
/// mobile controls inherited from the earlier missions.
/// </summary>
[DefaultExecutionOrder(-55)]
[DisallowMultipleComponent]
public sealed class Area5SceneBootstrap : MonoBehaviour
{
    private const string NextSceneName = "Game(area6)";

    public enum NpcState
    {
        Corrupted,
        Blocked,
        Safe,
        Questioning
    }

    [Header("Saved Scene References")]
    [SerializeField] private GameObject privacyNpc;
    [SerializeField] private Transform privacyObstacle;
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("Level 5 NPC State Sheets")]
    [SerializeField] private Texture2D corruptedNpcSheet;
    [SerializeField] private Texture2D blockedNpcSheet;
    [SerializeField] private Texture2D safeNpcSheet;
    [SerializeField] private Texture2D questioningNpcSheet;

    [Header("Level 5 Interface Artwork")]
    [SerializeField] private Sprite obstacleSprite;
    [SerializeField] private Sprite completionSprite;
    [SerializeField] private Sprite continueButtonSprite;

    private PlayerMovement playerMovement;
    private SpriteRenderer playerRenderer;
    private SpriteRenderer npcRenderer;
    private Area5NpcAnimator npcAnimator;
    private DialogueController dialogueController;
    private GameObject completionCanvas;
    private bool completed;

    public Sprite PlayerPortrait =>
        playerRenderer != null ? playerRenderer.sprite : null;
    public Sprite NpcPortrait =>
        npcAnimator != null ? npcAnimator.CurrentSprite :
        npcRenderer != null ? npcRenderer.sprite : null;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "Game(area5)")
        {
            enabled = false;
            return;
        }

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        ConfigureBackground();
        ConfigureExitDoor();
        ConfigureDialogueUi();
        ConfigureObstacle();
        ConfigureNpc();
        CreateCompletionOverlay();
    }

    private bool ValidateReferences()
    {
        GameObject player = GameObject.FindGameObjectWithTag("MC");
        playerMovement = player != null ? player.GetComponent<PlayerMovement>() : null;
        playerRenderer = player != null ? player.GetComponent<SpriteRenderer>() : null;
        dialogueController = FindSceneComponent<DialogueController>();

        bool valid = privacyNpc != null && privacyObstacle != null &&
                     playerMovement != null && playerRenderer != null &&
                     dialogueController != null && corruptedNpcSheet != null &&
                     blockedNpcSheet != null && safeNpcSheet != null &&
                     questioningNpcSheet != null && obstacleSprite != null &&
                     completionSprite != null && continueButtonSprite != null;
        if (!valid)
            Debug.LogError("Area 5 setup is incomplete. Re-run SafeNet/Rebuild Area 5 Privacy Mission.", this);
        return valid;
    }

    private void ConfigureBackground()
    {
        if (backgroundRenderer == null)
            return;
        backgroundRenderer.enabled = true;
        backgroundRenderer.sortingOrder = -20;
        backgroundRenderer.color = Color.white;
    }

    private void ConfigureExitDoor()
    {
        GameObject door = GameObject.Find("Door");
        if (door == null)
            return;

        Level2 legacyExit = door.GetComponent<Level2>();
        if (legacyExit != null)
            legacyExit.enabled = false;
        Collider2D doorCollider = door.GetComponent<Collider2D>();
        if (doorCollider != null)
            doorCollider.enabled = false;
    }

    private void ConfigureDialogueUi()
    {
        dialogueController.useImmersiveChoiceLayout = false;
        dialogueController.useSpriteBackedChoiceLayout = false;
        dialogueController.useLightStoryChoiceButtons = true;
        dialogueController.ConfigureDefaultDialogueSurface();

        // Old Area 5 art left a world SpriteRenderer under the screen-space
        // dialogue panel. In play mode it rendered as a tiny sprite floating
        // high above the characters. Dialogue canvases use UI Images, so any
        // SpriteRenderer under this panel is stale scene decoration.
        SpriteRenderer[] legacyRenderers = dialogueController.dialoguePanel
            .GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer legacyRenderer in legacyRenderers)
        {
            legacyRenderer.gameObject.SetActive(false);
            Destroy(legacyRenderer.gameObject);
        }
    }

    private void ConfigureObstacle()
    {
        SpriteRenderer renderer = privacyObstacle.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = privacyObstacle.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = obstacleSprite;
        renderer.sortingOrder = 3;
        renderer.color = Color.white;

        Area5AutoVault vault = playerMovement.GetComponent<Area5AutoVault>();
        if (vault == null)
            vault = playerMovement.gameObject.AddComponent<Area5AutoVault>();
        vault.Configure(playerMovement, playerMovement.rb, privacyObstacle);
    }

    private void ConfigureNpc()
    {
        npcRenderer = privacyNpc.GetComponent<SpriteRenderer>();
        if (npcRenderer == null)
            npcRenderer = privacyNpc.AddComponent<SpriteRenderer>();
        npcRenderer.sortingOrder = 4;
        npcRenderer.color = Color.white;

        npcAnimator = privacyNpc.GetComponent<Area5NpcAnimator>();
        if (npcAnimator == null)
            npcAnimator = privacyNpc.AddComponent<Area5NpcAnimator>();
        npcAnimator.Configure(npcRenderer, safeNpcSheet);
        FitNpcToFloor(3.15f);

        BoxCollider2D interactionCollider = privacyNpc.GetComponent<BoxCollider2D>();
        if (interactionCollider == null)
            interactionCollider = privacyNpc.AddComponent<BoxCollider2D>();
        // The active NPC is a world obstacle as well as an interaction target.
        // The interaction disables this collider when the scenario finishes,
        // allowing the player to continue without ever walking through them.
        interactionCollider.isTrigger = false;
        interactionCollider.size = new Vector2(2.2f, 3.2f);
        interactionCollider.offset = new Vector2(0f, 1.55f);

        Area5PrivacyInteraction interaction = privacyNpc.GetComponent<Area5PrivacyInteraction>();
        if (interaction == null)
            interaction = privacyNpc.AddComponent<Area5PrivacyInteraction>();
        interaction.Configure(this, interactionCollider);
    }

    private void FitNpcToFloor(float targetHeight)
    {
        if (npcRenderer == null || npcRenderer.sprite == null || playerRenderer == null)
            return;

        float scale = targetHeight / Mathf.Max(0.01f, npcRenderer.sprite.bounds.size.y);
        privacyNpc.transform.localScale = new Vector3(scale, scale, 1f);
        Vector3 position = privacyNpc.transform.position;
        position.y = playerRenderer.bounds.min.y - npcRenderer.sprite.bounds.min.y * scale;
        privacyNpc.transform.position = position;
    }

    public void SetNpcState(NpcState state)
    {
        // Keep one stable character appearance throughout the conversation.
        // Swapping animation sheets on the first tap looked like the NPC had
        // unexpectedly transformed instead of simply starting dialogue.
        if (npcRenderer != null)
            npcRenderer.color = Color.white;
    }

    public void CompleteMission(bool correct)
    {
        if (completed)
            return;
        completed = true;

        dialogueController.ShowDialogueUI(false);
        if (npcRenderer != null)
            npcRenderer.enabled = false;
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            if (playerMovement.rb != null)
                playerMovement.rb.linearVelocity = Vector2.zero;
        }

        int missionScore = GameManager.Instance != null
            ? GameManager.Instance.CurrentMissionScore
            : correct ? 5 : 0;
        SafeNetApiClient.Instance?.SaveMission(
            "area5",
            missionScore,
            true,
            SafeNetConstants.GradedMissionCount);

        if (completionCanvas != null)
            completionCanvas.SetActive(true);
    }

    private void CreateCompletionOverlay()
    {
        completionCanvas = new GameObject(
            "Area 5 Mission Complete Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        completionCanvas.transform.SetParent(transform, false);

        Canvas canvas = completionCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = completionCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image dimmer = CreateImage(
            completionCanvas.transform,
            "Completion Focus Backdrop",
            null,
            Vector2.zero,
            new Vector2(1920f, 1080f));
        dimmer.color = new Color(0.005f, 0.015f, 0.035f, 0.82f);

        Image completion = CreateImage(
            completionCanvas.transform,
            "Personal Data Protected",
            completionSprite,
            new Vector2(0f, 80f),
            new Vector2(1160f, 580f));
        completion.preserveAspect = true;
        completion.raycastTarget = false;
        completion.color = new Color(0.3f, 0.95f, 1f, 1f);
        Outline completionGlow = completion.gameObject.AddComponent<Outline>();
        completionGlow.effectColor = new Color(0.08f, 0.55f, 1f, 0.95f);
        completionGlow.effectDistance = new Vector2(5f, -5f);

        Image continueImage = CreateImage(
            completionCanvas.transform,
            "Continue To Area 6",
            continueButtonSprite,
            new Vector2(0f, -285f),
            new Vector2(520f, 150f));
        continueImage.preserveAspect = true;
        continueImage.color = new Color(0.25f, 1f, 0.68f, 1f);
        Outline continueGlow = continueImage.gameObject.AddComponent<Outline>();
        continueGlow.effectColor = new Color(0.05f, 0.65f, 0.85f, 1f);
        continueGlow.effectDistance = new Vector2(4f, -4f);
        Button continueButton = continueImage.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = continueImage;
        continueButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = continueButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.78f, 1f, 0.9f, 1f);
        colors.pressedColor = new Color(0.45f, 0.9f, 0.72f, 1f);
        continueButton.colors = colors;
        continueButton.onClick.AddListener(ContinueToArea6);
        SafeNetSfxPlayer.HookButton(continueButton);

        completionCanvas.SetActive(false);
    }

    private static Image CreateImage(
        Transform parent,
        string objectName,
        Sprite sprite,
        Vector2 position,
        Vector2 size)
    {
        GameObject item = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        item.transform.SetParent(parent, false);
        Image image = item.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    // Area 5 is not the final mission. Its completion overlay must hand the
    // persistent gameplay session to Area 6; Area 6 owns the Game Complete exit.
    private void ContinueToArea6()
    {
        if (!Application.CanStreamedLevelBeLoaded(NextSceneName))
        {
            Debug.LogError(
                $"Cannot continue from Area 5 because '{NextSceneName}' is missing from Build Settings.",
                this);
            return;
        }

        SceneManager.LoadScene(NextSceneName);
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }
}
