using UnityEngine;

/// <summary>
/// Holds the artwork shared by the left, right, and interact mobile controls.
/// </summary>
[CreateAssetMenu(menuName = "SafeNet/Mobile Control Theme", fileName = "SafeNetMobileControlTheme")]
public sealed class SafeNetMobileControlTheme : ScriptableObject
{
    public Sprite moveLeft;
    public Sprite moveRight;
    public Sprite interact;
}
