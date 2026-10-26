using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persists the player's volume settings, selects music for each scene, and
/// crossfades between tracks without restarting shared menu music.
/// </summary>
public sealed class SafeNetAudioManager : MonoBehaviour
{
    private const string MasterKey = "SafeNet.Audio.Master";
    private const string BgmKey = "SafeNet.Audio.Bgm";
    private const string SfxKey = "SafeNet.Audio.Sfx";

    private static SafeNetAudioManager instance;
    public static event Action SettingsChanged;

    public static float MasterVolume { get; private set; } = 0.85f;
    public static float BgmVolume { get; private set; } = 0.55f;
    public static float SfxVolume { get; private set; } = 0.8f;
    public static string CurrentTrackName =>
        instance != null && instance.musicSource != null && instance.musicSource.clip != null
            ? instance.musicSource.clip.name
            : string.Empty;

    private AudioSource musicSource;
    private SafeNetMusicLibrary musicLibrary;
    private Coroutine musicTransition;
    private float fadeMultiplier = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Install()
    {
        if (instance != null) return;
        GameObject host = new GameObject("SafeNet Audio Manager");
        instance = host.AddComponent<SafeNetAudioManager>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterKey, 0.85f);
        BgmVolume = PlayerPrefs.GetFloat(BgmKey, 0.55f);
        SfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.8f);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;
        musicSource.priority = 32;
        musicLibrary = Resources.Load<SafeNetMusicLibrary>("Music/SafeNetMusicLibrary");

        ApplySettings();
        SceneManager.sceneLoaded += OnSceneLoaded;

        // This also handles Play Mode starting directly inside a gameplay scene.
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded)
            PlayMusicForScene(activeScene.name, true);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name, false);

        if (scene.name != "Start Menu") return;

        Slider[] sliders = Resources.FindObjectsOfTypeAll<Slider>();
        foreach (Slider slider in sliders)
        {
            if (!slider.gameObject.scene.IsValid() || slider.gameObject.scene != scene) continue;
            slider.onValueChanged.RemoveListener(SetMaster);
            slider.onValueChanged.RemoveListener(SetBgm);
            slider.onValueChanged.RemoveListener(SetSfx);

            switch (slider.gameObject.name)
            {
                case "Master":
                    slider.SetValueWithoutNotify(MasterVolume);
                    slider.onValueChanged.AddListener(SetMaster);
                    break;
                case "Music":
                    slider.SetValueWithoutNotify(BgmVolume);
                    slider.onValueChanged.AddListener(SetBgm);
                    break;
                case "SFX":
                    slider.SetValueWithoutNotify(SfxVolume);
                    slider.onValueChanged.AddListener(SetSfx);
                    break;
            }
        }
    }

    public static void SetMaster(float value)
    {
        Install();
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        instance.ApplySettings();
    }

    public static void SetBgm(float value)
    {
        Install();
        BgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmKey, BgmVolume);
        instance.ApplySettings();
    }

    public static void SetSfx(float value)
    {
        Install();
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        instance.ApplySettings();
    }

    private void ApplySettings()
    {
        AudioListener.volume = MasterVolume;
        if (musicSource != null)
            musicSource.volume = BgmVolume * fadeMultiplier;
        PlayerPrefs.Save();
        SettingsChanged?.Invoke();
    }

    /// <summary>
    /// Changes to the track assigned to the loaded scene. Reusing the same
    /// track does not restart it, so menu music remains seamless.
    /// </summary>
    private void PlayMusicForScene(string sceneName, bool immediate)
    {
        AudioClip nextTrack = musicLibrary != null
            ? musicLibrary.TrackForScene(sceneName)
            : Resources.Load<AudioClip>("Music/cyber_lofi");

        if (musicSource.clip == nextTrack)
        {
            if (nextTrack != null && !musicSource.isPlaying)
                musicSource.Play();
            return;
        }

        if (musicTransition != null)
            StopCoroutine(musicTransition);

        if (immediate)
        {
            musicSource.Stop();
            musicSource.clip = nextTrack;
            fadeMultiplier = nextTrack != null ? 1f : 0f;
            ApplySettings();
            if (nextTrack != null)
                musicSource.Play();
            return;
        }

        musicTransition = StartCoroutine(SwitchTrack(nextTrack));
    }

    private IEnumerator SwitchTrack(AudioClip nextTrack)
    {
        const float fadeOutDuration = 0.35f;
        const float fadeInDuration = 0.55f;

        float startingFade = fadeMultiplier;
        for (float elapsed = 0f; elapsed < fadeOutDuration; elapsed += Time.unscaledDeltaTime)
        {
            fadeMultiplier = Mathf.Lerp(startingFade, 0f, elapsed / fadeOutDuration);
            musicSource.volume = BgmVolume * fadeMultiplier;
            yield return null;
        }

        fadeMultiplier = 0f;
        musicSource.Stop();
        musicSource.clip = nextTrack;

        if (nextTrack == null)
        {
            musicSource.volume = 0f;
            musicTransition = null;
            yield break;
        }

        musicSource.Play();
        for (float elapsed = 0f; elapsed < fadeInDuration; elapsed += Time.unscaledDeltaTime)
        {
            fadeMultiplier = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            musicSource.volume = BgmVolume * fadeMultiplier;
            yield return null;
        }

        fadeMultiplier = 1f;
        musicSource.volume = BgmVolume;
        musicTransition = null;
    }
}
