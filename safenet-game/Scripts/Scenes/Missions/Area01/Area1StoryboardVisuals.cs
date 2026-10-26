using UnityEngine;

/// <summary>
/// Recreates the Area 1 storyboard beats by changing the terminal, monitor,
/// mail alert, and exit artwork as the phishing conversation progresses.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area1StoryboardVisuals : MonoBehaviour
{
    [Header("Scene Renderers")]
    [SerializeField] private SpriteRenderer terminalRenderer;
    [SerializeField] private SpriteRenderer monitorRenderer;
    [SerializeField] private SpriteRenderer mailRenderer;
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private SpriteRenderer choiceMonitorRenderer;

    [Header("Terminal States")]
    [SerializeField] private Sprite terminalNormal;
    [SerializeField] private Sprite terminalUnsafe;
    [SerializeField] private Sprite terminalSecured;

    [Header("Monitor States")]
    [SerializeField] private Sprite monitorUnsafe;
    [SerializeField] private Sprite monitorSecured;

    [Header("Mail States")]
    [SerializeField] private Sprite mailUnsafe;
    [SerializeField] private Sprite mailSecured;

    [Header("Door States")]
    [SerializeField] private Sprite doorUnsafe;
    [SerializeField] private Sprite doorSecured;

    [Header("Ground Alignment")]
    [SerializeField] private float groundY = -5.1f;

    private Vector3 mailBasePosition;
    private Vector3 mailBaseScale;
    private Vector3 choiceMonitorBaseScale;
    private float choiceMonitorVisibility;
    private bool choiceMonitorVisible;
    private bool secured;

    private void Awake()
    {
        if (mailRenderer != null)
        {
            mailBasePosition = mailRenderer.transform.position;
            mailBaseScale = mailRenderer.transform.localScale;
        }

        if (choiceMonitorRenderer != null)
        {
            choiceMonitorBaseScale = choiceMonitorRenderer.transform.localScale;
            choiceMonitorRenderer.enabled = false;
        }

        ShowArrivalScene();
    }

    private void Update()
    {
        if (mailRenderer != null)
        {
            float speed = secured ? 1.7f : 3.2f;
            float wave = Mathf.Sin(Time.time * speed);
            mailRenderer.transform.position = mailBasePosition + Vector3.up * (wave * 0.08f);
            mailRenderer.transform.localScale = mailBaseScale * (1f + wave * 0.035f);
        }

        AnimateChoiceMonitor();
    }

    public void ShowArrivalScene()
    {
        SetChoiceMonitorVisible(false);
        secured = false;
        SetGroundedSprite(terminalRenderer, terminalNormal);
        SetGroundedSprite(monitorRenderer, monitorUnsafe);
        SetSprite(mailRenderer, mailUnsafe);
        SetGroundedSprite(doorRenderer, doorUnsafe);
    }

    public void ShowIncomingMessage()
    {
        SetChoiceMonitorVisible(false);
        secured = false;
        SetGroundedSprite(terminalRenderer, terminalUnsafe);
        SetGroundedSprite(monitorRenderer, monitorUnsafe);
        SetSprite(mailRenderer, mailUnsafe);
        SetGroundedSprite(doorRenderer, doorUnsafe);
    }

    public void ShowChoices()
    {
        ShowIncomingMessage();
        SetChoiceMonitorVisible(true);
    }

    public void ShowChoiceResult(int scoreModifier)
    {
        SetChoiceMonitorVisible(false);

        if (scoreModifier > 0)
            ShowSecuredScene();
        else
            ShowUnsafeScene();
    }

    private void ShowSecuredScene()
    {
        secured = true;
        SetGroundedSprite(terminalRenderer, terminalSecured);
        SetGroundedSprite(monitorRenderer, monitorSecured);
        SetSprite(mailRenderer, mailSecured);
        SetGroundedSprite(doorRenderer, doorSecured);
    }

    private void ShowUnsafeScene()
    {
        secured = false;
        SetGroundedSprite(terminalRenderer, terminalUnsafe);
        SetGroundedSprite(monitorRenderer, monitorUnsafe);
        SetSprite(mailRenderer, mailUnsafe);
        SetGroundedSprite(doorRenderer, doorUnsafe);
    }

    private void SetChoiceMonitorVisible(bool visible)
    {
        choiceMonitorVisible = visible;

        if (mailRenderer != null)
            mailRenderer.enabled = !visible;

        if (choiceMonitorRenderer == null)
            return;

        if (visible)
        {
            choiceMonitorVisibility = 0f;
            choiceMonitorRenderer.enabled = true;
            choiceMonitorRenderer.transform.localScale = choiceMonitorBaseScale * 0.82f;
            Color color = choiceMonitorRenderer.color;
            color.a = 0f;
            choiceMonitorRenderer.color = color;
        }
    }

    private void AnimateChoiceMonitor()
    {
        if (choiceMonitorRenderer == null)
            return;

        float target = choiceMonitorVisible ? 1f : 0f;
        choiceMonitorVisibility = Mathf.MoveTowards(
            choiceMonitorVisibility,
            target,
            Time.deltaTime * (choiceMonitorVisible ? 5f : 7f));

        float eased = Mathf.SmoothStep(0f, 1f, choiceMonitorVisibility);
        choiceMonitorRenderer.transform.localScale = Vector3.Lerp(
            choiceMonitorBaseScale * 0.82f,
            choiceMonitorBaseScale,
            eased);

        Color color = choiceMonitorRenderer.color;
        color.a = eased;
        choiceMonitorRenderer.color = color;

        if (!choiceMonitorVisible && choiceMonitorVisibility <= 0f)
            choiceMonitorRenderer.enabled = false;
    }

    private static void SetSprite(SpriteRenderer target, Sprite sprite)
    {
        if (target != null && sprite != null)
            target.sprite = sprite;
    }

    private void SetGroundedSprite(SpriteRenderer target, Sprite sprite)
    {
        if (target == null || sprite == null)
            return;

        target.sprite = sprite;
        Vector3 position = target.transform.position;
        position.y = groundY + target.bounds.extents.y;
        target.transform.position = position;
    }
}
