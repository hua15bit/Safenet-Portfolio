using UnityEngine;

/// <summary>
/// Stores the dedicated SafeNet music tracks and maps them to scene names.
/// The asset is kept in Resources so the persistent audio manager can load it
/// before any scene has finished opening.
/// </summary>
public sealed class SafeNetMusicLibrary : ScriptableObject
{
    [SerializeField] private AudioClip musicStart;
    [SerializeField] private AudioClip level1;
    [SerializeField] private AudioClip level2;
    [SerializeField] private AudioClip level3;
    [SerializeField] private AudioClip level4;
    [SerializeField] private AudioClip level5;
    [SerializeField] private AudioClip level6;
    [SerializeField] private AudioClip gameOver;
    [SerializeField] private AudioClip missionComplete;

    public AudioClip TrackForScene(string sceneName)
    {
        return sceneName switch
        {
            "Login" or "Start Menu" or "Mission Select" or "Tutorial" => musicStart,
            "Game(area1)" => level1,
            "Game(area2)" => level2,
            "Game(area3)" => level3,
            "Game(area4)" => level4,
            "Game(area5)" => level5,
            "Game(area6)" => level6,
            "GameOverScene" => gameOver,
            "Game Complete" => missionComplete,

            // The story cinematic already has its own video soundtrack.
            "Story Cinematic" => null,
            _ => musicStart
        };
    }
}
