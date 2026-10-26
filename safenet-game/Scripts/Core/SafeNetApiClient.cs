using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides the single boundary between Unity and SafeNet account/progress data.
/// It supports an in-memory tester profile now and an HTTPS backend when enabled.
/// </summary>
public class SafeNetApiClient : MonoBehaviour
{
    /// <summary>Overall state of the durable in-memory progress upload queue.</summary>
    public enum ProgressSyncState
    {
        Idle,
        Pending,
        Succeeded,
        Failed
    }

    public static SafeNetApiClient Instance { get; private set; }

    [Header("SafeNet Website Connection")]
    [Tooltip("Allows the game to authenticate and synchronize scores through the website API.")]
    [SerializeField] private bool liveApiEnabled;

    [Tooltip("Base URL of the SafeNet website API. Never put a database connection string here.")]
    [SerializeField] private string apiBaseUrl = "";

    [Tooltip("Student registration page opened by the Create Account button.")]
    [SerializeField] private string registrationUrl = "";

    [Tooltip("Maximum time Unity waits for the website API before reporting a connection error.")]
    [SerializeField] private int requestTimeoutSeconds = 15;

    public StudentProfile CurrentStudent { get; private set; }
    public bool IsTesterMode { get; private set; }
    public bool IsLoggedIn => IsTesterMode || !string.IsNullOrEmpty(Token);
    public bool IsLiveApiConfigured => liveApiEnabled && IsAllowedServiceUrl(apiBaseUrl);
    public ProgressSyncState CurrentProgressSyncState { get; private set; } = ProgressSyncState.Idle;
    public int PendingProgressCount => pendingProgressSaves.Count + (activeProgressSave != null ? 1 : 0);
    public int FailedProgressCount => failedProgressSaves.Count;
    public string LastProgressSyncError { get; private set; } = string.Empty;

    public event Action<StudentProfile> LoginSucceeded;
    public event Action<StudentProfile> ProfileUpdated;
    public event Action ProgressSyncChanged;
    public event Action<string> RequestFailed;
    public event Action LoggedOut;

    private const string ConfigResourceName = "SafeNetBackendConfig";
    private const int MaximumProgressRetries = 3;
    private const float InitialProgressRetryDelaySeconds = 1f;
    private const float MaximumProgressRetryDelaySeconds = 8f;
    private readonly Dictionary<string, int> testerMissionScores = new Dictionary<string, int>();
    private readonly HashSet<string> testerCompletedMissions = new HashSet<string>();
    private readonly List<PendingProgressSave> pendingProgressSaves = new List<PendingProgressSave>();
    private readonly List<PendingProgressSave> failedProgressSaves = new List<PendingProgressSave>();
    private PendingProgressSave activeProgressSave;
    private Coroutine progressQueueCoroutine;

    private string Token
    {
        get => SafeNetTokenStore.Read();
        set => SafeNetTokenStore.Write(value);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplyBundledConfiguration();

        // A token is useful only while a website API is configured.
        if (!IsLiveApiConfigured)
            Token = "";
    }

    public void OpenRegistration()
    {
        if (!IsLiveApiConfigured || !IsAllowedServiceUrl(registrationUrl))
        {
            RequestFailed?.Invoke("STUDENT REGISTRATION IS UNAVAILABLE UNTIL THE SAFENET WEBSITE API IS CONFIGURED.");
            return;
        }
        Application.OpenURL(registrationUrl);
    }

    public void Login(string identifier, string password, string classCode)
    {
        IsTesterMode = false;
        if (!RequireLiveApi())
            return;
        StartCoroutine(LoginRequest(identifier, password, classCode));
    }

