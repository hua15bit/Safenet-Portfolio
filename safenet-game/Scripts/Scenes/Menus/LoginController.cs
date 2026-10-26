using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Validates the login form, delegates authentication to SafeNetApiClient, and
/// provides an editor/development-build account for offline testing.
/// </summary>
public class LoginController : MonoBehaviour
{
    // Development access is intentionally unavailable in non-development release builds.
    private const string DebugUsername = "debug";
    private const string DebugPassword = "SafeNetDebug2026!";
    private const string DebugClassCode = "DEBUG1";

    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField passwordField;
    [SerializeField] private TMP_InputField classCodeField;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Login UI Artwork")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite logoSprite;
    [SerializeField] private Sprite enterButtonSprite;
    [SerializeField] private Sprite createAccountButtonSprite;
    [SerializeField] private Sprite exitButtonSprite;
    [SerializeField] private Font interfaceFont;

    private SafeNetApiClient api;
    private string pendingIdentifier;
    private bool loginInProgress;
    private TMP_FontAsset runtimeFontAsset;

    private void Awake()
    {
        EnsurePresentationSurface();
    }

    private void Start()
    {
        ApplyUpdatedVisuals();
        if (feedbackText != null) feedbackText.text = "";
        api = SafeNetApiClient.Instance;
        if (api == null)
        {
            GameObject apiObject = new GameObject("SafeNet API");
            api = apiObject.AddComponent<SafeNetApiClient>();
        }
        api.LoginSucceeded += OnLoginSucceeded;
        api.RequestFailed += OnRequestFailed;
        if (usernameField != null)
        {
            usernameField.text = PlayerPrefs.GetString("SafeNetPlayerName", "");
            usernameField.Select();
        }
    }

    /// <summary>
    /// Keeps the login usable even if an editor hierarchy pass nested its root
    /// canvas. Screen-space canvases can serialize a zero scale while they are
    /// scene roots; that scale becomes destructive after reparenting.
    /// </summary>
    private void EnsurePresentationSurface()
    {
        transform.localScale = Vector3.one;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
        }

