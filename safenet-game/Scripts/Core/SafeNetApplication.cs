using UnityEngine;

/// <summary>
/// Centralizes application exit behavior so the same button works in a build
/// and while testing inside the Unity Editor.
/// </summary>
public static class SafeNetApplication
{
    public static void Quit()
    {
#if UNITY_EDITOR
        Debug.Log("SafeNet: Exit requested. Stopping Play Mode for Editor testing.");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