    public void LoginAsTester()
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            RequestFailed?.Invoke("DEVELOPMENT LOGIN IS DISABLED IN THIS BUILD.");
            return;
        }
        IsTesterMode = true;
        Token = "";
        testerMissionScores.Clear();
        testerCompletedMissions.Clear();
        ResetProgressSync();
        CurrentStudent = new StudentProfile
        {
            fullname = "SafeNet Debug Account",
            student_number = "DEBUG-ACCOUNT",
            points = 0,
            missions_completed = 0,
            total_missions = SafeNetConstants.GradedMissionCount,
            @class = new StudentClassInfo { section = "Development Test", class_code = "DEBUG1" }
        };
        LoginSucceeded?.Invoke(CurrentStudent);
    }

    public void RestoreSession()
    {
        if (!IsLiveApiConfigured)
        {
            Token = "";
            return;
        }
        if (string.IsNullOrEmpty(Token))
            return;
        StartCoroutine(ProfileRequest());
    }

    public void SaveMission(
        string missionKey,
        int missionScore,
        bool completed,
        int totalMissions = SafeNetConstants.GradedMissionCount)
    {
        if (IsTesterMode)
        {
            int safeScore = Mathf.Max(0, missionScore);
            if (!testerMissionScores.TryGetValue(missionKey, out int existingScore) || safeScore > existingScore)
                testerMissionScores[missionKey] = safeScore;
            if (completed)
                testerCompletedMissions.Add(missionKey);

            int totalPoints = 0;
            foreach (int score in testerMissionScores.Values)
                totalPoints += score;
            CurrentStudent.points = totalPoints;
            CurrentStudent.missions_completed = Mathf.Min(totalMissions, testerCompletedMissions.Count);
            ProfileUpdated?.Invoke(CurrentStudent);
            CurrentProgressSyncState = ProgressSyncState.Succeeded;
            LastProgressSyncError = string.Empty;
            ProgressSyncChanged?.Invoke();
            return;
        }
        if (!IsLiveApiConfigured)
        {
            SetProgressFailure(
                "The SafeNet website API is not configured. Progress remains in this training session.");
            RequestFailed?.Invoke(
                "THE SAFENET WEBSITE API IS NOT CONFIGURED. USE THE DEVELOPMENT ACCOUNT FOR OFFLINE TESTING.");
            return;
        }
        if (string.IsNullOrEmpty(Token))
        {
            const string message = "Sign in before saving progress.";
            SetProgressFailure(message);
            RequestFailed?.Invoke(message);
            return;
        }

        EnqueueProgressSave(missionKey, missionScore, completed);
    }

    /// <summary>
    /// Requeues uploads that exhausted their bounded retry budget. This is
    /// called when the completion screen is opened and may also be wired to a
    /// future explicit Retry button.
    /// </summary>
    public void RetryFailedProgress()
    {
        if (failedProgressSaves.Count == 0 || !IsLiveApiConfigured || string.IsNullOrEmpty(Token))
            return;

        PendingProgressSave[] retryItems = failedProgressSaves.ToArray();
        failedProgressSaves.Clear();
        LastProgressSyncError = string.Empty;
        foreach (PendingProgressSave item in retryItems)
            MergePendingProgress(item.missionKey, item.missionScore, item.completed);

        StartProgressQueue();
    }

    public void Logout()
    {
        if (IsTesterMode || string.IsNullOrEmpty(Token) || !IsLiveApiConfigured)
        {
            ClearSession();
            SceneManager.LoadScene("Login");
            return;
        }

        // Never leave the student waiting on a sleeping or unavailable web
        // service. Remove the local credential and return to Login now, while
        // this persistent client sends a best-effort server notification.
        string authorizationToken = Token;
        ClearSession();
        StartCoroutine(LogoutNotificationRequest(authorizationToken));
        SceneManager.LoadScene("Login");
    }

    public void LogoutAndExit()
    {
        if (IsTesterMode || string.IsNullOrEmpty(Token) || !IsLiveApiConfigured)
        {
            ClearSession();
            SafeNetApplication.Quit();
            return;
        }
        StartCoroutine(LogoutRequest(true));
    }

    /// <summary>Overrides the bundled website configuration for a deployed environment.</summary>
    public void ConfigureFutureBackend(string baseUrl, string signUpUrl, bool enableConnection)
    {
        apiBaseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        registrationUrl = (signUpUrl ?? "").Trim();
        liveApiEnabled = enableConnection && !string.IsNullOrWhiteSpace(apiBaseUrl);
        ClearSession();
    }

    private void ApplyBundledConfiguration()
    {
        TextAsset configAsset = Resources.Load<TextAsset>(ConfigResourceName);
        if (configAsset == null)
            return;

        SafeNetBackendConfig config;
        try
        {
            config = JsonUtility.FromJson<SafeNetBackendConfig>(configAsset.text);
        }
        catch (ArgumentException error)
        {
            Debug.LogError($"SafeNet backend configuration is invalid: {error.Message}", this);
            return;
        }

        if (config == null)
            return;

        liveApiEnabled = config.live_api_enabled;
        if (!string.IsNullOrWhiteSpace(config.api_base_url))
            apiBaseUrl = config.api_base_url.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(config.registration_url))
            registrationUrl = config.registration_url.Trim();
        // Free hosting can need more than a minute to wake after inactivity.
        // Keep the value bounded, but allow the production configuration enough time.
        if (config.request_timeout_seconds > 0)
            requestTimeoutSeconds = Mathf.Clamp(config.request_timeout_seconds, 5, 120);
    }

    private bool RequireLiveApi()
    {
        if (IsLiveApiConfigured) return true;
        RequestFailed?.Invoke("THE SAFENET WEBSITE API IS NOT CONFIGURED. USE THE DEVELOPMENT ACCOUNT FOR OFFLINE TESTING.");
        return false;
    }

    private static bool IsAllowedServiceUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) || !string.IsNullOrEmpty(uri.UserInfo))
            return false;
        if (uri.Scheme == Uri.UriSchemeHttps) return true;
        return (Application.isEditor || Debug.isDebugBuild) &&
            uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
    }

    private IEnumerator LoginRequest(string identifier, string password, string classCode)
    {
        string json = JsonUtility.ToJson(new LoginRequest
        {
            identifier = identifier.Trim(),
            password = password,
            class_code = classCode.Trim().ToUpperInvariant()
        });
        using UnityWebRequest request = JsonRequest("auth/students/login", "POST", json, false);
        yield return request.SendWebRequest();
        LoginResponse response = Parse<LoginResponse>(request);
        if (response == null || !response.success || string.IsNullOrEmpty(response.token))
        {
            RequestFailed?.Invoke(Error(request, response?.message));
            yield break;
        }
        Token = response.token;
        CurrentStudent = response.student;
        LoginSucceeded?.Invoke(CurrentStudent);
    }

    private IEnumerator ProfileRequest()
    {
        using UnityWebRequest request = JsonRequest("students/me", "GET");
        yield return request.SendWebRequest();
        StudentResponse response = Parse<StudentResponse>(request);
        if (response == null || !response.success)
        {
            AuthorizedError(request, response?.message);
            yield break;
        }
        CurrentStudent = response.student;
        ProfileUpdated?.Invoke(CurrentStudent);
    }

    private void EnqueueProgressSave(string missionKey, int missionScore, bool completed)
    {
        string normalizedKey = (missionKey ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedKey))
        {
            const string message = "Cannot synchronize progress without a mission key.";
            SetProgressFailure(message);
            RequestFailed?.Invoke(message);
            return;
        }

        // A newer save for a failed mission supersedes that failed payload.
        // Merge it back into the live queue so a higher score or completion
        // flag is never lost.
        for (int index = failedProgressSaves.Count - 1; index >= 0; index--)
        {
            PendingProgressSave failed = failedProgressSaves[index];
            if (!string.Equals(failed.missionKey, normalizedKey, StringComparison.Ordinal))
                continue;

            missionScore = Mathf.Max(missionScore, failed.missionScore);
            completed |= failed.completed;
            failedProgressSaves.RemoveAt(index);
        }

        MergePendingProgress(normalizedKey, missionScore, completed);
        StartProgressQueue();
    }

    private void MergePendingProgress(string missionKey, int missionScore, bool completed)
    {
        int safeScore = Mathf.Max(0, missionScore);
        for (int index = 0; index < pendingProgressSaves.Count; index++)
        {
            PendingProgressSave pending = pendingProgressSaves[index];
            if (!string.Equals(pending.missionKey, missionKey, StringComparison.Ordinal))
                continue;

            pending.missionScore = Mathf.Max(pending.missionScore, safeScore);
            pending.completed |= completed;
            return;
        }

        pendingProgressSaves.Add(new PendingProgressSave
        {
            missionKey = missionKey,
            missionScore = safeScore,
            completed = completed
        });
    }

    private void StartProgressQueue()
    {
        CurrentProgressSyncState = ProgressSyncState.Pending;
        LastProgressSyncError = string.Empty;
        ProgressSyncChanged?.Invoke();
        if (progressQueueCoroutine == null)
            progressQueueCoroutine = StartCoroutine(ProcessProgressQueue());
    }

    private IEnumerator ProcessProgressQueue()
    {
        while (pendingProgressSaves.Count > 0)
        {
            activeProgressSave = pendingProgressSaves[0];
            pendingProgressSaves.RemoveAt(0);

            bool accepted = false;
            string finalError = string.Empty;
            for (int attempt = 0; attempt <= MaximumProgressRetries; attempt++)
            {
                string json = JsonUtility.ToJson(new ProgressRequest
                {
                    mission_key = activeProgressSave.missionKey,
                    mission_score = activeProgressSave.missionScore,
                    completed = activeProgressSave.completed
                });

                using UnityWebRequest request = JsonRequest("students/progress", "POST", json);
                yield return request.SendWebRequest();
                StudentResponse response = Parse<StudentResponse>(request);
                if (response != null && response.success && response.student != null)
                {
                    CurrentStudent = response.student;
                    ProfileUpdated?.Invoke(CurrentStudent);
                    RemoveFailuresSupersededBy(activeProgressSave);
                    accepted = true;
                    break;
                }

                finalError = Error(request, response?.message);
                bool canRetry = IsTransientProgressFailure(request) &&
                                attempt < MaximumProgressRetries;
                if (!canRetry)
                {
                    if (request.responseCode == 401)
                        ExpireAuthorizedSession();
                    break;
                }

                float delay = Mathf.Min(
                    MaximumProgressRetryDelaySeconds,
                    InitialProgressRetryDelaySeconds * Mathf.Pow(2f, attempt));
                yield return new WaitForSecondsRealtime(delay);
            }

            if (!accepted)
            {
                failedProgressSaves.Add(activeProgressSave);
                LastProgressSyncError = string.IsNullOrWhiteSpace(finalError)
                    ? "The progress server did not confirm the upload."
                    : finalError;
                RequestFailed?.Invoke(LastProgressSyncError);
            }

            activeProgressSave = null;
            CurrentProgressSyncState = pendingProgressSaves.Count > 0
                ? ProgressSyncState.Pending
                : failedProgressSaves.Count > 0
                    ? ProgressSyncState.Failed
                    : ProgressSyncState.Succeeded;
            ProgressSyncChanged?.Invoke();
        }

        progressQueueCoroutine = null;
    }

    private void RemoveFailuresSupersededBy(PendingProgressSave accepted)
    {
        for (int index = failedProgressSaves.Count - 1; index >= 0; index--)
        {
            PendingProgressSave failed = failedProgressSaves[index];
            bool sameMission = string.Equals(
                failed.missionKey,
                accepted.missionKey,
                StringComparison.Ordinal);
            bool scoreCovered = accepted.missionScore >= failed.missionScore;
            bool completionCovered = !failed.completed || accepted.completed;
            if (sameMission && scoreCovered && completionCovered)
                failedProgressSaves.RemoveAt(index);
        }

        if (failedProgressSaves.Count == 0)
            LastProgressSyncError = string.Empty;
    }

    private static bool IsTransientProgressFailure(UnityWebRequest request)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.DataProcessingError)
            return true;

        long code = request.responseCode;
        return code == 408 || code == 425 || code == 429 || code >= 500;
    }

    private IEnumerator LogoutRequest(bool exitAfter)
    {
        using UnityWebRequest request = JsonRequest("auth/students/logout", "POST", "{}");
        yield return request.SendWebRequest();
        ClearSession();
        if (exitAfter) SafeNetApplication.Quit();
        else SceneManager.LoadScene("Login");
    }

    private IEnumerator LogoutNotificationRequest(string authorizationToken)
    {
        using UnityWebRequest request = JsonRequest(
            "auth/students/logout", "POST", "{}", false);
        request.SetRequestHeader("Authorization", "Bearer " + authorizationToken);
        yield return request.SendWebRequest();
        // Local sign-out is already complete. A network failure must not
        // restore the session or interrupt the Login screen.
    }

    private UnityWebRequest JsonRequest(string endpoint, string method, string json = null, bool authenticate = true)
    {
        if (!IsAllowedServiceUrl(apiBaseUrl))
            throw new InvalidOperationException("SafeNet requires an HTTPS API address.");
        UnityWebRequest request = new UnityWebRequest(apiBaseUrl.TrimEnd('/') + "/" + endpoint, method);
        // Credentials must not follow a redirect to another origin.
        request.redirectLimit = 0;
        request.downloadHandler = new DownloadHandlerBuffer();
        if (json != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }
        request.SetRequestHeader("Accept", "application/json");
        if (authenticate && !string.IsNullOrEmpty(Token))
            request.SetRequestHeader("Authorization", "Bearer " + Token);
        request.timeout = requestTimeoutSeconds;
        return request;
    }

    private static T Parse<T>(UnityWebRequest request) where T : class
    {
        try
        {
            string json = request.downloadHandler?.text;
            return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<T>(json);
        }
        catch
        {
            // Invalid or empty server responses are handled by the caller's failure path.
            return null;
        }
    }

    private static string Error(UnityWebRequest request, string message)
    {
        if (!string.IsNullOrWhiteSpace(message)) return message;
        return request.result == UnityWebRequest.Result.ConnectionError
            ? "Cannot reach the SafeNet server. Check Wi-Fi and the server address."
            : "The SafeNet request failed.";
    }

    private void AuthorizedError(UnityWebRequest request, string message)
    {
        if (request.responseCode == 401) ClearSession();
        RequestFailed?.Invoke(Error(request, message));
    }

    private void ExpireAuthorizedSession()
    {
        // Keep the last acknowledged profile visible on the result screen,
        // but remove the credential so Unity cannot claim or attempt further
        // authenticated synchronization until the student signs in again.
        Token = "";
        IsTesterMode = false;
    }

    private void SetProgressFailure(string message)
    {
        LastProgressSyncError = message ?? string.Empty;
        CurrentProgressSyncState = ProgressSyncState.Failed;
        ProgressSyncChanged?.Invoke();
    }

    private void ResetProgressSync()
    {
        if (progressQueueCoroutine != null)
        {
            StopCoroutine(progressQueueCoroutine);
            progressQueueCoroutine = null;
        }
        pendingProgressSaves.Clear();
        failedProgressSaves.Clear();
        activeProgressSave = null;
        LastProgressSyncError = string.Empty;
        CurrentProgressSyncState = ProgressSyncState.Idle;
        ProgressSyncChanged?.Invoke();
    }

    private void ClearSession()
    {
        ResetProgressSync();
        Token = "";
        IsTesterMode = false;
        CurrentStudent = null;
        testerMissionScores.Clear();
        testerCompletedMissions.Clear();
        LoggedOut?.Invoke();
    }
}

