using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Presents the final training summary, records Area 6 completion, and offers
/// navigation back to mission selection or the start menu.
/// </summary>
public sealed class GameCompleteController : MonoBehaviour
{
    [Header("Game Complete UI")]
    [SerializeField] private TMP_Text studentNameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private TMP_Text totalScoreText;
    [SerializeField] private TMP_Text performanceText;
    [SerializeField] private TMP_Text completedMissionsText;
    [SerializeField] private TMP_Text syncStatusText;

    private GUIStyle headingStyle;
    private GUIStyle subheadingStyle;
    private GUIStyle labelStyle;
    private GUIStyle scoreStyle;
    private GUIStyle buttonStyle;
    private bool finalProgressSent;
    private SafeNetApiClient subscribedApi;
    private Coroutine scoreAnimation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    public static void LoadScene()
    {
        Scene existing = SceneManager.GetSceneByName("Game Complete");
        if (existing.IsValid() && existing.isLoaded)
        {
            SceneManager.SetActiveScene(existing);
            return;
        }

        if (Application.CanStreamedLevelBeLoaded("Game Complete"))
        {
            SceneManager.LoadScene("Game Complete");
            return;
        }

        Scene previous = SceneManager.GetActiveScene();
        Scene completion = SceneManager.CreateScene("Game Complete");
        SceneManager.SetActiveScene(completion);

        GameObject cameraObject = new GameObject("Game Complete Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.055f, 0.09f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";

        new GameObject("Game Complete Controller").AddComponent<GameCompleteController>();

        if (previous.IsValid() && previous.isLoaded && previous != completion)
            SceneManager.UnloadSceneAsync(previous);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Game Complete" ||
            FindAnyObjectByType<GameCompleteController>() != null) return;

        new GameObject("Game Complete Controller").AddComponent<GameCompleteController>();
    }

    private void Start()
    {
        SubscribeToApi();
        if (subscribedApi != null &&
            subscribedApi.CurrentProgressSyncState == SafeNetApiClient.ProgressSyncState.Failed)
            subscribedApi.RetryFailedProgress();

        SendFinalProgress();
        PopulateCanvasUi();
    }

    private void OnDestroy()
    {
        UnsubscribeFromApi();
    }

    public void ConfigureUi(TMP_Text studentName, TMP_Text studentClass, TMP_Text totalScore,
        TMP_Text performance, TMP_Text completedMissions, TMP_Text syncStatus)
    {
        studentNameText = studentName;
        classText = studentClass;
        totalScoreText = totalScore;
        performanceText = performance;
        completedMissionsText = completedMissions;
        syncStatusText = syncStatus;
    }

    public void OpenMissionSelection()
    {
        SceneManager.LoadScene("Mission Select");
    }

    public void ReturnToStartMenu()
    {
        SceneManager.LoadScene("Start Menu");
    }

    private void PopulateCanvasUi()
    {
        if (totalScoreText == null) return;

        StudentProfile student = SafeNetApiClient.Instance?.CurrentStudent;
        int totalScore = GameManager.Instance != null
            ? GameManager.Instance.totalScore
            : student != null ? student.points : 0;

        if (studentNameText != null)
            studentNameText.text = student != null ? student.fullname : "SAFENET PLAYER";
        if (classText != null)
            classText.text = student?.@class?.section ?? "TRAINING SESSION";
        if (performanceText != null)
            performanceText.text = PerformanceLabel(totalScore);
        if (completedMissionsText != null)
            completedMissionsText.text =
                $"{SafeNetConstants.GradedMissionCount} / " +
                $"{SafeNetConstants.GradedMissionCount} MISSIONS SECURED";

        RefreshSyncStatus();

        if (scoreAnimation != null)
            StopCoroutine(scoreAnimation);
        scoreAnimation = StartCoroutine(AnimateTotalScore(totalScore));
    }

