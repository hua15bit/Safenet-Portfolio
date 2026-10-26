using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the Level 5 NPC sheets as stable 4x3 grids. The supplied PNGs were
/// auto-sliced irregularly, so creating full rectangular frames at runtime
/// prevents the NPC from changing size or position between frames.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area5NpcAnimator : MonoBehaviour
{
    private readonly List<Sprite> frames = new List<Sprite>();
    private SpriteRenderer targetRenderer;
    private Texture2D activeSheet;
    private float elapsed;
    private int frameIndex;

    public Sprite CurrentSprite =>
        targetRenderer != null ? targetRenderer.sprite : null;

    public void Configure(SpriteRenderer renderer, Texture2D initialSheet)
    {
        targetRenderer = renderer;
        SetSheet(initialSheet);
    }

    public void SetSheet(Texture2D sheet)
    {
        if (targetRenderer == null || sheet == null || activeSheet == sheet)
            return;

        ReleaseFrames();
        activeSheet = sheet;
        frameIndex = 0;
        elapsed = 0f;

        int cellWidth = Mathf.Max(1, sheet.width / 4);
        int cellHeight = Mathf.Max(1, sheet.height / 3);
        for (int rowFromTop = 0; rowFromTop < 3; rowFromTop++)
        {
            for (int column = 0; column < 4; column++)
            {
                Rect rect = new Rect(
                    column * cellWidth,
                    sheet.height - (rowFromTop + 1) * cellHeight,
                    cellWidth,
                    cellHeight);
                Sprite frame = Sprite.Create(
                    sheet,
                    rect,
                    new Vector2(0.5f, 0.04f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                frame.name = sheet.name + "_RuntimeFrame_" + frames.Count;
                frames.Add(frame);
            }
        }

        if (frames.Count > 0)
            targetRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (targetRenderer == null || frames.Count <= 1)
            return;

        elapsed += Time.deltaTime;
        if (elapsed < 0.14f)
            return;

        elapsed -= 0.14f;
        frameIndex = (frameIndex + 1) % frames.Count;
        targetRenderer.sprite = frames[frameIndex];
    }

    private void OnDestroy()
    {
        ReleaseFrames();
    }

    private void ReleaseFrames()
    {
        foreach (Sprite frame in frames)
        {
            if (frame != null)
                Destroy(frame);
        }
        frames.Clear();
    }
}