/// <summary>One coalesced mission save retained while Unity changes scenes.</summary>
internal sealed class PendingProgressSave
{
    public string missionKey;
    public int missionScore;
    public bool completed;
}

/// <summary>JSON payload sent to the student login endpoint.</summary>
[Serializable]
public class LoginRequest
{
    public string identifier;
    public string password;
    public string class_code;
}

/// <summary>JSON payload used to save one mission's best progress.</summary>
[Serializable]
public class ProgressRequest
{
    public string mission_key;
    public int mission_score;
    public bool completed;
}

/// <summary>Fields common to backend responses.</summary>
[Serializable]
public class ApiResponse
{
    public bool success;
    public string message;
}

/// <summary>Login response containing a token and student profile.</summary>
[Serializable]
public class LoginResponse : ApiResponse
{
    public string token;
    public StudentProfile student;
}
/// <summary>Backend response containing an updated student profile.</summary>
[Serializable]
public class StudentResponse : ApiResponse
{
    public StudentProfile student;
}

/// <summary>Serializable student data shared with the faculty backend.</summary>
[Serializable]
public class StudentProfile
{
    public string id;
    public string fullname;
    public string student_number;
    public string program;
    public string email;
    public int points;
    public int missions_completed;
    public int total_missions;
    public StudentClassInfo @class;
}
/// <summary>Serializable class details attached to a student profile.</summary>
[Serializable]
public class StudentClassInfo
{
    public string id;
    public string subject;
    public string section;
    public string term;
    public string class_code;
}

/// <summary>Values loaded from Resources/SafeNetBackendConfig.json.</summary>
[Serializable]
internal sealed class SafeNetBackendConfig
{
    // JsonUtility assigns these public fields when loading the bundled JSON.
    public bool live_api_enabled = false;
    public string api_base_url = "";
    public string registration_url = "";
    public int request_timeout_seconds = 15;
}