        Camera outputCamera = Camera.main;
        if (outputCamera == null)
        {
            GameObject cameraObject = new GameObject(
                "Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            outputCamera = cameraObject.GetComponent<Camera>();
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = Color.black;
            outputCamera.cullingMask = 0;
        }
        else if (outputCamera.GetComponent<AudioListener>() == null &&
                 FindAnyObjectByType<AudioListener>() == null)
        {
            outputCamera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void Update()
    {
        if (SafeNetDesktopInput.SubmitPressed)
            SignIn();
    }

    public void SignIn()
    {
        if (loginInProgress) return;
        string username = usernameField != null ? usernameField.text.Trim() : "";
        string password = passwordField != null ? passwordField.text : "";
        string classCode = classCodeField != null ? classCodeField.text.Trim() : "";

        if (TryDevelopmentLogin(username, password, classCode)) return;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowFeedback("ENTER YOUR USERNAME OR STUDENT ID.", false);
            usernameField?.Select();
            return;
        }

        if (password.Length == 0 || classCode.Length != 6)
        {
            ShowFeedback("ENTER YOUR PASSWORD AND 6-CHARACTER CLASS CODE.", false);
            return;
        }
        pendingIdentifier = username;
        loginInProgress = true;
        ShowFeedback("CONNECTING TO SAFENET DATABASE...", true);
        api.Login(username, password, classCode);
    }

    public void SignInAsTester()
    {
        if (!DevelopmentLoginAllowed)
        {
            ShowFeedback("DEVELOPMENT LOGIN IS DISABLED IN THIS BUILD.", false);
            return;
        }
        ShowFeedback("DEVELOPMENT ACCESS GRANTED", true);
        api.LoginAsTester();
    }

    public void RegisterStudent() => api.OpenRegistration();

    public void ExitSafeNet()
    {
        SafeNetApplication.Quit();
    }

    private void OnLoginSucceeded(StudentProfile student)
    {
        loginInProgress = false;
        if (!api.IsTesterMode && !string.IsNullOrWhiteSpace(pendingIdentifier))
            PlayerPrefs.SetString("SafeNetPlayerName", pendingIdentifier);
        PlayerPrefs.Save();
        ShowFeedback(api.IsTesterMode ? "DEVELOPMENT ACCESS GRANTED" : "DATABASE VERIFIED // ACCESS GRANTED", true);
        SceneManager.LoadScene("Start Menu");
    }

    private static bool DevelopmentLoginAllowed => Application.isEditor || Debug.isDebugBuild;

    private bool TryDevelopmentLogin(string username, string password, string classCode)
    {
        if (!DevelopmentLoginAllowed) return false;
        if (!string.Equals(username, DebugUsername, System.StringComparison.OrdinalIgnoreCase) ||
            password != DebugPassword ||
            !string.Equals(classCode, DebugClassCode, System.StringComparison.OrdinalIgnoreCase))
            return false;

        pendingIdentifier = "";
        loginInProgress = true;
        ShowFeedback("DEVELOPMENT ACCESS GRANTED", true);
        api.LoginAsTester();
        return true;
    }

    private void OnRequestFailed(string message)
    {
        loginInProgress = false;
        ShowFeedback(message, false);
    }

    private void OnDestroy()
    {
        if (api == null) return;
        api.LoginSucceeded -= OnLoginSucceeded;
        api.RequestFailed -= OnRequestFailed;
    }

    // Applies the supplied Login_UI artwork even if the scene was open during an editor rebuild.
    private void ApplyUpdatedVisuals()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 768f);
            scaler.matchWidthOrHeight = 1f;
        }

        if (interfaceFont != null)
        {
            runtimeFontAsset = TMP_FontAsset.CreateFontAsset(interfaceFont);
            if (runtimeFontAsset != null)
            {
                runtimeFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                    text.font = runtimeFontAsset;
            }
        }

        Image background = FindComponent<Image>("Cyber Background");
        if (background != null && backgroundSprite != null)
        {
            background.gameObject.name = "Start Menu Background";
            background.sprite = backgroundSprite;
            background.color = Color.white;
            background.preserveAspect = false;
            Stretch(background.rectTransform);
        }

        SetObjectActive("Status Rail", false);
        SetObjectActive("Interface Label", false);
        foreach (Transform item in GetComponentsInChildren<Transform>(true))
            if (item.name.StartsWith("Grid ", System.StringComparison.Ordinal)) item.gameObject.SetActive(false);

        Image frame = FindComponent<Image>("Login Terminal");
        if (frame != null && frameSprite != null)
        {
            frame.gameObject.name = "Login Frame";
            frame.sprite = frameSprite;
            frame.color = Color.white;
            frame.preserveAspect = true;
            SetRect(frame.rectTransform, Vector2.zero, new Vector2(738f, 768f));
            Outline frameOutline = frame.GetComponent<Outline>();
            if (frameOutline != null) frameOutline.enabled = false;
        }

        ConfigureImage("SafeNet Logo", logoSprite, new Vector2(0f, 281f), new Vector2(124f, 116f));
        ConfigureText("Title", "SECURE LOGIN", new Vector2(0f, 197f), new Vector2(600f, 54f), 36f, new Color(0.49f, 0.96f, 1f));
        ConfigureText("Subtitle", "AUTHENTICATE TO ENTER THE SAFENET TRAINING SYSTEM",
            new Vector2(0f, 159f), new Vector2(590f, 28f), 15f, new Color(0.91f, 0.98f, 1f));

        ConfigureInput("Username Input", new Vector2(0f, 94f));
        ConfigureInput("Password Input", new Vector2(0f, 25f));
        ConfigureInput("Class Code Input", new Vector2(0f, -44f));
        ConfigureText("Login Feedback", "", new Vector2(0f, -91f), new Vector2(590f, 30f), 14f,
            new Color(1f, 0.86f, 0.28f));

        ConfigureArtworkButton("Sign In Button", enterButtonSprite, new Vector2(0f, -145f), new Vector2(390f, 77f));
        ConfigureArtworkButton("Register Button", createAccountButtonSprite, new Vector2(-113f, -224f), new Vector2(202f, 68f));
        ConfigureArtworkButton("Exit Button", exitButtonSprite, new Vector2(113f, -224f), new Vector2(200f, 66f));

        SetObjectActive("Tester Button", false);
        SetObjectActive("Security Note", false);
    }

    private void ConfigureInput(string objectName, Vector2 position)
    {
        TMP_InputField input = FindComponent<TMP_InputField>(objectName);
        if (input == null) return;

        RectTransform rect = input.GetComponent<RectTransform>();
        SetRect(rect, position, new Vector2(520f, 56f));
        Image image = input.GetComponent<Image>();
        if (image != null) image.color = new Color(0.01f, 0.075f, 0.13f, 0.97f);
        Outline outline = input.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(0.49f, 0.96f, 1f, 0.72f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        if (input.textComponent != null)
        {
            input.textComponent.fontSize = 18f;
            input.textComponent.color = new Color(0.91f, 0.98f, 1f);
        }
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.fontSize = 16f;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.color = new Color(0.49f, 0.96f, 1f);
        }
        input.asteriskChar = '*';
    }

    private void ConfigureArtworkButton(string objectName, Sprite sprite, Vector2 position, Vector2 size)
    {
        Button button = FindComponent<Button>(objectName);
        if (button == null || sprite == null) return;

        SetRect(button.GetComponent<RectTransform>(), position, size);
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        Outline outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.gameObject.SetActive(false);
    }

    private void ConfigureImage(string objectName, Sprite sprite, Vector2 position, Vector2 size)
    {
        Image image = FindComponent<Image>(objectName);
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        SetRect(image.rectTransform, position, size);
    }

    private void ConfigureText(string objectName, string value, Vector2 position, Vector2 size,
        float fontSize, Color color)
    {
        TMP_Text text = FindComponent<TMP_Text>(objectName);
        if (text == null) return;
        text.text = value;
        text.fontSize = fontSize;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Max(10f, fontSize - 5f);
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        SetRect(text.rectTransform, position, size);
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        foreach (T component in GetComponentsInChildren<T>(true))
            if (component.gameObject.name == objectName) return component;
        return null;
    }

    private void SetObjectActive(string objectName, bool active)
    {
        foreach (Transform item in GetComponentsInChildren<Transform>(true))
        {
            if (item.name != objectName) continue;
            item.gameObject.SetActive(active);
            return;
        }
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

    private void ShowFeedback(string message, bool success)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = success
            ? new Color(0f, 0.94f, 0.22f, 1f)
            : new Color(1f, 0.35f, 0.38f, 1f);
    }
}
