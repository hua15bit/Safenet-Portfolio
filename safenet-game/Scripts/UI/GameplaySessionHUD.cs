using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates the persistent gameplay score display and pause menu for the tutorial
/// and mission scenes, and safely restores time and audio when play resumes.
/// </summary>
public sealed class GameplaySessionHUD : MonoBehaviour
{
    private SafeNetPauseTheme theme;
    private GameObject pauseModal;
    private Button pauseButton;
    private TMP_Text pointsText;
    private TMP_Text modalPointsText;
    private TMP_Text statusText;
    private RectTransform frameRect;
    private bool paused;
    private float previousTimeScale = 1f;
    private int displayedPoints = int.MinValue;
    private GameObject pauseHome;
    private GameObject audioSettings;
    private readonly Image[] controlSizePresetImages = new Image[3];
    private TMP_Text controlSizeStatus;
    private bool showScore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool gameplay = scene.name == "Tutorial" ||
                        scene.name.StartsWith("Game(area", System.StringComparison.Ordinal);
        if (!gameplay || FindAnyObjectByType<GameplaySessionHUD>() != null) return;
        new GameObject("SafeNet Gameplay Pause System").AddComponent<GameplaySessionHUD>();
    }

    private void Awake()
    {
        theme = Resources.Load<SafeNetPauseTheme>("SafeNetPauseTheme");
        BuildInterface();
        EnsureEventSystem();
    }

    private void Update()
    {
        if (SafeNetDesktopInput.PausePressed)
        {
            SafeNetSfxPlayer.PlayButton();
            if (paused) ResumeGame();
            else PauseGame();
        }

        UpdatePoints();
        if (!paused) return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.8f);
        if (statusText != null)
        {
            Color color = statusText.color;
            color.a = Mathf.Lerp(0.62f, 1f, pulse);
            statusText.color = color;
        }
        if (frameRect != null)
        {
            float scale = Mathf.Lerp(0.997f, 1.004f, pulse);
            frameRect.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void BuildInterface()
    {
        showScore = SceneManager.GetActiveScene().name != "Tutorial";

        GameObject canvasObject = new GameObject(
            "Gameplay Pause Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.matchWidthOrHeight = 1f;

        GameObject safeArea = new GameObject("Pause Safe Area", typeof(RectTransform), typeof(MobileSafeArea));
        safeArea.layer = 5;
        safeArea.transform.SetParent(canvasObject.transform, false);
        Stretch(safeArea.GetComponent<RectTransform>());

        if (showScore)
        {
            Image scorePlate = CreateImage("Score Status", safeArea.transform,
                new Color(0.005f, 0.07f, 0.12f, 0.88f));
            SetCorner(scorePlate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -23f), new Vector2(205f, 50f));
            Outline scoreOutline = scorePlate.gameObject.AddComponent<Outline>();
            scoreOutline.effectColor = new Color(0.49f, 0.96f, 1f, 0.7f);
            scoreOutline.effectDistance = new Vector2(2f, -2f);
            pointsText = CreateText("Points", scorePlate.transform, "TOTAL SCORE // 0", Vector2.zero,
                new Vector2(185f, 36f), 14f, new Color(0.93f, 0.99f, 1f),
                TextAlignmentOptions.Center);
        }

        Image pauseImage = CreateImage("Pause Button", safeArea.transform, Color.white);
        if (theme != null) pauseImage.sprite = theme.pauseIcon;
        pauseImage.preserveAspect = true;
        SetCorner(pauseImage.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-18f, -16f), new Vector2(88f, 88f));
        pauseButton = pauseImage.gameObject.AddComponent<Button>();
        ConfigureButtonColors(pauseButton);
        pauseButton.onClick.AddListener(PauseFromButton);
        SafeNetSfxPlayer.HookButton(pauseButton);

        pauseModal = new GameObject("Pause Modal", typeof(RectTransform));
        pauseModal.layer = 5;
        pauseModal.transform.SetParent(safeArea.transform, false);
        Stretch(pauseModal.GetComponent<RectTransform>());

        Image dimmer = CreateImage("Paused World Dimmer", pauseModal.transform,
            new Color(0.003f, 0.012f, 0.025f, 0.82f));
        Stretch(dimmer.rectTransform);
        dimmer.raycastTarget = true;

        Image frame = CreateImage("Pause Interface Frame", pauseModal.transform, Color.white);
        if (theme != null) frame.sprite = theme.frame;
        frame.preserveAspect = true;
        frame.raycastTarget = false;
        SetRect(frame.rectTransform, Vector2.zero, new Vector2(570f, 608f));
        frameRect = frame.rectTransform;

        Image console = CreateImage("Pause Console", frame.transform,
            new Color(0.003f, 0.065f, 0.095f, 0.84f));
        SetRect(console.rectTransform, Vector2.zero, new Vector2(465f, 490f));
        console.raycastTarget = false;
        Outline consoleOutline = console.gameObject.AddComponent<Outline>();
        consoleOutline.effectColor = new Color(0.49f, 0.96f, 1f, 0.55f);
        consoleOutline.effectDistance = new Vector2(2f, -2f);

        pauseHome = new GameObject("Pause Home", typeof(RectTransform));
        pauseHome.transform.SetParent(console.transform, false);
        Stretch(pauseHome.GetComponent<RectTransform>());
        Transform home = pauseHome.transform;

        CreateText("Interface Label", home, "SAFENET // SESSION CONTROL",
            new Vector2(0f, 196f), new Vector2(420f, 26f), 13f,
            new Color(0.49f, 0.96f, 1f), TextAlignmentOptions.Center);
        CreateText("Pause Heading", home, "SYSTEM PAUSED",
            new Vector2(0f, 154f), new Vector2(420f, 48f), 31f,
            new Color(0.93f, 0.99f, 1f), TextAlignmentOptions.Center);
        statusText = CreateText("Pause Status", home, "SESSION STATE // SECURE HOLD",
            new Vector2(0f, 119f), new Vector2(420f, 24f), 12f,
            new Color(0.13f, 0.96f, 0.61f), TextAlignmentOptions.Center);

        Image sessionPanel = CreateImage("Session Summary", home,
            new Color(0.002f, 0.1f, 0.14f, 0.92f));
        SetRect(sessionPanel.rectTransform, new Vector2(0f, 65f), new Vector2(405f, 72f));
        Outline summaryOutline = sessionPanel.gameObject.AddComponent<Outline>();
        summaryOutline.effectColor = new Color(0.49f, 0.96f, 1f, 0.32f);
        CreateText("Current Mission", sessionPanel.transform, MissionDisplayName(),
            new Vector2(0f, 17f), new Vector2(375f, 28f), 14f,
            new Color(0.93f, 0.99f, 1f), TextAlignmentOptions.Center);
        if (showScore)
        {
            modalPointsText = CreateText("Current Score", sessionPanel.transform, "TOTAL SCORE // 0",
                new Vector2(0f, -18f), new Vector2(375f, 26f), 13f,
                new Color(1f, 0.84f, 0.35f), TextAlignmentOptions.Center);
        }

        CreateText("Checkpoint Notice", home, "PROGRESS CHECKPOINT SAVED",
            new Vector2(0f, 15f), new Vector2(400f, 22f), 11.5f,
            new Color(0.13f, 0.96f, 0.61f), TextAlignmentOptions.Center);

        Button resume = CreateArtworkButton("Resume Button", home,
            theme != null ? theme.resumeButton : null, "RESUME", new Vector2(0f, -30f), true);
        Button settings = CreateArtworkButton("Settings Button", home,
            theme != null ? theme.settingsButton : null, "SETTINGS / VOLUME", new Vector2(0f, -88f), true);
        Button missionSelection = CreateArtworkButton("Mission Selection Button", home,
            theme != null ? theme.missionSelectionButton : null, "MISSION SELECTION", new Vector2(0f, -146f), true);
        Button startMenu = CreateArtworkButton("Start Menu Button", home,
            theme != null ? theme.startMenuButton : null, "RETURN TO START MENU", new Vector2(0f, -204f), true);
        resume.onClick.AddListener(ResumeFromButton);
        missionSelection.onClick.AddListener(OpenMissionSelection);
        startMenu.onClick.AddListener(ReturnToStartMenu);
        settings.onClick.AddListener(() => ShowAudioSettings(true));
        SafeNetSfxPlayer.HookButton(resume);
        SafeNetSfxPlayer.HookButton(missionSelection);
        SafeNetSfxPlayer.HookButton(startMenu);
        SafeNetSfxPlayer.HookButton(settings);

        BuildAudioSettings(console.transform);

        pauseModal.SetActive(false);
        UpdatePoints();
    }

    private void BuildAudioSettings(Transform parent)
    {
        audioSettings = new GameObject("Audio Settings", typeof(RectTransform));
        audioSettings.transform.SetParent(parent, false);
        Stretch(audioSettings.GetComponent<RectTransform>());
        CreateText("Settings Heading", audioSettings.transform, "AUDIO / CONTROLS",
            new Vector2(0, 205), new Vector2(420, 42), 27, Color.white, TextAlignmentOptions.Center);
        CreateText("Settings Help", audioSettings.transform, "ADJUST VOLUME AND TOUCH TARGET SIZE",
            new Vector2(0, 168), new Vector2(415, 24), 11.5f, new Color(.49f, .96f, 1), TextAlignmentOptions.Center);
        CreateVolumeSlider("Master", "MASTER VOLUME", 105, SafeNetAudioManager.MasterVolume, SafeNetAudioManager.SetMaster);
        CreateVolumeSlider("Music", "MUSIC", 29, SafeNetAudioManager.BgmVolume, SafeNetAudioManager.SetBgm);
        CreateVolumeSlider("SFX", "SOUND EFFECTS / VOICE", -47, SafeNetAudioManager.SfxVolume, SafeNetAudioManager.SetSfx);

        controlSizeStatus = CreateText("Control Size Status", audioSettings.transform, "TOUCH CONTROL SIZE",
            new Vector2(0, -103), new Vector2(415, 24), 12.5f, Color.white, TextAlignmentOptions.Center);
        for (int index = 0; index < controlSizePresetImages.Length; index++)
        {
            int preset = index + 1;
            Button presetButton = CreateControlSizePresetButton(
                preset,
                new Vector2(-112f + index * 112f, -143f));
            controlSizePresetImages[index] = presetButton.GetComponent<Image>();
            presetButton.onClick.AddListener(() => SelectControlSizePreset(preset));
            SafeNetSfxPlayer.HookButton(presetButton);
        }
        RefreshControlSizePresetButtons();

        CreateText("Saved Hint", audioSettings.transform, "SETTINGS ARE SAVED AUTOMATICALLY",
            new Vector2(0, -178), new Vector2(425, 22), 10.5f, new Color(.49f, .96f, 1), TextAlignmentOptions.Center);
        Button back = CreateArtworkButton("Back To Pause", audioSettings.transform,
            theme != null ? theme.backToPauseButton : null, "BACK TO PAUSE", new Vector2(0, -214), true);
        back.onClick.AddListener(() => ShowAudioSettings(false));
        SafeNetSfxPlayer.HookButton(back);
        audioSettings.SetActive(false);
    }

    private Button CreateControlSizePresetButton(int preset, Vector2 position)
    {
        Image image = CreateImage("Control Size " + preset, audioSettings.transform,
            new Color(.015f, .14f, .2f, 1f));
        SetRect(image.rectTransform, position, new Vector2(96f, 44f));
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.35f, .95f, 1f, .75f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = image.gameObject.AddComponent<Button>();
        ConfigureButtonColors(button);
        CreateText("Preset Label", image.transform, preset.ToString(), Vector2.zero,
            new Vector2(82f, 34f), 18f, Color.white, TextAlignmentOptions.Center);
        return button;
    }

    private void SelectControlSizePreset(int preset)
    {
        SafeNetControlSizeSettings.SetPreset(preset);
        RefreshControlSizePresetButtons();
        SafeNetMobileInput.Reset();
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void RefreshControlSizePresetButtons()
    {
        int selected = SafeNetControlSizeSettings.CurrentPreset;
        for (int index = 0; index < controlSizePresetImages.Length; index++)
        {
            Image image = controlSizePresetImages[index];
            if (image == null) continue;
            image.color = index + 1 == selected
                ? new Color(.05f, .72f, .48f, 1f)
                : new Color(.015f, .14f, .2f, 1f);
        }

        if (controlSizeStatus != null)
            controlSizeStatus.text = "TOUCH CONTROL SIZE // PRESET " + selected;
    }

    private void CreateVolumeSlider(string name, string label, float y, float value, UnityEngine.Events.UnityAction<float> change)
    {
        Transform parent = audioSettings.transform;
        CreateText(name + " Label", parent, label, new Vector2(-35, y + 26),
            new Vector2(310, 25), 14, Color.white, TextAlignmentOptions.MidlineLeft);
        TMP_Text percentage = CreateText(name + " Percentage", parent, Mathf.RoundToInt(value * 100) + "%",
            new Vector2(168, y + 26), new Vector2(65, 25), 16, Color.white, TextAlignmentOptions.MidlineRight);

        // The entire 400 x 48 track is touchable, not just the small handle.
        Image touchArea = CreateImage(name, parent, new Color(.015f, .1f, .15f, 1));
        SetRect(touchArea.rectTransform, new Vector2(0, y - 12), new Vector2(400, 48));
        Slider slider = touchArea.gameObject.AddComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
        Image track = CreateImage("Track", touchArea.transform, new Color(.1f, .3f, .4f, 1));
        SetRect(track.rectTransform, Vector2.zero, new Vector2(362, 10));
        track.raycastTarget = false;
        Image fill = CreateImage("Volume Fill", track.transform, new Color(.15f, .9f, .85f, 1));
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;
        RectTransform handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(touchArea.transform, false);
        SetRect(handleArea, Vector2.zero, new Vector2(362, 40));
        Image handle = CreateImage("Volume Handle", handleArea, Color.white);
        // Slider stretches the handle vertically; zero extra height keeps it
        // inside its 40-unit area instead of covering the label above the track.
        SetRect(handle.rectTransform, Vector2.zero, new Vector2(32, 0));
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(change);
        slider.onValueChanged.AddListener(volume => percentage.text = Mathf.RoundToInt(volume * 100) + "%");
    }

    private void ShowAudioSettings(bool show)
    {
        pauseHome.SetActive(!show);
        audioSettings.SetActive(show);
        SafeNetMobileInput.Reset();
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void PauseFromButton()
    {
        PauseGame();
    }

    private void ResumeFromButton()
    {
        ResumeGame();
    }

    private void PauseGame()
    {
        if (paused) return;
        paused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        SafeNetMobileInput.Reset();
        GameManager.Instance?.SaveCheckpoint();
        UpdatePoints(true);
        ShowAudioSettings(false);
        pauseModal.SetActive(true);
        if (pauseButton != null) pauseButton.interactable = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        VoiceLinePlayer.SetPaused(true);
    }

    private void ResumeGame()
    {
        if (!paused) return;
        paused = false;
        SafeNetMobileInput.Reset();
        AudioListener.pause = false;
        VoiceLinePlayer.SetPaused(false);
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        pauseModal.SetActive(false);
        if (pauseButton != null) pauseButton.interactable = true;
        if (frameRect != null) frameRect.localScale = Vector3.one;
    }

    private void OpenMissionSelection()
    {
        LeaveScene("Mission Select");
    }

    private void ReturnToStartMenu()
    {
        LeaveScene("Start Menu");
    }

    private void LeaveScene(string sceneName)
    {
        GameManager.Instance?.SaveCheckpoint();
        paused = false;
        AudioListener.pause = false;
        VoiceLinePlayer.SetPaused(false);
        Time.timeScale = 1f;
        SafeNetMobileInput.Reset();
        SceneManager.LoadScene(sceneName);
    }

    private void UpdatePoints(bool force = false)
    {
        StudentProfile student = SafeNetApiClient.Instance?.CurrentStudent;
        int totalPoints = GameManager.Instance != null
            ? GameManager.Instance.totalScore
            : student != null ? student.points : 0;
        if (!force && displayedPoints == totalPoints) return;
        displayedPoints = totalPoints;
        string value = "TOTAL SCORE // " + totalPoints;
        if (pointsText != null) pointsText.text = value;
        if (modalPointsText != null) modalPointsText.text = value;
    }

    private static string MissionDisplayName()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "Tutorial") return "TUTORIAL // SAFE COMMUNICATION BASICS";
        if (!scene.StartsWith("Game(area", System.StringComparison.Ordinal)) return "SAFENET TRAINING SESSION";

        int start = scene.IndexOf("area", System.StringComparison.Ordinal) + 4;
        int end = scene.IndexOf(')', start);
        string number = end > start ? scene.Substring(start, end - start) : "?";
        return number switch
        {
            "1" => "MISSION 1 // INBOX UNDER ATTACK",
            "2" => "MISSION 2 // THE SMISHING SIGNALS",
            "3" => "MISSION 3 // THE TRUSTED STRANGER",
            "4" => "MISSION 4 // VAULT OF WEAK KEYS",
            "5" => "MISSION 5 // DATA SHADOW",
            "6" => "MISSION 6 // THE FALSE WEB",
            _ => "MISSION " + number + " // SAFENET TRAINING SECTOR"
        };
    }

    private Button CreateArtworkButton(string name, Transform parent, Sprite sprite,
        string label, Vector2 position, bool artworkContainsLabel = false)
    {
        Image image = CreateImage(name, parent, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        SetRect(image.rectTransform, position, new Vector2(370f, 58f));
        Button button = image.gameObject.AddComponent<Button>();
        ConfigureButtonColors(button);

        // The supplied Pause UI sprites already contain their final labels.
        // Preserve that artwork instead of hiding it under a generated cover.
        // Settings and other repurposed/missing sprites still receive a clear
        // text fallback so the control always remains understandable.
        if (sprite == null || !artworkContainsLabel)
        {
            Image cover = CreateImage("Button Text Cover", image.transform,
                new Color(0.005f, 0.16f, 0.48f, 0.96f));
            SetRect(cover.rectTransform, Vector2.zero, new Vector2(270f, 27f));
            cover.raycastTarget = false;
            CreateText("Button Label", image.transform, label, Vector2.zero,
                new Vector2(300f, 32f), 15f, new Color(0.93f, 0.99f, 1f),
                TextAlignmentOptions.Center);
        }
        return button;
    }

    private static void ConfigureButtonColors(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.68f, 1f, 0.92f, 1f);
        colors.pressedColor = new Color(0.31f, 0.75f, 0.92f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.35f, 0.42f, 0.46f, 0.62f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private TMP_Text CreateText(string name, Transform parent, string value, Vector2 position,
        Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (theme != null && theme.interfaceFont != null) text.font = theme.interfaceFont;
        text.text = value;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(9f, fontSize - 4f);
        text.fontSizeMax = fontSize;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        SetRect(text.rectTransform, position, size);
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject eventSystemObject =
            new GameObject("SafeNet EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetCorner(RectTransform rect, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void OnDestroy()
    {
        if (!paused) return;
        paused = false;
        AudioListener.pause = false;
        VoiceLinePlayer.SetPaused(false);
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        SafeNetMobileInput.Reset();
    }
}
