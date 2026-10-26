using UnityEngine;

/// <summary>
/// Fits a UI container inside the device safe area and reapplies the layout
/// when the screen size, orientation, or display cutout changes.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MobileSafeArea : MonoBehaviour
{
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        Apply();
        SafeNetMobileInput.Reset();
    }

    // Runtime builders may parent/stretch this object after Awake has run.
    private void Start() => Apply();

    private void Update()
    {
        if (lastSafeArea != Screen.safeArea ||
            lastScreenSize.x != Screen.width ||
            lastScreenSize.y != Screen.height)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0)
            safe = new Rect(0, 0, Screen.width, Screen.height);
        RectTransform rect = GetComponent<RectTransform>();
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}
