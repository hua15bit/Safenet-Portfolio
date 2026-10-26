using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads and plays one dialogue voice clip at a time from Resources/VoiceLines.
/// The player persists between scenes and follows the shared effects volume.
/// </summary>
public sealed class VoiceLinePlayer : MonoBehaviour
{
    private static VoiceLinePlayer instance;
    private static readonly Dictionary<string, AudioClip> ClipCache =
        new Dictionary<string, AudioClip>();
    private AudioSource source;

    private static VoiceLinePlayer Instance
    {
        get
        {
            if (instance != null) return instance;
            GameObject host = new GameObject("SafeNet Voice Line Player");
            instance = host.AddComponent<VoiceLinePlayer>();
            DontDestroyOnLoad(host);
            return instance;
        }
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        SafeNetAudioManager.Install();
        SafeNetAudioManager.SettingsChanged += ApplyVolume;
        ApplyVolume();
    }

    private void OnDestroy()
    {
        SafeNetAudioManager.SettingsChanged -= ApplyVolume;
        if (instance == this) instance = null;
    }

    private void ApplyVolume()
    {
        if (source != null) source.volume = SafeNetAudioManager.SfxVolume * 0.9f;
    }

    public static float Play(string clipKey, float pitch = 1f)
    {
        // A new scenario always owns the voice channel. Stop the preceding
        // line even when the requested key is empty or the clip is missing.
        Stop();
        if (string.IsNullOrWhiteSpace(clipKey))
            return 0f;

        AudioClip clip = LoadClip(clipKey);
        if (clip == null)
        {
            Debug.LogWarning("SafeNet voice line was not found: " + clipKey);
            return 0f;
        }

        // The editor setup preloads approved clips, but this fallback keeps a
        // freshly added recording from remaining silent on slower phones.
        if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
        {
            Debug.LogWarning("SafeNet voice line could not load audio data: " + clipKey);
            return 0f;
        }

        Instance.source.clip = clip;
        Instance.source.pitch = Mathf.Clamp(pitch, 0.75f, 1.25f);
        Instance.source.Play();
        return clip.length / Mathf.Abs(Instance.source.pitch);
    }

    /// <summary>
    /// Loads a scene's dialogue clips before the player starts a conversation.
    /// This removes the noticeable first-play pause on slower Android devices.
    /// </summary>
    public static void Preload(params string[] clipKeys)
    {
        if (clipKeys == null)
            return;

        // Create and configure the shared AudioSource during scene setup, not
        // on the player's first interaction. This removes the first-line delay
        // that was most noticeable on Aegis at the start of Area 4.
        _ = Instance;

        foreach (string clipKey in clipKeys)
        {
            if (!string.IsNullOrWhiteSpace(clipKey))
                LoadClip(clipKey);
        }
    }

    public static bool IsPlaying =>
        instance != null && instance.source != null && instance.source.isPlaying;

    public static float RemainingTime
    {
        get
        {
            if (!IsPlaying || instance.source.clip == null)
                return 0f;

            float pitch = Mathf.Max(0.01f, Mathf.Abs(instance.source.pitch));
            return Mathf.Max(0f, instance.source.clip.length - instance.source.time) / pitch;
        }
    }

    private static AudioClip LoadClip(string clipKey)
    {
        if (ClipCache.TryGetValue(clipKey, out AudioClip cached) && cached != null)
            return cached;

        AudioClip clip = Resources.Load<AudioClip>("VoiceLines/" + clipKey);
        if (clip == null)
            return null;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        ClipCache[clipKey] = clip;
        return clip;
    }

    public static void Stop()
    {
        if (instance != null && instance.source != null) instance.source.Stop();
    }

    public static void SetPaused(bool paused)
    {
        if (instance == null || instance.source == null) return;
        if (paused) instance.source.Pause();
        else instance.source.UnPause();
    }
}
