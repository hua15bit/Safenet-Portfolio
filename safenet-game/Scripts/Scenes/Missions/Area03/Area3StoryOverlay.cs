using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space presenter for Area 3's phone, threat, and completion artwork.
/// It deliberately does not receive raycasts, so the real dialogue buttons stay
/// responsible for every touch on desktop and mobile.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area3StoryOverlay : MonoBehaviour
{
    private Image dimmer;
    private RawImage artwork;
    private TMP_Text countdownText;
    private Coroutine countdownRoutine;

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

        GameObject dimmerObject = new GameObject("Area 3 Focus Backdrop", typeof(RectTransform));
        dimmerObject.transform.SetParent(transform, false);
        dimmer = dimmerObject.AddComponent<Image>();
        dimmer.raycastTarget = false;
        dimmer.color = new Color(0.005f, 0.012f, 0.025f, 0.72f);
        RectTransform dimmerRect = dimmer.rectTransform;
        dimmerRect.anchorMin = Vector2.zero;
        dimmerRect.anchorMax = Vector2.one;
        dimmerRect.offsetMin = Vector2.zero;
        dimmerRect.offsetMax = Vector2.zero;

        GameObject artworkObject = new GameObject("Area 3 Story Artwork", typeof(RectTransform));
        artworkObject.transform.SetParent(transform, false);
        artwork = artworkObject.AddComponent<RawImage>();
        artwork.raycastTarget = false;
        artwork.color = Color.white;
        RectTransform artworkRect = artwork.rectTransform;
        artworkRect.anchorMin = new Vector2(0.5f, 0.5f);
        artworkRect.anchorMax = new Vector2(0.5f, 0.5f);
        artworkRect.pivot = new Vector2(0.5f, 0.5f);
        artworkRect.anchoredPosition = new Vector2(0f, 90f);

        GameObject timerObject = new GameObject("Area 3 Countdown", typeof(RectTransform));
        timerObject.transform.SetParent(transform, false);
        countdownText = timerObject.AddComponent<TextMeshProUGUI>();
        countdownText.raycastTarget = false;
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.fontSize = 42f;
        countdownText.color = new Color(1f, 0.22f, 0.3f, 1f);
        countdownText.text = string.Empty;
        Outline timerOutline = timerObject.AddComponent<Outline>();
        timerOutline.effectColor = new Color(0.02f, 0.03f, 0.07f, 0.95f);
        timerOutline.effectDistance = new Vector2(3f, -3f);
        RectTransform timerRect = countdownText.rectTransform;
        timerRect.anchorMin = new Vector2(0.5f, 0.5f);
        timerRect.anchorMax = new Vector2(0.5f, 0.5f);
        timerRect.pivot = new Vector2(0.5f, 0.5f);
        timerRect.anchoredPosition = new Vector2(285f, 325f);
        timerRect.sizeDelta = new Vector2(180f, 70f);

        Hide();
    }

    public void Show(Texture texture, bool showCountdown = false, float targetHeight = 760f)
    {
        Show(texture, new Rect(0f, 0f, 1f, 1f), showCountdown, targetHeight);
    }

    public void ShowSheetFrame(
        Texture texture,
        int frameIndex,
        int columns,
        int rows,
        float targetHeight = 720f)
    {
        if (texture == null || columns <= 0 || rows <= 0)
        {
            Hide();
            return;
        }

        int safeIndex = Mathf.Clamp(frameIndex, 0, columns * rows - 1);
        int column = safeIndex % columns;
        int rowFromTop = safeIndex / columns;
        float width = 1f / columns;
        float height = 1f / rows;
        Rect frame = new Rect(column * width, 1f - (rowFromTop + 1) * height, width, height);
        Show(texture, frame, false, targetHeight);
    }

    public void Hide()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        if (artwork != null)
            artwork.enabled = false;
        if (dimmer != null)
            dimmer.enabled = false;
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private void Show(Texture texture, Rect uvRect, bool showCountdown, float targetHeight)
    {
        if (texture == null)
        {
            Hide();
            return;
        }

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        artwork.texture = texture;
        artwork.uvRect = uvRect;
        artwork.enabled = true;
        if (dimmer != null)
            dimmer.enabled = true;

        float sampledWidth = Mathf.Max(1f, texture.width * uvRect.width);
        float sampledHeight = Mathf.Max(1f, texture.height * uvRect.height);
        float aspect = sampledWidth / sampledHeight;
        float height = Mathf.Clamp(targetHeight, 420f, 820f);
        float width = Mathf.Min(height * aspect, 1120f);
        artwork.rectTransform.sizeDelta = new Vector2(width, height);

        countdownText.gameObject.SetActive(showCountdown);
        if (showCountdown)
            countdownRoutine = StartCoroutine(PlayCountdown());
    }

    private IEnumerator PlayCountdown()
    {
        for (int seconds = 30; seconds >= 0; seconds--)
        {
            countdownText.text = $"00:{seconds:00}";
            countdownText.color = seconds <= 10
                ? new Color(1f, 0.08f, 0.18f, 1f)
                : new Color(1f, 0.35f, 0.18f, 1f);
            yield return new WaitForSecondsRealtime(1f);
        }

        countdownRoutine = null;
    }
}