    private IEnumerator AnimateTotalScore(int targetScore)
    {
        const float duration = 1.25f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            totalScoreText.text = Mathf.RoundToInt(Mathf.Lerp(0f, targetScore, eased)).ToString();
            yield return null;
        }
        totalScoreText.text = targetScore.ToString();
        scoreAnimation = null;
    }

    private void SendFinalProgress()
    {
        if (finalProgressSent) return;
        finalProgressSent = true;

        SafeNetApiClient api = subscribedApi != null ? subscribedApi : SafeNetApiClient.Instance;
        int missionScore = GameManager.Instance != null
            ? GameManager.Instance.CurrentMissionScore
            : 0;
        // Area 6 is now the final mission. It saves once before this scene is
        // loaded and is repeated here only to make the completion screen safe
        // when opened through the fallback path.
        api?.SaveMission(
            "area6",
            missionScore,
            true,
            SafeNetConstants.GradedMissionCount);
    }

    private void SubscribeToApi()
    {
        SafeNetApiClient api = SafeNetApiClient.Instance;
        if (api == subscribedApi)
            return;

        UnsubscribeFromApi();
        subscribedApi = api;
        if (subscribedApi == null)
            return;

        subscribedApi.ProfileUpdated += HandleProfileUpdated;
        subscribedApi.ProgressSyncChanged += RefreshSyncStatus;
    }

    private void UnsubscribeFromApi()
    {
        if (subscribedApi == null)
            return;

        subscribedApi.ProfileUpdated -= HandleProfileUpdated;
        subscribedApi.ProgressSyncChanged -= RefreshSyncStatus;
        subscribedApi = null;
    }

    private void HandleProfileUpdated(StudentProfile student)
    {
        if (student == null)
            return;

        if (scoreAnimation != null)
        {
            StopCoroutine(scoreAnimation);
            scoreAnimation = null;
        }

        int acknowledgedTotal = Mathf.Max(0, student.points);
        if (totalScoreText != null)
            totalScoreText.text = acknowledgedTotal.ToString();
        if (performanceText != null)
            performanceText.text = PerformanceLabel(acknowledgedTotal);
        if (completedMissionsText != null)
            completedMissionsText.text =
                $"{Mathf.Min(SafeNetConstants.GradedMissionCount, Mathf.Max(0, student.missions_completed))} / " +
                $"{SafeNetConstants.GradedMissionCount} MISSIONS SECURED";
    }

    private void RefreshSyncStatus()
    {
        if (syncStatusText != null)
            syncStatusText.text = SyncStatusMessage();
    }

    private string SyncStatusMessage()
    {
        SafeNetApiClient api = subscribedApi != null ? subscribedApi : SafeNetApiClient.Instance;
        if (api == null || !api.IsLiveApiConfigured || api.IsTesterMode)
            return "RESULTS SAVED TO THIS SAFENET TRAINING SESSION";
        if (!api.IsLoggedIn)
            return "RESULTS NOT SYNCHRONIZED — SIGN IN AND TRY AGAIN";

        return api.CurrentProgressSyncState switch
        {
            SafeNetApiClient.ProgressSyncState.Pending =>
                $"SYNCHRONIZING RESULTS... {Mathf.Max(1, api.PendingProgressCount)} UPLOAD(S) REMAINING",
            SafeNetApiClient.ProgressSyncState.Succeeded =>
                "RESULTS SYNCHRONIZED WITH THE FACULTY DASHBOARD",
            SafeNetApiClient.ProgressSyncState.Failed =>
                "RESULTS NOT SYNCHRONIZED — " + FriendlySyncError(api.LastProgressSyncError),
            _ => "WAITING FOR SERVER CONFIRMATION"
        };
    }

    private static string FriendlySyncError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return "CHECK YOUR CONNECTION AND TRY AGAIN";

        string singleLine = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
        const int maximumLength = 92;
        if (singleLine.Length > maximumLength)
            singleLine = singleLine.Substring(0, maximumLength).TrimEnd() + "...";
        return singleLine.ToUpperInvariant();
    }

    private void EnsureStyles()
    {
        if (headingStyle != null) return;

        headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 44,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.35f, 1f, 0.82f) }
        };
        subheadingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.6f, 0.9f, 1f) }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };
        scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(1f, 0.86f, 0.34f) }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }

    private void OnGUI()
    {
        // The immediate-mode panel is retained only as a fallback for a missing serialized scene.
        if (totalScoreText != null) return;
        EnsureStyles();

        GUI.color = new Color(0.018f, 0.055f, 0.09f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float width = Mathf.Min(780f, Screen.width - 40f);
        float height = Mathf.Min(690f, Screen.height - 40f);
        Rect panel = new Rect(
            (Screen.width - width) / 2f,
            (Screen.height - height) / 2f,
            width,
            height);

        GUI.Box(panel, "");
        GUILayout.BeginArea(new Rect(panel.x + 38f, panel.y + 28f, panel.width - 76f, panel.height - 56f));
        GUILayout.Label("MISSION NETWORK SECURED", headingStyle, GUILayout.Height(68f));
        GUILayout.Label(
            $"All {SafeNetConstants.GradedMissionCount} cybersecurity missions have been completed.",
            subheadingStyle,
            GUILayout.Height(42f));
        GUILayout.Space(18f);

        StudentProfile student = SafeNetApiClient.Instance?.CurrentStudent;
        int totalScore = GameManager.Instance != null
            ? GameManager.Instance.totalScore
            : student != null ? student.points : 0;
        int completed = student != null
            ? Mathf.Max(SafeNetConstants.GradedMissionCount, student.missions_completed)
            : SafeNetConstants.GradedMissionCount;

        DrawResult("STUDENT", student != null ? student.fullname : "SafeNet Player");
        DrawResult("CLASS", student?.@class?.section ?? "Tester Session");
        DrawResult(
            "MISSIONS COMPLETED",
            $"{Mathf.Min(completed, SafeNetConstants.GradedMissionCount)} / " +
            SafeNetConstants.GradedMissionCount);
        DrawResult("TOTAL SCORE", totalScore.ToString());
        DrawResult("PERFORMANCE", PerformanceLabel(totalScore));

        GUILayout.Space(24f);
        GUILayout.Label(
            SyncStatusMessage(),
            subheadingStyle,
            GUILayout.Height(38f));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("MISSION SELECTION", buttonStyle, GUILayout.Height(56f)))
            OpenMissionSelection();
        GUILayout.Space(10f);
        if (GUILayout.Button("RETURN TO START MENU", buttonStyle, GUILayout.Height(56f)))
            ReturnToStartMenu();
        GUILayout.EndArea();
    }

    private void DrawResult(string label, string value)
    {
        GUILayout.BeginHorizontal(GUILayout.Height(45f));
        GUILayout.Label(label, labelStyle);
        GUILayout.Label(value, scoreStyle, GUILayout.Width(360f));
        GUILayout.EndHorizontal();
    }

    private static string PerformanceLabel(int score)
    {
        // Performance bands remain intentionally stable while mission scoring
        // is being reworked separately.
        if (score >= 45) return "CYBER DEFENDER";
        if (score >= 35) return "SECURITY SPECIALIST";
        if (score >= 25) return "THREAT ANALYST";
        return "AWARENESS TRAINEE";
    }
}
