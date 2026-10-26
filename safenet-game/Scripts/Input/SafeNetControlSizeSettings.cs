using UnityEngine;

/// <summary>
/// Stores the player's touch-control size preference and reapplies it whenever
/// gameplay controls are created. Presets enlarge both the artwork and its
/// raycast hitbox, making the controls easier to use on a phone.
/// </summary>
public static class SafeNetControlSizeSettings
{
    private const string PreferenceKey = "SafeNet.Controls.SizePreset";
    private const int DefaultPreset = 1;

    public static int CurrentPreset =>
        Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, DefaultPreset), 1, 3);

    public static float CurrentScale => ScaleForPreset(CurrentPreset);

    public static void SetPreset(int preset)
    {
        int safePreset = Mathf.Clamp(preset, 1, 3);
        PlayerPrefs.SetInt(PreferenceKey, safePreset);
        PlayerPrefs.Save();
        ApplyToLoadedControls();
    }

    public static float ScaleForPreset(int preset)
    {
        return Mathf.Clamp(preset, 1, 3) switch
        {
            1 => 1f,
            2 => 1.18f,
            3 => 1.36f,
            _ => 1.18f
        };
    }

    public static void ApplyToLoadedControls()
    {
        MobileControlButton[] controls = Object.FindObjectsByType<MobileControlButton>(
            FindObjectsInactive.Include);
        foreach (MobileControlButton control in controls)
            control.ApplySizeScale(CurrentScale);
    }
}
