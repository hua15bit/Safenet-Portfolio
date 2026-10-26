using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Uniformly fits the authored UI inside a phone's usable display. Expand keeps
/// the complete layout visible on both wide phones and squarer tablets; a safe
/// area container protects controls from notches and system navigation bars.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class SafeNetResponsiveCanvas : MonoBehaviour
{
    private CanvasScaler scaler;
    private Vector2 designResolution;
    private RectTransform content;
    private Rect lastSafeArea;
    private Vector2Int lastSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Start runs after scene bootstraps have built their runtime canvases.
        new GameObject("SafeNet Screen Layout").AddComponent<SafeNetCanvasInstaller>();
    }

    private void Awake() => Configure();

    public void Configure()
    {
        if (scaler != null) return;
        scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        designResolution = scaler.referenceResolution;
        if (designResolution.x < 800 || designResolution.y < 400)
            designResolution = new Vector2(1920, 1080);
        Fit();
    }

    private void LateUpdate()
    {
        if (lastSize.x != Screen.width || lastSize.y != Screen.height || lastSafeArea != Screen.safeArea)
            Fit();
    }

    /// <summary>Pure sizing calculation, also used by the mobile acceptance tests.</summary>
    public static Vector2 SafeReference(Vector2 design, Vector2 screen, Rect safe)
    {
        if (screen.x <= 0 || screen.y <= 0 || safe.width <= 0 || safe.height <= 0) return design;
        return new Vector2(design.x * screen.x / safe.width, design.y * screen.y / safe.height);
    }

    private void Fit()
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referenceResolution = SafeReference(designResolution, new Vector2(Screen.width, Screen.height), Screen.safeArea);
        lastSize = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;
        AdoptContent();
    }

    public void AdoptContent()
    {
        // A canvas that already owns a safe-area child has an intentional draw
        // order: full-bleed backgrounds first, then the inset interactive UI.
        // Moving only the background into a newly appended safe-area container
        // would place it above the controls and hide the entire screen.
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).GetComponent<MobileSafeArea>() != null)
                return;
        }

        // Capture the children before reparenting them. Iterating backwards and
        // appending each child used to reverse the Canvas draw order, allowing
        // full-screen backgrounds/dimmers to cover buttons and tutorial panels.
        List<RectTransform> childrenToAdopt = new List<RectTransform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            if (!(transform.GetChild(i) is RectTransform child) || child == content) continue;

            childrenToAdopt.Add(child);
        }

        if (childrenToAdopt.Count == 0)
            return;

        if (content == null)
        {
            content = new GameObject("Device Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(transform, false);
            content.gameObject.AddComponent<MobileSafeArea>();
        }

        // Reparent in original sibling order so Unity UI draw order is stable.
        foreach (RectTransform child in childrenToAdopt)
        {
            Vector2 position = child.anchoredPosition;
            Vector2 size = child.sizeDelta;
            child.SetParent(content, false);
            child.anchoredPosition = position;
            child.sizeDelta = size;
        }
    }
}

/// <summary>Includes late-created challenge/completion canvases without per-frame scene scans.</summary>
public sealed class SafeNetCanvasInstaller : MonoBehaviour
{
    private void Start() => InvokeRepeating(nameof(Refresh), 0f, .5f);

    private void Refresh()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            SafeNetResponsiveCanvas fit = canvas.GetComponent<SafeNetResponsiveCanvas>();
            if (fit == null) fit = canvas.gameObject.AddComponent<SafeNetResponsiveCanvas>();
            // Disabled completion panels do not receive Awake until revealed.
            // Prepare them now so their first visible frame is already fitted.
            fit.Configure();
            fit.AdoptContent();
        }
    }
}
