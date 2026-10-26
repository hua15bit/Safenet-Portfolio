using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One boundary policy for the tutorial and all missions. Camera limits follow
/// the finished artwork; solid side walls and a body clamp keep Aira inside it.
/// Area 6 retains its longer street and existing checkpoint blockers.
/// </summary>
[DefaultExecutionOrder(9000)]
public sealed class SafeNetLevelBounds : MonoBehaviour
{
    public Rect CameraBounds { get; private set; }
    public float LeftEdge { get; private set; }
    public float RightEdge { get; private set; }
    public bool IsReady { get; private set; }
    public float ReferenceCameraSize { get; private set; }
    public Collider2D PlayerCollider { get; private set; }
    private SpriteRenderer playerSprite;
    public Bounds PlayerBounds
    {
        get
        {
            Bounds bounds = PlayerCollider.bounds;
            if (playerSprite != null) bounds.Encapsulate(playerSprite.bounds);
            return bounds;
        }
    }
    private Rigidbody2D body;
    private Camera outputCamera;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Tutorial" && !scene.name.StartsWith("Game(area", StringComparison.Ordinal)) return;
        new GameObject("SafeNet Level Borders").AddComponent<SafeNetLevelBounds>();
    }

    private void Start()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        outputCamera = Camera.main;
        FitCameraToSafeArea();
        Collider2D confiner = GameObject.Find("Confiner")?.GetComponent<Collider2D>();
        if (player == null || outputCamera == null || confiner == null)
        {
            Debug.LogError("SafeNet level borders need a player, camera, and Confiner.", this);
            return;
        }
        body = player.rb;
        PlayerCollider = player.GetComponent<Collider2D>();
        playerSprite = player.GetComponent<SpriteRenderer>();
        ReferenceCameraSize = outputCamera.orthographicSize;

