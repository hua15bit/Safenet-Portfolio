using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Provides the shared UI and interaction sound effects. It automatically
/// connects saved scene buttons and can also connect buttons created at runtime.
/// </summary>
public sealed class SafeNetSfxPlayer : MonoBehaviour
{
    private static SafeNetSfxPlayer instance;
    private readonly HashSet<Button> hookedButtons = new HashSet<Button>();
    private AudioSource source;
    private AudioClip buttonClip;
    private AudioClip interactClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        GameObject host = new GameObject("SafeNet SFX Player");
        instance = host.AddComponent<SafeNetSfxPlayer>();
        DontDestroyOnLoad(host);
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
        buttonClip = Resources.Load<AudioClip>("Sfx/ui_click");
        interactClip = Resources.Load<AudioClip>("Sfx/interact");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SafeNetAudioManager.SettingsChanged -= ApplyVolume;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void ApplyVolume()
    {
        if (source != null) source.volume = SafeNetAudioManager.SfxVolume * 0.75f;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        hookedButtons.Clear();
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (Button button in buttons) HookButton(button);
    }

    public static void HookButton(Button button)
    {
        if (button == null) return;
        Install();
        if (!instance.hookedButtons.Add(button)) return;
        button.onClick.AddListener(PlayButton);
    }

    public static void PlayButton()
    {
        Install();
        instance.Play(instance.buttonClip, 1f);
    }

    public static void PlayInteract()
    {
        Install();
        instance.Play(instance.interactClip, 1f);
    }

    private void Play(AudioClip clip, float pitch)
    {
        if (clip == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip);
    }
}
