using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Owns the player's score and system integrity across gameplay scenes, saves
/// mission checkpoints, and opens Game Over when integrity is exhausted.
/// </summary>
public class GameManager : MonoBehaviour
{
    private const string LastMissionSceneKey = "SafeNet.LastMissionScene";
    public static GameManager Instance { get; private set; }

    [Header("Player Stats")]
    public int totalScore = 0;
    public int systemIntegrity = SafeNetConstants.MaximumSystemIntegrity;
    public int scoreRequiredToPass = 5;
    private int activeMissionScore;
    private string activeMissionScene = string.Empty;
    private readonly Dictionary<string, int> localMissionBestScores = new Dictionary<string, int>();
    private SafeNetApiClient subscribedApi;

    /// <summary>
    /// Score earned in the current attempt. It is intentionally separate from
    /// totalScore so replaying a mission cannot add the same points twice.
    /// </summary>
    public int CurrentMissionScore => Mathf.Max(0, activeMissionScore);
    public int WrongAnswersThisMission =>
        Mathf.Max(0, SafeNetConstants.MaximumSystemIntegrity - systemIntegrity);
    public static string LastMissionScene =>
        PlayerPrefs.GetString(LastMissionSceneKey, "Game(area1)");

    [Header("UI Reference")]
    public TMP_Text scoreUIText;

    private void Awake()
    {
        // Only one persistent manager may own run state across scene changes.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        DisableLegacySceneScore();
        SubscribeToApi();
        ApplyStudentProfile(SafeNetApiClient.Instance?.CurrentStudent);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromApi();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SubscribeToApi();
        ApplySceneScoreRequirement(scene.name);

        BeginOrResumeMission(scene.name);

        DisableLegacySceneScore();
        UpdateScoreDisplay();
    }

    private void Start()
    {
        SubscribeToApi();
        ApplySceneScoreRequirement(SceneManager.GetActiveScene().name);
        BeginOrResumeMission(SceneManager.GetActiveScene().name);
        DisableLegacySceneScore();
        UpdateScoreDisplay();
    }

    private void BeginOrResumeMission(string sceneName)
    {
        bool isGameplayMission = sceneName.StartsWith(
            "Game(area",
            System.StringComparison.Ordinal);

        if (!isGameplayMission)
        {
            // Game Over deliberately clears this before loading its scene so
            // Retry Mission starts a new attempt. Menus also end the attempt.
            if (sceneName != "GameOverScene")
                activeMissionScene = string.Empty;
            return;
        }

        if (activeMissionScene == sceneName)
        {
            Debug.Log(
                $"Resuming {sceneName}: {WrongAnswersThisMission} wrong answer(s), " +
                $"integrity {systemIntegrity}/{SafeNetConstants.MaximumSystemIntegrity}.");
            return;
        }

        if (!string.IsNullOrEmpty(activeMissionScene))
        {
            // Moving forward to another gameplay stage starts a new score
            // segment, but integrity belongs to the whole run and is retained.
            activeMissionScene = sceneName;
            activeMissionScore = 0;
            Debug.Log(
                $"Entering {sceneName} with {WrongAnswersThisMission} accumulated " +
                $"wrong answer(s), integrity {systemIntegrity}/{SafeNetConstants.MaximumSystemIntegrity}.");
            return;
        }

        activeMissionScene = sceneName;
        systemIntegrity = SafeNetConstants.MaximumSystemIntegrity;
        activeMissionScore = 0;
        Debug.Log($"Starting {sceneName} with full system integrity.");
    }

    private void ApplySceneScoreRequirement(string sceneName)
    {
        // The persistent GameManager must adopt each scene's requirement because
        // duplicate scene managers are destroyed after the first level loads.
        scoreRequiredToPass = sceneName switch
        {
            "Tutorial" => 10,
            "Game(area2)" => 10,
            "Game(area1)" or "Game(area3)" or "Game(area4)" or
            "Game(area5)" or "Game(area6)" => 5,
            _ => scoreRequiredToPass
        };
    }

    public void AddScore(int amount)
    {
        activeMissionScore = Mathf.Max(0, activeMissionScore + amount);
        UpdateOfflineBestScore();
        Debug.Log(
            $"Mission score updated to {CurrentMissionScore}. " +
            $"Best-score total: {totalScore}.");
        UpdateScoreDisplay();
        SaveCheckpoint();
    }

    public void ResetForMission()
    {
        activeMissionScore = 0;
        systemIntegrity = SafeNetConstants.MaximumSystemIntegrity;
        activeMissionScene = SceneManager.GetActiveScene().name;
        UpdateScoreDisplay();
    }

