using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies a scene-specific animated material and subtle parallax to gameplay
/// artwork whose object name identifies it as a background.
/// </summary>
public sealed class DynamicCyberBackground : MonoBehaviour
{
    private struct Preset
    {
        public Color primary;
        public Color secondary;
        public float speed;
        public float motion;
        public float light;
        public float corruption;
        public float theme;

        public Preset(
            Color primary,
            Color secondary,
            float speed,
            float motion,
            float light,
            float corruption,
            float theme)
        {
            this.primary = primary;
            this.secondary = secondary;
            this.speed = speed;
            this.motion = motion;
            this.light = light;
            this.corruption = corruption;
            this.theme = theme;
        }
    }

    private readonly List<Material> runtimeMaterials = new List<Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsGameplayScene(scene.name) || FindAnyObjectByType<DynamicCyberBackground>() != null)
            return;

        GameObject host = new GameObject("Animated Stage Background");
        host.AddComponent<DynamicCyberBackground>().Apply(scene.name);
    }

    private static bool IsGameplayScene(string sceneName) =>
        sceneName == "Tutorial" ||
        sceneName.StartsWith("Game(area", StringComparison.Ordinal);

    private void Apply(string sceneName)
    {
        Camera camera = Camera.main;
        Shader shader = Shader.Find("SafeNet/DynamicBackground");
        if (camera == null || shader == null)
        {
            Debug.LogWarning("SafeNet dynamic background shader or camera is unavailable.");
            return;
        }

        Preset preset = GetPreset(sceneName);
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (!renderer.gameObject.name.ToLowerInvariant().Contains("background")) continue;

            Material material = new Material(shader)
            {
                name = renderer.gameObject.name + " Animated Material"
            };
            material.SetColor("_Primary", preset.primary);
            material.SetColor("_Secondary", preset.secondary);
            material.SetFloat("_MotionSpeed", preset.speed);
            material.SetFloat("_MotionAmount", preset.motion);
            material.SetFloat("_LightAmount", preset.light);
            material.SetFloat("_Corruption", preset.corruption);
            material.SetFloat("_Theme", preset.theme);
            renderer.material = material;
            runtimeMaterials.Add(material);

            CyberParallaxLayer parallax = renderer.GetComponent<CyberParallaxLayer>();
            if (parallax == null) parallax = renderer.gameObject.AddComponent<CyberParallaxLayer>();
            parallax.Configure(camera, preset.speed);
        }
    }

    private static Preset GetPreset(string sceneName)
    {
        switch (sceneName)
        {
            case "Tutorial":
                return new Preset(Hex("#59E8FF"), Hex("#4D83FF"), .48f, .0015f, .08f, 0f, 0f);
            case "Game(area1)":
                return new Preset(Hex("#31F6C5"), Hex("#2C8CFF"), .72f, .0028f, .18f, .02f, 1f);
            case "Game(area2)":
                return new Preset(Hex("#4DEBFF"), Hex("#9C5CFF"), .78f, .003f, .19f, .04f, 2f);
            case "Game(area3)":
                return new Preset(Hex("#FFB84D"), Hex("#FF4F91"), .82f, .0026f, .17f, .06f, 3f);
            case "Game(area4)":
                return new Preset(Hex("#68F7FF"), Hex("#356DFF"), .74f, .0028f, .21f, .02f, 4f);
            case "Game(area5)":
                return new Preset(Hex("#FF416F"), Hex("#9B43FF"), .92f, .0032f, .16f, .6f, 5f);
            default:
                return new Preset(Hex("#59E8FF"), Hex("#4D83FF"), .55f, .002f, .1f, 0f, 0f);
        }
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
            if (material != null) Destroy(material);
        runtimeMaterials.Clear();
    }
}

/// <summary>
/// Keeps one background layer aligned with the camera while adding a small,
/// deterministic drift that gives otherwise static artwork depth.
/// </summary>
public sealed class CyberParallaxLayer : MonoBehaviour
{
    private Camera targetCamera;
    private Vector3 startPosition;
    private Vector3 cameraStart;
    private Vector3 startScale;
    private float speed;
    private float seed;

    public void Configure(Camera camera, float motionSpeed)
    {
        targetCamera = camera;
        startPosition = transform.position;
        cameraStart = camera.transform.position;
        startScale = transform.localScale;
        speed = motionSpeed;
        seed = Mathf.Abs(
            transform.position.x * .37f +
            transform.position.y * .19f +
            transform.GetSiblingIndex() * .61f);

        // Extra edge coverage prevents the shader's internal depth movement
        // and slight parallax from revealing the sprite boundary.
        transform.localScale = startScale * 1.018f;
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 delta = targetCamera.transform.position - cameraStart;
        float x = Mathf.Sin(Time.time * .11f * speed + seed) * .045f;
        float y = Mathf.Cos(Time.time * .08f * speed + seed) * .022f;
        transform.position = startPosition +
                             new Vector3(delta.x * .045f + x, delta.y * .015f + y, 0f);
    }

    private void OnDestroy()
    {
        transform.localScale = startScale;
    }
}