        Bounds art = default, backdrop = default, platform = default;
        bool hasArt = false, hasBackdrop = false, hasPlatform = false;
        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>())
        {
            if (!sprite.enabled || sprite.bounds.size.x < 15) continue;
            string label = sprite.name.ToLowerInvariant();
            bool back = label.Contains("background") || label.Contains("wall panel") || label.Contains("corrupted street");
            bool floor = label.Contains("platform") || label.Contains("floor panel");
            if (!back && !floor) continue;

            // Keep the shader's living texture, but do not drift the actual
            // sprite beyond the physical world rectangle on smaller screens.
            if (sprite.TryGetComponent(out CyberParallaxLayer parallax)) parallax.enabled = false;
            Include(ref art, ref hasArt, sprite.bounds);
            if (back) Include(ref backdrop, ref hasBackdrop, sprite.bounds);
            if (floor) Include(ref platform, ref hasPlatform, sprite.bounds);
        }

        Bounds authored = confiner.bounds;
        float left = authored.min.x, right = authored.max.x;
        float bottom = authored.min.y, top = authored.max.y;
        if (hasArt)
        {
            bottom = Mathf.Max(bottom, art.min.y);
            top = Mathf.Min(top, art.max.y);
        }
        if (hasBackdrop) { left = Mathf.Max(left, backdrop.min.x); right = Mathf.Min(right, backdrop.max.x); }
        if (hasPlatform) { left = Mathf.Max(left, platform.min.x); right = Mathf.Min(right, platform.max.x); }
        CameraBounds = Rect.MinMaxRect(left + .08f, bottom + .08f, right - .08f, top - .08f);

        Collider2D existingLeft = GameObject.Find("Area 6 Left Boundary")?.GetComponent<Collider2D>();
        Collider2D existingRight = GameObject.Find("Area 6 Right Boundary")?.GetComponent<Collider2D>();
        // A little visual clearance also accommodates wider walking/idle frames.
        LeftEdge = existingLeft != null ? existingLeft.bounds.max.x : CameraBounds.xMin + .6f;
        RightEdge = existingRight != null ? existingRight.bounds.min.x : CameraBounds.xMax - .6f;
        if (existingLeft == null) CreateWall("Left Level Border", LeftEdge - .5f);
        if (existingRight == null) CreateWall("Right Level Border", RightEdge + .5f);
        if (body != null) body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        foreach (CinemachineCamera camera in FindObjectsByType<CinemachineCamera>())
        {
            SafeNetCameraBounds framing = camera.GetComponent<SafeNetCameraBounds>();
            if (framing == null) framing = camera.gameObject.AddComponent<SafeNetCameraBounds>();
            framing.Configure(this);
        }
        IsReady = true;
        ClampPlayer();
    }

    private static void Include(ref Bounds combined, ref bool initialized, Bounds next)
    {
        if (initialized) combined.Encapsulate(next);
        else { combined = next; initialized = true; }
    }

    private void CreateWall(string label, float x)
    {
        GameObject wall = new GameObject(label);
        wall.transform.SetParent(transform, false);
        wall.transform.position = new Vector3(x, CameraBounds.center.y, 0);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1, CameraBounds.height + 20);
    }

    private void FixedUpdate() => ClampPlayer();
    private void LateUpdate()
    {
        FitCameraToSafeArea();
        ClampPlayer();
    }

    private void FitCameraToSafeArea()
    {
        if (outputCamera == null || Screen.width <= 0 || Screen.height <= 0) return;
        if (lastSafeArea == Screen.safeArea && lastScreenSize == new Vector2Int(Screen.width, Screen.height)) return;
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        // Aira and the world use the same unobstructed viewport as the controls.
        outputCamera.rect = new Rect(safe.x / Screen.width, safe.y / Screen.height,
            safe.width / Screen.width, safe.height / Screen.height);
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }

    /// <summary>Fallback for a teleport/fast movement as well as ordinary physics.</summary>
    public void ClampPlayer()
    {
        if (!IsReady || body == null || PlayerCollider == null) return;
        Bounds bounds = PlayerCollider.bounds;
        Vector2 offset = (Vector2)bounds.center - body.position;
        float x = Mathf.Clamp(bounds.center.x, LeftEdge + bounds.extents.x, RightEdge - bounds.extents.x);
        float y = Mathf.Clamp(bounds.center.y, CameraBounds.yMin + bounds.extents.y, CameraBounds.yMax - bounds.extents.y);
        Vector2 corrected = new Vector2(x, y) - offset;
        if ((corrected - body.position).sqrMagnitude < .000001f) return;
        Vector2 velocity = body.linearVelocity;
        if (Mathf.Abs(corrected.x - body.position.x) > .001f) velocity.x = 0;
        if (Mathf.Abs(corrected.y - body.position.y) > .001f) velocity.y = 0;
        body.position = corrected;
        // A late-frame safety correction must also move the rendered sprite
        // immediately, not leave it outside the border until the physics tick.
        body.transform.position = new Vector3(corrected.x, corrected.y, body.transform.position.z);
        body.linearVelocity = velocity;
    }

    public float CameraSizeForAspect(float aspect) => Mathf.Min(ReferenceCameraSize,
        CameraBounds.height * .5f, CameraBounds.width * .5f / Mathf.Max(.1f, aspect));

    /// <summary>Limits the entire viewport, not only the camera's center point.</summary>
    public Vector3 ConfineCamera(Vector3 position, float size, float aspect, Bounds actor)
    {
        float halfWidth = size * aspect;
        // Keep the complete character visible even with the original camera's
        // strong forward offset or during the Area 5 vault.
        position.x = Mathf.Clamp(position.x, actor.max.x - halfWidth + .12f, actor.min.x + halfWidth - .12f);
        position.y = Mathf.Clamp(position.y, actor.max.y - size + .12f, actor.min.y + size - .12f);
        position.x = Mathf.Clamp(position.x, CameraBounds.xMin + halfWidth, CameraBounds.xMax - halfWidth);
        position.y = Mathf.Clamp(position.y, CameraBounds.yMin + size, CameraBounds.yMax - size);
        return position;
    }
}
