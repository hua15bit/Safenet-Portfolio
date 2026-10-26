using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Converts pointer press and release events from an on-screen control into
/// the shared mobile input state used by PlayerMovement.
/// </summary>
public class MobileControlButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Control
    {
        MoveLeft,
        MoveRight,
        Interact
    }

    [SerializeField] private Control control;
    [SerializeField] private Image background;
    [SerializeField] private Color idleColor = new Color(0.025f, 0.12f, 0.2f, 0.3f);
    [SerializeField] private Color pressedColor = new Color(0.09f, 0.78f, 1f, 0.95f);
    private bool usingSuppliedArtwork;
    private bool baseLayoutCaptured;
    private Vector2 baseAnchoredPosition;

    private bool InteractionIsBlocked =>
        control == Control.Interact && DialogueController.IsAwaitingChoice;

    private void Awake()
    {
        if (background == null) background = GetComponent<Image>();
        ApplySuppliedArtwork();
        ApplySizeScale(SafeNetControlSizeSettings.CurrentScale);
        SetVisual(false);
    }

    /// <summary>
    /// Applies the accessibility preset to the real RectTransform so the
    /// visible control and its touch hitbox always remain the same size.
    /// </summary>
    public void ApplySizeScale(float scale)
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect == null) return;

        if (!baseLayoutCaptured)
        {
            baseLayoutCaptured = true;
            baseAnchoredPosition = rect.anchoredPosition;
        }

        Vector2 baseSize = control == Control.Interact
            ? new Vector2(128f, 128f)
            : new Vector2(140f, 140f);
        float safeScale = Mathf.Clamp(scale, 1f, 1.36f);
        rect.sizeDelta = baseSize * safeScale;

        // The movement controls use a left-edge pivot. Shift the right button
        // by the left button's growth so the pair keeps its original gap even
        // at the largest accessibility preset.
        rect.anchoredPosition = control == Control.MoveRight
            ? baseAnchoredPosition + Vector2.right * (baseSize.x * (safeScale - 1f))
            : baseAnchoredPosition;
    }

    private void ApplySuppliedArtwork()
    {
        SafeNetMobileControlTheme theme =
            Resources.Load<SafeNetMobileControlTheme>("SafeNetMobileControlTheme");
        if (theme == null || background == null) return;

        Sprite sprite = control switch
        {
            Control.MoveLeft => theme.moveLeft,
            Control.MoveRight => theme.moveRight,
            Control.Interact => theme.interact,
            _ => null
        };
        if (sprite == null) return;

        usingSuppliedArtwork = true;
        background.sprite = sprite;
        background.type = Image.Type.Simple;
        background.preserveAspect = true;
        background.raycastTarget = true;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = control == Control.Interact
                ? new Vector2(128f, 128f)
                : new Vector2(140f, 140f);

        Outline outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        foreach (Transform child in transform)
            if (child.name == "Label") child.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // A decision must be made with one of its visible choice buttons.
        // Do not queue an Interact press behind the choice overlay.
        if (InteractionIsBlocked)
        {
            SetVisual(false);
            return;
        }

        SetVisual(true);
        switch (control)
        {
            case Control.MoveLeft:
                SafeNetMobileInput.SetLeft(true);
                break;
            case Control.MoveRight:
                SafeNetMobileInput.SetRight(true);
                break;
            case Control.Interact:
                SafeNetMobileInput.PressInteract();
                break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleaseMovement();
        SetVisual(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ReleaseMovement();
        SetVisual(false);
    }

    private void OnDisable()
    {
        ReleaseMovement();
        SetVisual(false);
    }

    private void LateUpdate()
    {
        if (control != Control.Interact || background == null)
            return;

        bool blocked = InteractionIsBlocked;
        background.raycastTarget = !blocked;
        if (blocked)
            SetVisual(false);
    }

    private void ReleaseMovement()
    {
        if (control == Control.MoveLeft) SafeNetMobileInput.SetLeft(false);
        if (control == Control.MoveRight) SafeNetMobileInput.SetRight(false);
    }

    private void SetVisual(bool pressed)
    {
        if (background != null)
            background.color = usingSuppliedArtwork
                ? pressed ? new Color(0.68f, 0.94f, 1f, 1f) : Color.white
                : pressed ? pressedColor : idleColor;

        // Never scale the RectTransform that owns the raycast target. Scaling
        // it on pointer-down made edge touches leave the newly shrunken hitbox,
        // cancelling the visual while the queued interaction still executed.
        transform.localScale = Vector3.one;
    }
}
