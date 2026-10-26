using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the Tutorial presentation from the supplied Assets_Colored artwork
/// while preserving the scene's existing player, camera, mobile controls and HUD.
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class TutorialSceneBootstrap : MonoBehaviour
{
    private const float WorldWidth = 29f;
    private const float PlatformSurfaceY = -3.05f;
    private const float AegisHeight = 2.25f;
    private const float DoorHeight = 5.55f;

    [Header("Assets_Colored artwork")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite platformSprite;
    [SerializeField] private Sprite aegisSprite;
    [SerializeField] private Sprite unsafeDoorSprite;
    [SerializeField] private Sprite securedDoorSprite;

    private TutorialFlow flow;
    private PlayerMovement playerMovement;
    private Transform playerTransform;
    private Transform aegisTransform;
    private SpriteRenderer aegisRenderer;
    private SpriteRenderer doorRenderer;
    private BoxCollider2D doorCollider;
    private GameObject howToCanvas;
    private GameObject bannerCanvas;
    private GameObject bannerPanel;
    private TMP_Text bannerText;
    private TMP_FontAsset uiFont;
    private bool howToVisible;
    private bool aegisFollowingPlayer;
    private float fixedAegisX = -4.1f;

    private void Awake()
    {
        flow = GetComponent<TutorialFlow>();
        CacheUIFont();
        ConfigureWorldArtwork();
        CreateBannerUI();
        CreateHowToPlayUI();
    }

    private void Start()
    {
        if (flow != null)
            flow.SetObjectiveVisible(false);

        if (playerMovement != null)
            playerMovement.enabled = false;

        howToVisible = true;
        if (howToCanvas != null)
            howToCanvas.SetActive(true);
    }

    private void Update()
    {
        if (howToVisible && SafeNetDesktopInput.ContinuePressed)
        {
            DismissHowToPlay();
        }

        UpdateAegisPosition();
    }

    private void ConfigureWorldArtwork()
    {
        HideTutorialScoreUI();

        GameObject legacyPhone = FindSceneObject("Tutorial Phone");
        if (legacyPhone != null)
            legacyPhone.SetActive(false);

        ConfigureBackground();
        ConfigurePlatform();
        ConfigurePlayer();
        ConfigureAegis();
        ConfigureExitDoor();
    }

    private void HideTutorialScoreUI()
    {
        // The tutorial teaches controls and safe decision-making; it must not
        // add to, or visually advertise, the scored mission session.
        GameObject legacyScore = FindSceneObject("ScoreText");
        if (legacyScore != null)
            legacyScore.SetActive(false);
    }

    private void ConfigureBackground()
    {
        GameObject background = FindSceneObject("Tutorial Open World Background");
        if (background == null)
            background = new GameObject("Tutorial Colored Background");

        SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = background.AddComponent<SpriteRenderer>();

        renderer.sprite = backgroundSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = -20;
        background.name = "Tutorial Colored Background";
        background.transform.SetParent(null, true);

        if (backgroundSprite == null || backgroundSprite.bounds.size.x <= 0f)
            return;

        float scale = WorldWidth / backgroundSprite.bounds.size.x;
        float backgroundHeight = backgroundSprite.bounds.size.y * scale;
        background.transform.localScale = new Vector3(scale, scale, 1f);
        // The supplied slices use Unity's centered pivot even though the source
        // PNGs have transparent space around them.
        background.transform.position = new Vector3(
            0f,
            PlatformSurfaceY + backgroundHeight * 0.5f,
            1f);
    }

    private void ConfigurePlatform()
    {
        if (platformSprite == null || platformSprite.bounds.size.x <= 0f)
            return;

        GameObject platform = FindSceneObject("Tutorial Colored Platform");
        if (platform == null)
            platform = new GameObject("Tutorial Colored Platform");

        SpriteRenderer renderer = platform.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = platform.AddComponent<SpriteRenderer>();

        renderer.sprite = platformSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = -10;

        float scale = WorldWidth / platformSprite.bounds.size.x;
        float platformHeight = platformSprite.bounds.size.y * scale;
        platform.transform.localScale = new Vector3(scale, scale, 1f);
        platform.transform.position = new Vector3(
            0f,
            PlatformSurfaceY - platformHeight * 0.5f,
            0.5f);
    }

    private void ConfigurePlayer()
    {
        GameObject player = FindSceneObject("MainMC") ?? FindSceneObject("MC");
        if (player == null)
            return;

        playerTransform = player.transform;
        playerMovement = player.GetComponent<PlayerMovement>();
        playerTransform.position = new Vector3(-9.2f, playerTransform.position.y, playerTransform.position.z);

        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            float correction = PlatformSurfaceY - renderer.bounds.min.y;
            playerTransform.position += Vector3.up * correction;
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 5);
        }
    }

    private void ConfigureAegis()
    {
        GameObject aegis = FindSceneObject("Tutorial Professor");
        if (aegis == null)
            aegis = new GameObject("Aegis Tutorial Guide");

        aegis.name = "Aegis Tutorial Guide";
        aegis.SetActive(true);
        aegis.transform.SetParent(null, true);

        Animator legacyAnimator = aegis.GetComponent<Animator>();
        if (legacyAnimator != null)
        {
            legacyAnimator.enabled = false;
            Destroy(legacyAnimator);
        }

        aegisRenderer = aegis.GetComponent<SpriteRenderer>();
        if (aegisRenderer == null)
            aegisRenderer = aegis.AddComponent<SpriteRenderer>();
        aegisRenderer.sprite = aegisSprite;
        aegisRenderer.color = Color.white;
        aegisRenderer.sortingOrder = 4;

        if (aegisSprite != null && aegisSprite.bounds.size.y > 0f)
        {
            float scale = AegisHeight / aegisSprite.bounds.size.y;
            aegis.transform.localScale = new Vector3(scale, scale, 1f);
        }

        aegis.transform.position = new Vector3(
            fixedAegisX,
            PlatformSurfaceY + 0.55f + AegisHeight * 0.5f,
            0f);
        aegisTransform = aegis.transform;

        BoxCollider2D collider = aegis.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = aegis.AddComponent<BoxCollider2D>();
        collider.enabled = true;
        collider.isTrigger = true;
        if (aegisSprite != null)
        {
            collider.size = aegisSprite.bounds.size;
            collider.offset = aegisSprite.bounds.center;
        }

        TutorialAegisInteractable interaction = aegis.GetComponent<TutorialAegisInteractable>();
        if (interaction == null)
            interaction = aegis.AddComponent<TutorialAegisInteractable>();
        interaction.Configure(flow, this, aegisSprite);
    }

    private void ConfigureExitDoor()
    {
        GameObject door = FindSceneObject("Tutorial Exit Disabled - Use Professor") ??
                          FindSceneObject("Tutorial Secured Exit Door");
        if (door == null)
            door = new GameObject("Tutorial Secured Exit Door");

        door.name = "Tutorial Secured Exit Door";
        door.SetActive(true);
        door.transform.SetParent(null, true);

        doorRenderer = door.GetComponent<SpriteRenderer>();
        if (doorRenderer == null)
            doorRenderer = door.AddComponent<SpriteRenderer>();
        doorRenderer.color = Color.white;
        doorRenderer.sortingOrder = 2;

        doorCollider = door.GetComponent<BoxCollider2D>();
        if (doorCollider == null)
            doorCollider = door.AddComponent<BoxCollider2D>();
        doorCollider.enabled = true;
        doorCollider.isTrigger = true;

        if (door.GetComponent<TutorialExit>() == null)
            door.AddComponent<TutorialExit>();

        door.transform.position = new Vector3(9.1f, PlatformSurfaceY + DoorHeight * 0.5f, 0f);
        SetDoorSprite(unsafeDoorSprite);
    }

    private void SetDoorSprite(Sprite sprite)
    {
        if (doorRenderer == null || sprite == null)
            return;

        Vector3 position = doorRenderer.transform.position;
        doorRenderer.sprite = sprite;
        float scale = DoorHeight / sprite.bounds.size.y;
        doorRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        doorRenderer.transform.position = new Vector3(
            position.x,
            PlatformSurfaceY + DoorHeight * 0.5f,
            position.z);

        if (doorCollider != null)
        {
            // Use a ground-level interaction zone in world units. Transparent
            // sprite padding and the open-door artwork must not move the hitbox.
            doorCollider.size = new Vector2(4.2f / scale, 4.4f / scale);
            Vector3 zoneCenter = new Vector3(position.x, PlatformSurfaceY + 2.2f, position.z);
            doorCollider.offset = doorRenderer.transform.InverseTransformPoint(zoneCenter);
        }
    }

    private void CreateHowToPlayUI()
    {
        howToCanvas = CreateCanvasRoot("How To Play Canvas", 500);

        Image dimmer = CreateImage(howToCanvas.transform, "Dimmer", new Color(0f, 0.015f, 0.025f, 0.72f));
        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;

        Image panel = CreateImage(howToCanvas.transform, "How To Play Panel", new Color(0.015f, 0.04f, 0.065f, 0.96f));
        SetCenteredRect(panel.rectTransform, Vector2.zero, new Vector2(820f, 900f));
        panel.raycastTarget = true;
        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.2f, 0.88f, 1f, 0.9f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        CreateText(panel.transform, "HOW TO PLAY", new Vector2(0f, 365f), new Vector2(720f, 80f),
            48f, new Color(0.55f, 0.97f, 1f), FontStyles.Bold);
        CreateImageAt(panel.transform, "Title Divider", new Vector2(0f, 315f), new Vector2(500f, 4f),
            new Color(0.15f, 0.75f, 0.95f, 0.95f));

        CreateText(panel.transform, "MOVEMENT", new Vector2(0f, 265f), new Vector2(600f, 50f),
            30f, Color.white, FontStyles.Bold);

        Sprite leftSprite = FindUISprite("Move Left");
        Sprite rightSprite = FindUISprite("Move Right");
        CreateSpriteIcon(panel.transform, "Left Control Preview", leftSprite, new Vector2(-85f, 175f));
        CreateSpriteIcon(panel.transform, "Right Control Preview", rightSprite, new Vector2(85f, 175f));
        CreateText(panel.transform,
            "Press and hold LEFT or RIGHT to move. Release the button when you want to stop.",
            new Vector2(0f, 78f), new Vector2(680f, 100f), 24f,
            new Color(0.84f, 0.94f, 1f), FontStyles.Normal);

        CreateText(panel.transform, "INTERACT", new Vector2(0f, -10f), new Vector2(600f, 50f),
            30f, Color.white, FontStyles.Bold);
        Sprite interactSprite = FindUISprite("Interact");
        CreateSpriteIcon(panel.transform, "Interact Control Preview", interactSprite, new Vector2(0f, -105f));
        CreateText(panel.transform,
            "Move close to Aegis or a terminal, then tap INTERACT to talk or activate it.",
            new Vector2(0f, -205f), new Vector2(680f, 90f), 24f,
            new Color(0.84f, 0.94f, 1f), FontStyles.Normal);

        Button startButton = CreateButton(panel.transform, "Start Tutorial", "START",
            new Vector2(0f, -345f), new Vector2(360f, 82f));
        startButton.onClick.AddListener(DismissHowToPlay);
        SafeNetSfxPlayer.HookButton(startButton);
    }

    private void CreateBannerUI()
    {
        bannerCanvas = CreateCanvasRoot("Tutorial Story Banner Canvas", 450);
        bannerPanel = CreateImage(bannerCanvas.transform, "Story Banner",
            new Color(0.005f, 0.025f, 0.045f, 0.96f)).gameObject;
        RectTransform panelRect = bannerPanel.GetComponent<RectTransform>();
        SetCenteredRect(panelRect, new Vector2(0f, 80f), new Vector2(1240f, 120f));
        bannerPanel.GetComponent<Image>().raycastTarget = false;

        Outline outline = bannerPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.88f, 1f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);

        bannerText = CreateText(bannerPanel.transform, string.Empty, Vector2.zero,
            new Vector2(1160f, 90f), 34f, new Color(0.65f, 1f, 1f), FontStyles.Bold);
        bannerPanel.SetActive(false);
    }

    public IEnumerator ShowBannerAndWait(string message, float duration)
    {
        if (bannerPanel == null || bannerText == null)
            yield break;

        bannerText.text = message;
        bannerPanel.SetActive(true);
        yield return new WaitForSecondsRealtime(duration);
        bannerPanel.SetActive(false);
    }

    public void CompleteTutorial(bool choseSafeResponse)
    {
        if (flow == null || flow.PracticeCompleted)
            return;

        flow.CompletePractice(choseSafeResponse);
        flow.SetObjectiveVisible(true);
        SetDoorSprite(securedDoorSprite);
        aegisFollowingPlayer = true;
        StartCoroutine(ShowBannerAndWait("TUTORIAL COMPLETED, MISSION 1 IS OPEN.", 2.2f));
    }

    private void DismissHowToPlay()
    {
        if (!howToVisible)
            return;

        howToVisible = false;
        if (howToCanvas != null)
            howToCanvas.SetActive(false);
        if (playerMovement != null)
            playerMovement.enabled = true;

        if (flow != null)
        {
            flow.BeginTutorial();
            flow.SetObjectiveVisible(true);
        }
    }

    private void UpdateAegisPosition()
    {
        if (aegisTransform == null)
            return;

        float targetX = fixedAegisX;
        if (aegisFollowingPlayer && playerTransform != null)
            targetX = Mathf.Clamp(playerTransform.position.x + 1.8f, -7.2f, 7.4f);

        float hoverY = PlatformSurfaceY + 0.55f + AegisHeight * 0.5f +
                       Mathf.Sin(Time.unscaledTime * 2.1f) * 0.055f;
        Vector3 target = new Vector3(targetX, hoverY, aegisTransform.position.z);
        aegisTransform.position = Vector3.Lerp(
            aegisTransform.position,
            target,
            1f - Mathf.Exp(-5.5f * Time.unscaledDeltaTime));
    }

    private void CacheUIFont()
    {
        GameObject source = FindSceneObject("Objective Text");
        TMP_Text text = source != null ? source.GetComponent<TMP_Text>() : null;
        if (text != null)
            uiFont = text.font;
    }

    private GameObject CreateCanvasRoot(string objectName, int sortingOrder)
    {
        GameObject root = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return root;
    }

    private Image CreateImage(Transform parent, string objectName, Color color)
    {
        GameObject item = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Image CreateImageAt(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        Image image = CreateImage(parent, objectName, color);
        SetCenteredRect(image.rectTransform, position, size);
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text CreateText(
        Transform parent,
        string content,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style)
    {
        GameObject item = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
        text.text = content;
        if (uiFont != null)
            text.font = uiFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        SetCenteredRect(text.rectTransform, position, size);
        return text;
    }

    private void CreateSpriteIcon(Transform parent, string objectName, Sprite sprite, Vector2 position)
    {
        Image image = CreateImage(parent, objectName, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        SetCenteredRect(image.rectTransform, position, new Vector2(120f, 120f));

        if (sprite == null)
        {
            image.color = new Color(0.08f, 0.42f, 0.62f, 1f);
            CreateText(image.transform, "CONTROL", Vector2.zero, new Vector2(110f, 110f),
                18f, Color.white, FontStyles.Bold);
        }
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        string label,
        Vector2 position,
        Vector2 size)
    {
        Image image = CreateImage(parent, objectName, new Color(0.025f, 0.35f, 0.58f, 1f));
        SetCenteredRect(image.rectTransform, position, size);

        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.95f, 1f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.2f, 0.78f, 1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        CreateText(image.transform, label, Vector2.zero, size - new Vector2(28f, 14f),
            32f, Color.white, FontStyles.Bold);
        return button;
    }

    private Sprite FindUISprite(string objectName)
    {
        GameObject source = FindSceneObject(objectName);
        Image image = source != null ? source.GetComponent<Image>() : null;
        return image != null ? image.sprite : null;
    }

    private GameObject FindSceneObject(string objectName)
    {
        Scene scene = gameObject.scene;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
                if (candidate.name == objectName)
                    return candidate.gameObject;
        }

        return null;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
