using UnityEngine;

/// <summary>
/// Marks saved Scene-view artwork that represents objects created at runtime.
/// The EditorOnly tag strips it from builds; this extra guard also hides it in
/// Editor Play Mode so it never duplicates the real gameplay objects.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class EditorOnlySceneArtPreview : MonoBehaviour
{
    private void Awake()
    {
        if (Application.isPlaying)
            gameObject.SetActive(false);
    }
}
