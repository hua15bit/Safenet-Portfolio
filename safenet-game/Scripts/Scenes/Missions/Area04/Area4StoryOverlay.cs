using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the selected password terminal close to the player without taking
/// any raycasts. The real Unity choice buttons remain the only touch targets.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area4StoryOverlay : MonoBehaviour
{
    private Image dimmer;
    private Image artwork;

    private void Awake()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -5;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject dimmerObject = new GameObject("Area 4 Focus Backdrop", typeof(RectTransform));
        dimmerObject.transform.SetParent(transform, false);
        dimmer = dimmerObject.AddComponent<Image>();
        dimmer.raycastTarget = false;
        dimmer.color = new Color(0.005f, 0.015f, 0.025f, 0.68f);
        RectTransform dimmerRect = dimmer.rectTransform;
        dimmerRect.anchorMin = Vector2.zero;
        dimmerRect.anchorMax = Vector2.one;
        dimmerRect.offsetMin = Vector2.zero;
        dimmerRect.offsetMax = Vector2.zero;

        GameObject artworkObject = new GameObject("Area 4 Password Terminal", typeof(RectTransform));
        artworkObject.transform.SetParent(transform, false);
        artwork = artworkObject.AddComponent<Image>();
        artwork.raycastTarget = false;
        artwork.preserveAspect = true;
        RectTransform artworkRect = artwork.rectTransform;
        artworkRect.anchorMin = new Vector2(0.5f, 0.5f);
        artworkRect.anchorMax = new Vector2(0.5f, 0.5f);
        artworkRect.pivot = new Vector2(0.5f, 0.5f);
        artworkRect.anchoredPosition = new Vector2(0f, 80f);

        Hide();
    }

    public void Show(Sprite sprite, float targetHeight = 800f)
    {
        if (sprite == null)
        {
            Hide();
            return;
        }

        artwork.sprite = sprite;
        artwork.enabled = true;
        dimmer.enabled = true;

        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        float height = Mathf.Clamp(targetHeight, 520f, 840f);
        float width = Mathf.Min(height * aspect, 1120f);
        artwork.rectTransform.sizeDelta = new Vector2(width, height);
    }

    public void Hide()
    {
        if (artwork != null)
            artwork.enabled = false;
        if (dimmer != null)
            dimmer.enabled = false;
    }
}
