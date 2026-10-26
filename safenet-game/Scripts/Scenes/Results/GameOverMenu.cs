using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles Game Over navigation and supplies a compatibility upgrade for older
/// serialized copies of the infected-device interface.
/// </summary>
public class GameOverMenu : MonoBehaviour
{
    [Header("Game Over Artwork")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite retryButtonSprite;
    [SerializeField] private Sprite missionSelectionButtonSprite;
    [SerializeField] private Sprite mainMenuButtonSprite;
    [SerializeField] private TMP_FontAsset interfaceFont;

    public void ConfigureArtwork(Sprite background, Sprite frame, Sprite retry, Sprite missionSelection,
        Sprite mainMenu, TMP_FontAsset font)
    {
        backgroundSprite = background;
        frameSprite = frame;
        retryButtonSprite = retry;
        missionSelectionButtonSprite = missionSelection;
        mainMenuButtonSprite = mainMenu;
        interfaceFont = font;
    }

    private void Start()
    {
        // Prefer the current shared UI theme so older scene copies also gain
        // the supplied RETRY MISSION artwork without requiring manual rewiring.
        SafeNetPauseTheme theme = Resources.Load<SafeNetPauseTheme>("SafeNetPauseTheme");
        if (theme != null && theme.retryMissionButton != null)
            retryButtonSprite = theme.retryMissionButton;

        // Older serialized copies of the scene are upgraded at runtime until the editor rebuild runs.
        if (FindChild<Transform>("Infected System Frame") == null)
        {
            UpgradeLegacyScene();
            return;
        }

        ApplyCurrentRetryArtwork();
    }

    private void ApplyCurrentRetryArtwork()
    {
        Button retry = FindChild<Button>("Retry Mission Button");
        if (retry == null || retryButtonSprite == null) return;

        Image image = retry.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = retryButtonSprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        Outline outline = retry.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        Transform cover = retry.transform.Find("Button Text Cover");
        if (cover != null) cover.gameObject.SetActive(false);
        TMP_Text label = retry.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.gameObject.SetActive(false);
    }

    private void UpgradeLegacyScene()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 768f);
            scaler.matchWidthOrHeight = 1f;
        }

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (interfaceFont != null) text.font = interfaceFont;
            text.fontStyle = FontStyles.Normal;
        }

        Image background = FindChild<Image>("Website Background");
        if (background != null)
        {
            background.gameObject.name = "Infected Device Background";
            if (backgroundSprite != null) background.sprite = backgroundSprite;
            background.color = Color.white;
        }

        foreach (Transform item in GetComponentsInChildren<Transform>(true))
            if (item.name.StartsWith("Grid ", System.StringComparison.Ordinal) || item.name == "Status Rail")
                item.gameObject.SetActive(false);

        Image terminal = FindChild<Image>("Failure Terminal");
        if (terminal == null) return;
        terminal.gameObject.name = "Legacy Infected System Frame";
        if (frameSprite != null) terminal.sprite = frameSprite;
        terminal.color = Color.white;
        terminal.preserveAspect = true;
        SetRect(terminal.rectTransform, Vector2.zero, new Vector2(738f, 768f));
        Outline outline = terminal.GetComponent<Outline>();
        if (outline != null) outline.effectColor = new Color(1f, 0.05f, 0.15f, 0.65f);

        Transform logo = FindChild<Transform>("SafeNet Logo");
        if (logo != null) logo.gameObject.SetActive(false);

        TMP_Text label = FindChild<TMP_Text>("Terminal Label");
        ConfigureText(label, "CRITICAL THREAT // DEVICE SECURITY OVERRIDDEN",
            new Vector2(0f, 252f), new Vector2(600f, 34f), 18f, new Color(1f, 0.68f, 0.14f));
        TMP_Text title = FindChild<TMP_Text>("Mission Failed");
        ConfigureText(title, "SYSTEM BREACH", new Vector2(0f, 190f),
            new Vector2(610f, 70f), 50f, new Color(1f, 0.16f, 0.2f));
        TMP_Text detail = FindChild<TMP_Text>("Failure Detail");
        ConfigureText(detail,
            "A MALICIOUS PROCESS INFILTRATED THE TRAINING DEVICE.\nTHE SESSION WAS TERMINATED TO CONTAIN THE THREAT.",
            new Vector2(0f, 118f), new Vector2(560f, 70f), 15f, new Color(0.84f, 0.94f, 0.97f));

        Image statusPanel = FindChild<Image>("Integrity Status");
        if (statusPanel != null)
        {
            statusPanel.color = new Color(0.12f, 0.005f, 0.025f, 0.96f);
            SetRect(statusPanel.rectTransform, new Vector2(0f, 46f), new Vector2(530f, 42f));
        }
        TMP_Text status = FindChild<TMP_Text>("Status Text");
        ConfigureText(status, "THREAT ACTIVITY: MALICIOUS PROCESS REPLICATING",
            Vector2.zero, new Vector2(500f, 32f), 12f, new Color(1f, 0.68f, 0.14f));

        Button retry = FindChild<Button>("Retry Mission Button");
        Button mission = FindChild<Button>("Mission Selection Button");
        Button main = FindChild<Button>("Main Menu Button");
        UpgradeButton(retry, retryButtonSprite, "RETRY MISSION", new Vector2(0f, -54f));
        UpgradeButton(mission, missionSelectionButtonSprite, "MISSION SELECTION", new Vector2(0f, -139f));
        UpgradeButton(main, mainMenuButtonSprite, "RETURN TO START MENU", new Vector2(0f, -224f));

        CreateLegacyThreatLayer(background != null ? background.transform : transform,
            terminal.transform, title, status);
    }

    private void CreateLegacyThreatLayer(Transform parent, Transform terminal, TMP_Text title, TMP_Text status)
    {
        Image darkness = CreateRuntimeImage("Runtime Threat Darkness", parent,
            new Color(0.035f, 0f, 0.025f, 0.7f));
        Stretch(darkness.rectTransform);
        darkness.transform.SetSiblingIndex(Mathf.Max(0, terminal.GetSiblingIndex()));

        List<Graphic> signals = new List<Graphic>();
        List<RectTransform> fragments = new List<RectTransform>();
        Vector2[] positions =
        {
            new Vector2(-540f, 245f), new Vector2(520f, 190f), new Vector2(-570f, -220f),
            new Vector2(555f, -275f), new Vector2(-610f, 20f), new Vector2(605f, 45f)
        };
        for (int i = 0; i < positions.Length; i++)
        {
            Image line = CreateRuntimeImage("Infected Data Path", darkness.transform,
                new Color(1f, 0.02f, 0.15f, 0.55f));
            SetRect(line.rectTransform, positions[i],
                i < 4 ? new Vector2(230f, 3f) : new Vector2(3f, 155f));
            signals.Add(line);

            Image fragment = CreateRuntimeImage("Corrupted Data Fragment", darkness.transform,
                i % 2 == 0 ? new Color(1f, 0f, 0.25f, 0.75f) : new Color(1f, 0.56f, 0.05f, 0.72f));
            SetRect(fragment.rectTransform, positions[i] + new Vector2(i % 2 == 0 ? 100f : -100f, 22f),
                new Vector2(26f + i % 3 * 8f, 7f));
            signals.Add(fragment);
            fragments.Add(fragment.rectTransform);
        }

        Image glow = CreateRuntimeImage("Runtime Breach Core Glow", darkness.transform,
            new Color(1f, 0f, 0.16f, 0.2f));
        SetRect(glow.rectTransform, Vector2.zero, new Vector2(760f, 760f));

        Image warning = CreateRuntimeImage("Runtime Threat Warning", terminal,
            new Color(0.2f, 0f, 0.025f, 0.98f));
        SetRect(warning.rectTransform, new Vector2(0f, 315f), new Vector2(62f, 62f));
        Outline warningOutline = warning.gameObject.AddComponent<Outline>();
        warningOutline.effectColor = new Color(1f, 0.15f, 0.2f);
        warningOutline.effectDistance = new Vector2(3f, -3f);
        TMP_Text mark = CreateRuntimeText("!", warning.transform, 45f, new Color(1f, 0.16f, 0.2f));
        Stretch(mark.rectTransform);

        GameOverThreatEffects effects = GetComponent<GameOverThreatEffects>();
        if (effects == null) effects = gameObject.AddComponent<GameOverThreatEffects>();
        effects.Configure(glow.rectTransform, warning.rectTransform, title, status,
            signals.ToArray(), fragments.ToArray());
    }

    private void UpgradeButton(Button button, Sprite sprite, string labelText, Vector2 position)
    {
        if (button == null) return;
        SetRect(button.GetComponent<RectTransform>(), position, new Vector2(410f, 62f));
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            if (sprite != null) image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        Outline outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        Transform existingCover = button.transform.Find("Button Text Cover");
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        // Every supplied result-screen button includes its own typography.
        // Hide old generated masks/labels so the approved asset is visible.
        if (sprite != null)
        {
            if (existingCover != null) existingCover.gameObject.SetActive(false);
            if (label != null) label.gameObject.SetActive(false);
            return;
        }

        Image cover = existingCover != null ? existingCover.GetComponent<Image>() : null;
        if (cover == null)
        {
            cover = CreateRuntimeImage("Button Text Cover", button.transform,
                new Color(0.005f, 0.16f, 0.48f, 0.96f));
        }
        cover.gameObject.SetActive(true);
        SetRect(cover.rectTransform, Vector2.zero, new Vector2(300f, 29f));
        cover.raycastTarget = false;
        if (label == null) label = CreateRuntimeText(labelText, button.transform, 16f, Color.white);
        label.gameObject.SetActive(true);
        label.transform.SetAsLastSibling();
        ConfigureText(label, labelText, Vector2.zero, new Vector2(320f, 34f), 16f, Color.white);
    }

    private static void ConfigureText(TMP_Text text, string value, Vector2 position, Vector2 size,
        float fontSize, Color color)
    {
        if (text == null) return;
        text.text = value;
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(9f, fontSize - 5f);
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        SetRect(text.rectTransform, position, size);
    }

    private T FindChild<T>(string objectName) where T : Component
    {
        foreach (T component in GetComponentsInChildren<T>(true))
            if (component.gameObject.name == objectName) return component;
        return null;
    }

    private static Image CreateRuntimeImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text CreateRuntimeText(string value, Transform parent, float fontSize, Color color)
    {
        GameObject obj = new GameObject("Threat Text", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (interfaceFont != null) text.font = interfaceFont;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
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
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public void RetryMission()
    {
        string missionScene = GameManager.LastMissionScene;
        if (!Application.CanStreamedLevelBeLoaded(missionScene))
            missionScene = "Game(area1)";

        SceneManager.LoadScene(missionScene);
    }

    public void ReturnToMissionSelection()
    {
        SceneManager.LoadScene("Mission Select");
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("Start Menu");
    }

    public void ExitSafeNet()
    {
        SafeNetApplication.Quit();
    }
}
