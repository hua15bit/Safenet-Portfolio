using TMPro;
using UnityEngine;

/// <summary>
/// Holds the pause-menu artwork and font so every gameplay scene uses one
/// consistent presentation without duplicating asset references.
/// </summary>
[CreateAssetMenu(menuName = "SafeNet/Pause Theme", fileName = "SafeNetPauseTheme")]
public sealed class SafeNetPauseTheme : ScriptableObject
{
    public Sprite pauseIcon;
    public Sprite frame;
    public Sprite resumeButton;
    public Sprite settingsButton;
    public Sprite backToPauseButton;
    public Sprite retryMissionButton;
    public Sprite signOutButton;
    public Sprite missionSelectionButton;
    public Sprite startMenuButton;
    public TMP_FontAsset interfaceFont;
}
