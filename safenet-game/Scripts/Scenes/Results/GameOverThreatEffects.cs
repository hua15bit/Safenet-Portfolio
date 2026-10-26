using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animates individual breach signals, corrupted fragments, and warning text on
/// the Game Over screen while leaving the main artwork crisp and readable.
/// </summary>
public sealed class GameOverThreatEffects : MonoBehaviour
{
    [SerializeField] private RectTransform breachGlow;
    [SerializeField] private RectTransform warningIcon;
    [SerializeField] private TMP_Text breachTitle;
    [SerializeField] private TMP_Text activityText;
    [SerializeField] private Graphic[] threatSignals;
    [SerializeField] private RectTransform[] corruptedFragments;

    private readonly string[] activityMessages =
    {
        "THREAT ACTIVITY: MALICIOUS PROCESS REPLICATING",
        "THREAT ACTIVITY: UNAUTHORIZED MEMORY ACCESS",
        "THREAT ACTIVITY: SAFENET DEFENSES OVERRIDDEN",
        "RECOVERY STATUS: MANUAL RESTART REQUIRED"
    };

    private readonly List<Color> signalColors = new List<Color>();
    private readonly List<Vector2> fragmentPositions = new List<Vector2>();
    private Vector2 titlePosition;
    private float nextGlitch;
    private float glitchEnds;
    private float nextMessage;
    private int messageIndex;

    public void Configure(RectTransform glow, RectTransform icon, TMP_Text title, TMP_Text activity,
        Graphic[] signals, RectTransform[] fragments)
    {
        breachGlow = glow;
        warningIcon = icon;
        breachTitle = title;
        activityText = activity;
        threatSignals = signals;
        corruptedFragments = fragments;
        CacheState();
    }

    private void Awake() => CacheState();

    private void CacheState()
    {
        if (breachTitle != null) titlePosition = breachTitle.rectTransform.anchoredPosition;

        signalColors.Clear();
        if (threatSignals != null)
            foreach (Graphic signal in threatSignals)
                signalColors.Add(signal != null ? signal.color : Color.clear);

        fragmentPositions.Clear();
        if (corruptedFragments != null)
            foreach (RectTransform fragment in corruptedFragments)
                fragmentPositions.Add(fragment != null ? fragment.anchoredPosition : Vector2.zero);

        nextGlitch = Time.unscaledTime + 0.8f;
        nextMessage = Time.unscaledTime + 1.8f;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 3.2f);

        if (breachGlow != null)
        {
            breachGlow.localScale = Vector3.one * Mathf.Lerp(0.97f, 1.035f, pulse);
            if (breachGlow.TryGetComponent(out Graphic glowGraphic))
            {
                Color color = glowGraphic.color;
                color.a = Mathf.Lerp(0.12f, 0.29f, pulse);
                glowGraphic.color = color;
            }
        }

        if (warningIcon != null)
        {
            float iconPulse = 1f + Mathf.Sin(now * 4.6f) * 0.045f;
            warningIcon.localScale = new Vector3(iconPulse, iconPulse, 1f);
        }

        for (int i = 0; threatSignals != null && i < threatSignals.Length; i++)
        {
            Graphic signal = threatSignals[i];
            if (signal == null || i >= signalColors.Count) continue;
            Color color = signalColors[i];
            float wave = 0.5f + 0.5f * Mathf.Sin(now * 4.1f - i * 0.72f);
            color.a *= Mathf.Lerp(0.28f, 1f, wave);
            signal.color = color;
        }

        for (int i = 0; corruptedFragments != null && i < corruptedFragments.Length; i++)
        {
            RectTransform fragment = corruptedFragments[i];
            if (fragment == null || i >= fragmentPositions.Count) continue;
            Vector2 origin = fragmentPositions[i];
            float direction = (i & 1) == 0 ? 1f : -1f;
            fragment.anchoredPosition = origin + new Vector2(
                Mathf.Sin(now * (0.8f + i * 0.07f) + i) * (7f + i % 3 * 3f) * direction,
                Mathf.Cos(now * (0.65f + i * 0.05f) + i * 0.4f) * 3f);
        }

        AnimateTitleGlitch(now);
        CycleThreatMessage(now);
    }

    private void AnimateTitleGlitch(float now)
    {
        if (breachTitle == null) return;
        if (now >= nextGlitch)
        {
            glitchEnds = now + Random.Range(0.08f, 0.16f);
            nextGlitch = now + Random.Range(1.5f, 3.2f);
        }

        if (now < glitchEnds)
        {
            breachTitle.rectTransform.anchoredPosition = titlePosition +
                new Vector2(Random.Range(-7f, 7f), Random.Range(-2f, 2f));
            breachTitle.color = Random.value > 0.45f
                ? new Color(1f, 0.16f, 0.2f)
                : new Color(1f, 0.68f, 0.12f);
        }
        else
        {
            breachTitle.rectTransform.anchoredPosition = titlePosition;
            breachTitle.color = new Color(1f, 0.2f, 0.24f);
        }
    }

    private void CycleThreatMessage(float now)
    {
        if (activityText == null || now < nextMessage) return;
        messageIndex = (messageIndex + 1) % activityMessages.Length;
        activityText.text = activityMessages[messageIndex];
        nextMessage = now + 2.2f;
    }
}