    public void SaveCheckpoint()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.StartsWith("Game(area", System.StringComparison.Ordinal)) return;
        string missionKey = sceneName.Replace("Game(", "").Replace(")", "").Replace(" ", "-").ToLowerInvariant();
        SafeNetApiClient.Instance?.SaveMission(
            missionKey,
            CurrentMissionScore,
            false,
            SafeNetConstants.GradedMissionCount);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveCheckpoint();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SaveCheckpoint();
    }

    public void TakeDamage()
    {
        systemIntegrity--;
        Debug.Log(
            $"Security Breach! Wrong answers: {WrongAnswersThisMission}/" +
            $"{SafeNetConstants.MaximumSystemIntegrity}. System Integrity: {systemIntegrity}");

        if (systemIntegrity <= 0)
        {
            TriggerGameOver();
        }
    }

    private void UpdateScoreDisplay()
    {
        if (scoreUIText != null)
        {
            scoreUIText.text = "Score: " + totalScore;
        }
    }

    /// <summary>
    /// Older scenes contain a plain top-left "Score: 0" label. The styled
    /// GameplaySessionHUD is now the single score display, so the legacy text
    /// is kept inactive only for serialized-scene compatibility.
    /// </summary>
    private void DisableLegacySceneScore()
    {
        GameObject legacyScore = GameObject.Find("ScoreText");
        if (legacyScore != null)
            legacyScore.SetActive(false);
        scoreUIText = null;
    }

    private void TriggerGameOver()
    {
        Debug.Log("CRITICAL FAILURE: System Compromised!");
        string failedMission = SceneManager.GetActiveScene().name;
        if (failedMission.StartsWith("Game(area", System.StringComparison.Ordinal))
        {
            PlayerPrefs.SetString(LastMissionSceneKey, failedMission);
            PlayerPrefs.Save();
            SaveCheckpoint();
        }

        // Game Over ends the current attempt. Its Retry Mission button starts
        // the same stage again with a fresh three-error allowance.
        activeMissionScene = string.Empty;
        systemIntegrity = SafeNetConstants.MaximumSystemIntegrity;
        activeMissionScore = 0;
        UpdateScoreDisplay();
        SceneManager.LoadScene("GameOverScene");
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

        subscribedApi.LoginSucceeded += ApplyStudentProfile;
        subscribedApi.ProfileUpdated += ApplyStudentProfile;
        subscribedApi.LoggedOut += ResetAccountScore;
        ApplyStudentProfile(subscribedApi.CurrentStudent);
    }

    private void UnsubscribeFromApi()
    {
        if (subscribedApi == null)
            return;

        subscribedApi.LoginSucceeded -= ApplyStudentProfile;
        subscribedApi.ProfileUpdated -= ApplyStudentProfile;
        subscribedApi.LoggedOut -= ResetAccountScore;
        subscribedApi = null;
    }

    private void ApplyStudentProfile(StudentProfile student)
    {
        if (student == null)
            return;

        // The server recomputes points from one best result per mission. Its
        // acknowledged total is therefore authoritative for signed-in users,
        // and tester mode uses the exact same rule in memory.
        totalScore = Mathf.Max(0, student.points);
        UpdateScoreDisplay();
    }

    private void ResetAccountScore()
    {
        totalScore = 0;
        activeMissionScore = 0;
        activeMissionScene = string.Empty;
        localMissionBestScores.Clear();
        UpdateScoreDisplay();
    }

    private void UpdateOfflineBestScore()
    {
        SafeNetApiClient api = SafeNetApiClient.Instance;
        if (api != null && api.IsLoggedIn)
            return;

        string missionKey = MissionKey(SceneManager.GetActiveScene().name);
        if (string.IsNullOrEmpty(missionKey))
            return;

        if (!localMissionBestScores.TryGetValue(missionKey, out int best) ||
            CurrentMissionScore > best)
            localMissionBestScores[missionKey] = CurrentMissionScore;

        int bestTotal = 0;
        foreach (int score in localMissionBestScores.Values)
            bestTotal += score;
        totalScore = bestTotal;
    }

    private static string MissionKey(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) ||
            !sceneName.StartsWith("Game(area", System.StringComparison.Ordinal))
            return string.Empty;

        return sceneName.Replace("Game(", "")
            .Replace(")", "")
            .Replace(" ", "-")
            .ToLowerInvariant();
    }
}
