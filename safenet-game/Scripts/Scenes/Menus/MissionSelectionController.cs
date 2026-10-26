using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Draws the mission-selection interface, applies progression locks, and opens
/// only missions that are both unlocked and included in the current build.
/// </summary>
public class MissionSelectionController : MonoBehaviour
{
    // Tutorial is always available. Each completed graded mission unlocks the next one.
    private static readonly string[] SceneNames =
    {
        "Tutorial", "Game(area1)", "Game(area2)", "Game(area3)",
        "Game(area4)", "Game(area5)", "Game(area6)"
    };

    private static readonly string[] FallbackLabels =
    {
        "Tutorial", "Mission 1: Inbox Under Attack", "Mission 2: The Smishing Signals",
        "Mission 3: The Trusted Stranger", "Mission 4: Vault of Weak Keys",
        "Mission 5: Data Shadow", "Mission 6: The False Web"
    };

    // Artwork is assigned in the Mission Select scene by SafeNetMissionSelectionSetup.
    [Header("Mission Selection Artwork")]
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private Texture2D frameTexture;
    [SerializeField] private Texture2D[] missionTextures = new Texture2D[7];
    [SerializeField] private Texture2D backButtonTexture;
    [SerializeField] private Font titleFont;

    private GUIStyle titleStyle;
    private GUIStyle fallbackButtonStyle;

    private const float ReferenceWidth = 738f;
    private const float ReferenceHeight = 768f;

    // Positions match the supplied 738 x 768 MissionSelection_UI frame.
    private static readonly Rect[] ReferenceMissionRects =
    {
        new Rect(131f, 143f, 476f, 86f),
        new Rect(132f, 213f, 475f, 69f),
        new Rect(130f, 270f, 477f, 80f),
        new Rect(130f, 329f, 477f, 86f),
        new Rect(129f, 392f, 480f, 85f),
        new Rect(126f, 461f, 485f, 72f),
        new Rect(127f, 522f, 484f, 77f)
    };

    private static readonly Rect ReferenceBackRect = new Rect(194f, 635f, 350f, 71f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Mission Select" || FindAnyObjectByType<MissionSelectionController>() != null) return;
        new GameObject("Mission Selection").AddComponent<MissionSelectionController>();
    }

    private void EnsureStyles(float scale)
    {
        int titleSize = Mathf.Max(20, Mathf.RoundToInt(32f * scale));

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = titleFont,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.49f, 0.96f, 1f);

            fallbackButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
        }

        titleStyle.fontSize = titleSize;
    }

    private void OnGUI()
    {
        if (!HasCompleteArtwork())
        {
            DrawFallbackInterface();
            return;
        }

        Rect safeArea = Screen.safeArea;
        Rect guiSafeArea = new Rect(
            safeArea.x,
            Screen.height - safeArea.yMax,
            safeArea.width,
            safeArea.height);

        float scale = Mathf.Min(guiSafeArea.width / ReferenceWidth, guiSafeArea.height / ReferenceHeight);
        float panelWidth = ReferenceWidth * scale;
        float panelHeight = ReferenceHeight * scale;
        Rect panel = new Rect(
            guiSafeArea.x + (guiSafeArea.width - panelWidth) * 0.5f,
            guiSafeArea.y + (guiSafeArea.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        EnsureStyles(scale);

        // Reuse the Start Menu background behind the centered mission panel.
        Color previousColor = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            backgroundTexture,
            ScaleMode.ScaleAndCrop,
            true);
        GUI.DrawTexture(panel, frameTexture, ScaleMode.StretchToFill, true);

        GUI.Label(ScaleRect(new Rect(65f, 69f, 608f, 58f), panel, scale), "MISSION SELECTION", titleStyle);

        SafeNetApiClient api = SafeNetApiClient.Instance;
        StudentProfile student = api != null ? api.CurrentStudent : null;
        int completed = student != null ? Mathf.Max(0, student.missions_completed) : 0;

        for (int index = 0; index < SceneNames.Length; index++)
        {
            Rect buttonRect = ScaleRect(ReferenceMissionRects[index], panel, scale);
            bool progressionUnlocked = index == 0 || index <= completed + 1;
            bool sceneReady = Application.CanStreamedLevelBeLoaded(SceneNames[index]);
            bool unlocked = progressionUnlocked && sceneReady;

            GUI.color = unlocked ? Color.white : new Color(0.34f, 0.4f, 0.45f, 0.72f);
            GUI.DrawTexture(buttonRect, missionTextures[index], ScaleMode.StretchToFill, true);
            GUI.color = Color.white;

            if (unlocked)
            {
                if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
                {
                    SafeNetSfxPlayer.PlayButton();
                    SceneManager.LoadScene(SceneNames[index]);
                }
            }
        }

        Rect backRect = ScaleRect(ReferenceBackRect, panel, scale);
        GUI.color = Color.white;
        GUI.DrawTexture(backRect, backButtonTexture, ScaleMode.StretchToFill, true);
        if (GUI.Button(backRect, GUIContent.none, GUIStyle.none))
        {
            SafeNetSfxPlayer.PlayButton();
            SceneManager.LoadScene("Start Menu");
        }

        GUI.color = previousColor;
    }

    private bool HasCompleteArtwork()
    {
        if (backgroundTexture == null || frameTexture == null || backButtonTexture == null ||
            titleFont == null ||
            missionTextures == null || missionTextures.Length != SceneNames.Length)
            return false;

        for (int index = 0; index < missionTextures.Length; index++)
            if (missionTextures[index] == null) return false;

        return true;
    }

    private static Rect ScaleRect(Rect referenceRect, Rect panel, float scale)
    {
        return new Rect(
            panel.x + referenceRect.x * scale,
            panel.y + referenceRect.y * scale,
            referenceRect.width * scale,
            referenceRect.height * scale);
    }

    // Safe fallback if the scene artwork is accidentally unassigned during development.
    private void DrawFallbackInterface()
    {
        EnsureStyles(1f);
        GUI.color = new Color(0.025f, 0.08f, 0.13f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float width = Mathf.Min(820f, Screen.width - 40f);
        Rect panel = new Rect((Screen.width - width) / 2f, 20f, width, Screen.height - 40f);
        GUILayout.BeginArea(new Rect(panel.x + 25f, panel.y + 15f, panel.width - 50f, panel.height - 30f));
        GUILayout.Label("MISSION SELECTION", titleStyle, GUILayout.Height(52f));

        SafeNetApiClient api = SafeNetApiClient.Instance;
        StudentProfile student = api != null ? api.CurrentStudent : null;
        int completed = student != null ? Mathf.Max(0, student.missions_completed) : 0;

        for (int index = 0; index < SceneNames.Length; index++)
        {
            bool unlocked = (index == 0 || index <= completed + 1) &&
                            Application.CanStreamedLevelBeLoaded(SceneNames[index]);
            GUI.enabled = unlocked;
            if (GUILayout.Button(FallbackLabels[index], fallbackButtonStyle, GUILayout.Height(50f)))
            {
                SafeNetSfxPlayer.PlayButton();
                SceneManager.LoadScene(SceneNames[index]);
            }
            GUI.enabled = true;
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("BACK TO START MENU", fallbackButtonStyle, GUILayout.Height(48f)))
        {
            SafeNetSfxPlayer.PlayButton();
            SceneManager.LoadScene("Start Menu");
        }
        GUILayout.EndArea();
    }
}
