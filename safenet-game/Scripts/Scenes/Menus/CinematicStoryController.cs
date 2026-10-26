using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Prepares and plays the introductory story video, provides a skip control,
/// and continues to the configured scene when playback finishes.
/// </summary>
public class CinematicStoryController : MonoBehaviour
{
    [SerializeField] private VideoClip storyVideo;
    [SerializeField] private string nextScene = "Tutorial";
    private VideoPlayer player;
    private AudioSource audioSource;
    private bool leaving;
    private bool prepared;

    private void Awake()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Cinematic Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.waitForFirstFrame = true;
        player.skipOnDrop = false;
        player.renderMode = VideoRenderMode.CameraNearPlane;
        player.targetCamera = camera;
        player.aspectRatio = VideoAspectRatio.FitInside;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 1f;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, audioSource);
        player.isLooping = false;
        player.clip = storyVideo != null ? storyVideo : Resources.Load<VideoClip>("Video/SafeNetStory");
        player.prepareCompleted += OnPrepared;
        player.loopPointReached += _ => ContinueToTutorial();
        player.errorReceived += (_, message) =>
        {
            Debug.LogWarning("SafeNet cinematic could not play: " + message);
            ContinueToTutorial();
        };

        CreateOverlay();
    }

    private void Start()
    {
        if (player.clip == null)
        {
            Debug.LogWarning("SafeNet story video is missing. Continuing to Tutorial.");
            ContinueToTutorial();
            return;
        }
        player.Prepare();
    }

    private void Update()
    {
        if (SafeNetDesktopInput.SkipPressed)
            ContinueToTutorial();
    }

    public void ContinueToTutorial()
    {
        if (leaving) return;
        leaving = true;
        if (player != null) player.Stop();
        SceneManager.LoadScene(nextScene);
    }

    private void OnPrepared(VideoPlayer source)
    {
        if (leaving || prepared) return;
        prepared = true;
        source.Play();
    }

    private void CreateOverlay()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        GameObject canvasObject = new GameObject("Cinematic UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        GameObject skipObject = new GameObject("Skip", typeof(RectTransform), typeof(Image), typeof(Button));
        skipObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = skipObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-34, -34);
        rect.sizeDelta = new Vector2(210, 72);

        Image background = skipObject.GetComponent<Image>();
        background.color = new Color(.02f, .08f, .15f, .82f);
        Button button = skipObject.GetComponent<Button>();
        button.onClick.AddListener(ContinueToTutorial);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(skipObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "SKIP  ▶";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(.48f, .95f, 1f);
    }
}
