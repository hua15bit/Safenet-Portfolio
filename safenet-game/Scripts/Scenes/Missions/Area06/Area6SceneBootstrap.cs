using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds Mission 6 (The False Web) from the supplied Level 6 artwork. The
/// mission is deliberately linear: every security challenge keeps a physical
/// blocker active until the current answer is correct.
/// </summary>
[DefaultExecutionOrder(-55)]
[DisallowMultipleComponent]
public sealed class Area6SceneBootstrap : MonoBehaviour
{
    private const int ChallengeScore = 5;
    private const float MissionOrthographicSize = 7.86f;
    private const float BackgroundHeightOverscan = 1.02f;
    private const float CameraBottom = -8.559017f;
    private const float CameraTop = 7.440983f;
    private const int MalwareDisappearFrameCount = 8;
    private const float MalwareDisappearFrameSeconds = 0.12f;
    public const string InteractablesRootName = "[AREA 6 INTERACTABLES - EXPAND IN PLAY MODE]";

    // These positions form the complete mission path: URL, malware, source,
    // download, and final-door verification challenges.
    private static readonly float[] CheckpointOffsets = { 12f, 35f, 58f, 81f, 104f };
    private static readonly string[] CheckpointHierarchyNames =
    {
        "01 URL Verification [INTERACTABLE]",
        "02 Malware Pop-up [INTERACTABLE]",
        "03 Source Reliability [INTERACTABLE]",
        "04 Download Trap [INTERACTABLE]",
        "05 Final Door Check [INTERACTABLE]"
    };

    [Header("Street Backgrounds")]
    [SerializeField] private Sprite corruptedBackgroundSprite;
    [SerializeField] private Sprite normalBackgroundSprite;
    [SerializeField] private SpriteRenderer backgroundTemplate;

    [Header("World Checkpoint Artwork")]
    [SerializeField] private Texture2D[] worldArtwork = new Texture2D[5];

    [Header("Challenge Artwork")]
    [SerializeField] private Texture2D[] promptArtwork = new Texture2D[5];
    [SerializeField] private Texture2D[] correctArtwork = new Texture2D[5];
    [SerializeField] private Texture2D[] incorrectArtwork = new Texture2D[5];
    [SerializeField] private Texture2D[] alternateIncorrectArtwork = new Texture2D[5];

    private readonly List<SpriteRenderer> backgroundSegments = new List<SpriteRenderer>();
    private readonly List<Area6Checkpoint> checkpoints = new List<Area6Checkpoint>();

    private PlayerMovement player;
    private Rigidbody2D playerBody;
    private DialogueController dialogue;
    private int currentCheckpoint;
    private Area6Checkpoint openCheckpoint;
    private bool missionComplete;
    private bool checkpointVoiceActive;
    private Coroutine challengeArtworkAnimation;

    private GameObject challengeCanvas;
    private RawImage challengeArtwork;
    private TMP_Text challengeTitle;
    private TMP_Text challengeFeedback;
    private readonly List<Button> choiceButtons = new List<Button>();
    private Button actionButton;
    private TMP_Text actionButtonLabel;

    private static readonly string[] Titles =
    {
        "URL CHECK: Which login page should I use?",
        "MALWARE POP-UP: YOUR DEVICE IS INFECTED! DOWNLOAD NOW!",
        "SOURCE CHECK: I found different answers online. Which one should I trust?",
        "DOWNLOAD TRAP: Which button is legitimate?",
        "FINAL SECURITY CHECK: What should you verify before entering information?"
    };

    private static readonly string[][] Options =
    {
        new[] { "A. studentportal.edu.ph", "B. studentporta1.edu.ph" },
        new[] { "A. Close the pop-up", "B. Download the unknown program" },
        new[]
        {
            "A. Official academic source with author, date, and evidence",
            "B. Anonymous blog with no references",
            "C. Random social-media post"
        },
        new[] { "A. Verified DOWNLOAD button", "B. Flashing DOWNLOAD NOW advertisement" },
        new[] { "A. Website address", "B. Website colors" }
    };

    private static readonly string[] CorrectFeedback =
    {
        "Correct. Checking the complete website address helps you avoid fake pages designed to look legitimate.",
        "Correct. Close unexpected infection warnings and verify the device through trusted security tools.",
        "Correct. Reliable information has a clear author, date, evidence, and trustworthy website.",
        "Correct. Use the download button that belongs to the website or file you actually intended to use.",
        "Correct. A website address can reveal suspicious or fake websites. The false web is disconnected."
    };

    private static readonly string[] IncorrectFeedback =
    {
        "Unsafe choice. A familiar-looking page can still be fake. Inspect the full address and try again.",
        "Unsafe choice. Unexpected warnings should be verified before downloading anything. Try again.",
        "That source may not be reliable. Check its author, date, evidence, and website before trusting it.",
        "Unsafe choice. Some advertisements imitate real download buttons. Compare the source and try again.",
        "Unsafe choice. A website's appearance does not prove that it is trustworthy. Try again."
    };

    private static readonly string[] PromptVoiceKeys =
    {
        "level6_login_question",
        "level6_infected_popup",
        "level6_source_question",
        "level6_download_prompt",
        "level6_final_prompt"
    };

    private static readonly string[] CorrectVoiceKeys =
    {
        "level6_url_correct",
        "level6_popup_correct",
        "level6_source_correct",
        "level6_download_correct",
        "level6_final_correct"
    };

    private static readonly string[] IncorrectVoiceKeys =
    {
        "level6_url_unsafe",
        "level6_popup_unsafe",
        "level6_source_unsafe",
        "level6_download_unsafe",
        "level6_final_unsafe"
    };

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "Game(area6)")
        {
            enabled = false;
            return;
        }

        VoiceLinePlayer.Preload(
            "level6_login_question",
            "level6_infected_popup",
            "level6_source_question",
            "level6_intro",
            "level6_download_prompt",
            "level6_final_prompt",
            "level6_url_correct",
            "level6_popup_correct",
            "level6_source_correct",
            "level6_download_correct",
            "level6_final_correct",
            "level6_url_unsafe",
            "level6_popup_unsafe",
            "level6_source_unsafe",
            "level6_download_unsafe",
            "level6_final_unsafe",
            "level6_complete");

        GameObject playerObject = GameObject.FindGameObjectWithTag("MC");
        player = playerObject != null ? playerObject.GetComponent<PlayerMovement>() : null;
        playerBody = playerObject != null ? playerObject.GetComponent<Rigidbody2D>() : null;
        dialogue = FindSceneComponent<DialogueController>();

        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        RemoveArea5Objects();
        ConfigureDialogue();
        ConfigureMissionCamera();
        BuildStreet();
        BuildWorldBoundaryWalls();
        BuildCheckpoints();
        BuildChallengeInterface();
    }

    private void Start()
    {
        StartCoroutine(ShowIntroduction());
    }

    private bool ValidateReferences()
    {
        bool valid = player != null && playerBody != null && dialogue != null &&
                     corruptedBackgroundSprite != null && normalBackgroundSprite != null &&
                     backgroundTemplate != null && HasFive(worldArtwork) &&
                     HasFive(promptArtwork) && HasFive(correctArtwork) &&
                     HasFive(incorrectArtwork);
        if (!valid)
            Debug.LogError(
                "Area 6 setup is incomplete. Re-run SafeNet/Rebuild Area 6 False Web Mission.",
                this);
        return valid;
    }

    private static bool HasFive(UnityEngine.Object[] items)
    {
        if (items == null || items.Length != 5)
            return false;
        for (int index = 0; index < items.Length; index++)
            if (items[index] == null) return false;
        return true;
    }

    private void RemoveArea5Objects()
    {
        HideObject("Area 5 Privacy Obstacle");
        HideObject("Privacy Guide NPC");
        HideObject("Door");

        Area5SceneBootstrap oldBootstrap = GetComponent<Area5SceneBootstrap>();
        if (oldBootstrap != null)
            oldBootstrap.enabled = false;
    }

    private static void HideObject(string objectName)
    {
        GameObject item = GameObject.Find(objectName);
        if (item != null)
            item.SetActive(false);
    }

    private void ConfigureDialogue()
    {
        dialogue.useImmersiveChoiceLayout = false;
        dialogue.useSpriteBackedChoiceLayout = false;
        dialogue.useLightStoryChoiceButtons = true;
        dialogue.ConfigureDefaultDialogueSurface();
        dialogue.ShowDialogueUI(false);
    }

    private void BuildStreet()
    {
        backgroundTemplate.enabled = true;
        backgroundTemplate.sprite = corruptedBackgroundSprite;
        backgroundTemplate.sortingOrder = -20;
        backgroundTemplate.color = Color.white;

        float targetHeight = MissionOrthographicSize * 2f * BackgroundHeightOverscan;
        float scale = targetHeight /
                      Mathf.Max(0.01f, corruptedBackgroundSprite.bounds.size.y);
        // Match the vertical center of the proven camera container used by
        // Areas 1-5. The image is uniformly scaled, so it is cropped rather
        // than stretched on wider phones.
        float centerY = (CameraBottom + CameraTop) * 0.5f;
        float segmentWidth = corruptedBackgroundSprite.bounds.size.x * scale;
        float cameraAspect = Camera.main != null && Camera.main.aspect > 0f
            ? Camera.main.aspect
            : 16f / 9f;
        float halfViewWidth = MissionOrthographicSize * cameraAspect;
        float firstLeftX = player.transform.position.x - halfViewWidth - 2f;
        float firstCenterX = firstLeftX + segmentWidth * 0.5f;
        float lastCheckpointX = player.transform.position.x +
                                CheckpointOffsets[CheckpointOffsets.Length - 1];
        float requiredRightEdge = lastCheckpointX + halfViewWidth + 12f;
        int segmentCount = Mathf.Max(
            1,
            Mathf.CeilToInt((requiredRightEdge - firstLeftX) / segmentWidth));

        Transform oldParent = backgroundTemplate.transform.parent;
        backgroundTemplate.transform.SetParent(null, true);
        backgroundTemplate.transform.localScale = new Vector3(scale, scale, 1f);
        backgroundTemplate.transform.position = new Vector3(firstCenterX, centerY, 2f);
        backgroundTemplate.gameObject.name = "Area 6 Corrupted Street 1";
        backgroundSegments.Add(backgroundTemplate);

        for (int index = 1; index < segmentCount; index++)
        {
            SpriteRenderer segment = Instantiate(backgroundTemplate);
            segment.gameObject.name = "Area 6 Corrupted Street " + (index + 1);
            segment.transform.position = new Vector3(
                firstCenterX + segmentWidth * index,
                centerY,
                2f);
            backgroundSegments.Add(segment);
        }

        if (oldParent != null && oldParent.childCount == 0)
            oldParent.gameObject.SetActive(false);
    }

    private void BuildCheckpoints()
    {
        Transform interactablesRoot = EnsureInteractableHierarchy();
        checkpoints.Clear();
        float startX = player.transform.position.x;
        float playerY = player.transform.position.y;
        float[] artworkHeights = { 6.3f, 6.4f, 6.5f, 5.4f, 7.2f };

        for (int index = 0; index < CheckpointOffsets.Length; index++)
        {
            float checkpointX = startX + CheckpointOffsets[index];
            Transform existing = interactablesRoot.Find(CheckpointHierarchyNames[index]);
            GameObject root = existing != null
                ? existing.gameObject
                : new GameObject(CheckpointHierarchyNames[index]);
            root.transform.SetParent(interactablesRoot, false);
            root.transform.localPosition = Vector3.zero;

            GameObject art = CreateWorldArtwork(
                root.transform,
                "Obstacle Artwork",
                worldArtwork[index],
                new Vector3(checkpointX + 1f, playerY + 1.1f, 0f),
                artworkHeights[index]);

            GameObject blocker = new GameObject("Progression Blocker (Solid Collider)");
            blocker.transform.SetParent(root.transform, false);
            // Position the wall at the visible left edge of the supplied art.
            // This makes Aira bump before her sprite can render behind it.
            RectTransform artworkRect = art.GetComponent<RectTransform>();
            float artworkWidth = artworkRect != null ? artworkRect.sizeDelta.x : 2f;
            float blockerX = art.transform.position.x - artworkWidth * 0.5f - 0.35f;
            blocker.transform.position = new Vector3(blockerX, playerY, 0f);
            BoxCollider2D solid = blocker.AddComponent<BoxCollider2D>();
            solid.isTrigger = false;
            solid.size = new Vector2(1.2f, 30f);

            GameObject triggerObject = new GameObject("Interaction Trigger (Press Interact)");
            triggerObject.transform.SetParent(root.transform, false);
            triggerObject.transform.position = new Vector3(blockerX - 1.5f, playerY, 0f);
            BoxCollider2D trigger = triggerObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(4.5f, 5.5f);

            Area6Checkpoint checkpoint = triggerObject.AddComponent<Area6Checkpoint>();
            checkpoint.Configure(this, index, blocker, art, trigger);
            checkpoints.Add(checkpoint);
        }
    }

    /// <summary>
    /// Creates stable, named checkpoint anchors in the saved scene. Their
    /// artwork, solid blocker, and interaction trigger appear beneath these
    /// anchors during Play Mode, making Area 6 easy to inspect in Hierarchy.
    /// </summary>
    public Transform EnsureInteractableHierarchy()
    {
        Transform root = transform.Find(InteractablesRootName);
        if (root == null)
        {
            GameObject rootObject = new GameObject(InteractablesRootName);
            rootObject.transform.SetParent(transform, false);
            root = rootObject.transform;
        }

        foreach (string checkpointName in CheckpointHierarchyNames)
        {
            if (root.Find(checkpointName) != null) continue;
            GameObject checkpoint = new GameObject(checkpointName);
            checkpoint.transform.SetParent(root, false);
        }

        return root;
    }

    private void ConfigureMissionCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = MissionOrthographicSize;
        }

        CinemachineCamera virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        if (virtualCamera != null)
        {
            LensSettings lens = virtualCamera.Lens;
            lens.OrthographicSize = MissionOrthographicSize;
            virtualCamera.Lens = lens;
        }

        CinemachinePositionComposer composer =
            FindAnyObjectByType<CinemachinePositionComposer>();
        if (composer != null)
        {
            // Copy the framing already proven in Areas 1-5.
            ScreenComposerSettings composition = composer.Composition;
            composition.ScreenPosition = new Vector2(-0.36f, 0.18f);
            composer.Composition = composition;
        }

        if (playerBody != null)
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        ExpandCameraBoundary(mainCamera);
    }

    private void ExpandCameraBoundary(Camera mainCamera)
    {
        GameObject boundaryObject = GameObject.Find("Confiner");
        if (boundaryObject == null)
        {
            boundaryObject = new GameObject("Confiner");
            SceneManager.MoveGameObjectToScene(
                boundaryObject,
                SceneManager.GetActiveScene());
        }

        PolygonCollider2D boundary = boundaryObject.GetComponent<PolygonCollider2D>();
        if (boundary == null)
            boundary = boundaryObject.AddComponent<PolygonCollider2D>();
        boundary.isTrigger = true;

        float aspect = mainCamera != null && mainCamera.aspect > 0f
            ? mainCamera.aspect
            : 16f / 9f;
        float halfWidth = MissionOrthographicSize * aspect;
        float startX = player.transform.position.x;
        float endX = startX + CheckpointOffsets[CheckpointOffsets.Length - 1] + 12f;
        float left = -13.9f;
        float right = endX + halfWidth + 3f;
        float bottom = CameraBottom;
        float top = CameraTop;

        Vector2[] worldPoints =
        {
            new Vector2(left, bottom),
            new Vector2(left, top),
            new Vector2(right, top),
            new Vector2(right, bottom)
        };
        Vector2[] localPoints = new Vector2[worldPoints.Length];
        for (int index = 0; index < worldPoints.Length; index++)
            localPoints[index] = boundary.transform.InverseTransformPoint(worldPoints[index]);

        boundary.pathCount = 1;
        boundary.SetPath(0, localPoints);

        CinemachineConfiner2D confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        if (confiner != null)
        {
            confiner.BoundingShape2D = boundary;
            confiner.InvalidateBoundingShapeCache();
        }
    }

    private void BuildWorldBoundaryWalls()
    {
        float startX = player.transform.position.x;
        float playerY = player.transform.position.y;
        float finalX = startX + CheckpointOffsets[CheckpointOffsets.Length - 1];
        CreateBoundaryWall("Area 6 Left Boundary", startX - 3f, playerY);
        CreateBoundaryWall("Area 6 Right Boundary", finalX + 8f, playerY);
    }

    private void CreateBoundaryWall(string objectName, float worldX, float worldY)
    {
        GameObject wall = new GameObject(objectName);
        wall.transform.SetParent(transform, false);
        wall.transform.position = new Vector3(worldX, worldY, 0f);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;
        collider.size = new Vector2(1f, 30f);
    }

    private static GameObject CreateWorldArtwork(
        Transform parent,
        string objectName,
        Texture2D texture,
        Vector3 worldPosition,
        float worldHeight)
    {
        GameObject canvasObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Canvas));
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.position = worldPosition;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5;

        float aspect = texture != null && texture.height > 0
            ? (float)texture.width / texture.height
            : 1f;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(worldHeight * aspect, worldHeight);
        canvasRect.localScale = Vector3.one;

        GameObject imageObject = new GameObject(
            "Artwork",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        return canvasObject;
    }

    private IEnumerator ShowIntroduction()
    {
        yield return null;
        dialogue.ClearChoices();
        dialogue.SetNPCInfo("Aegis", null);
        dialogue.setDialogueText(
            "This sector is filled with deceptive websites and misleading information. " +
            "Be careful what you trust. Move right and inspect each warning carefully.");
        dialogue.ShowDialogueUI(true);
        dialogue.PrepareChoiceButtons(1);
        dialogue.CreateChoiceButton("CONTINUE", CloseIntroduction);
        PlayCheckpointVoice("level6_intro");
    }

    private void CloseIntroduction()
    {
        StopCheckpointVoice();
        dialogue.ShowDialogueUI(false);
    }

    public bool IsCurrentCheckpoint(int checkpointIndex)
    {
        return !missionComplete && openCheckpoint == null &&
               checkpointIndex == currentCheckpoint;
    }

    public void OpenCheckpoint(Area6Checkpoint checkpoint)
    {
        if (checkpoint == null || !IsCurrentCheckpoint(checkpoint.CheckpointIndex))
            return;

        openCheckpoint = checkpoint;
        SetPlayerControl(false);
        ShowPrompt(checkpoint.CheckpointIndex);
    }

    private void ShowPrompt(int index)
    {
        StopChallengeArtworkAnimation();
        StopCheckpointVoice();
        challengeCanvas.SetActive(true);
        challengeTitle.text = Titles[index];
        challengeFeedback.text = "Select the safest response.";
        challengeFeedback.color = new Color(0.08f, 0.17f, 0.25f, 1f);
        SetChallengeArtwork(promptArtwork[index]);

        for (int buttonIndex = 0; buttonIndex < choiceButtons.Count; buttonIndex++)
        {
            Button button = choiceButtons[buttonIndex];
            bool show = buttonIndex < Options[index].Length;
            button.gameObject.SetActive(show);
            if (!show) continue;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = Options[index][buttonIndex];
            button.onClick.RemoveAllListeners();
            int selected = buttonIndex;
            button.onClick.AddListener(() => ResolveChoice(selected));
            SafeNetSfxPlayer.HookButton(button);
        }

        actionButton.gameObject.SetActive(false);

        string voiceKey = index >= 0 && index < PromptVoiceKeys.Length
            ? PromptVoiceKeys[index]
            : null;
        if (!string.IsNullOrEmpty(voiceKey))
        {
            PlayCheckpointVoice(voiceKey);
        }
    }

    private void ResolveChoice(int selectedIndex)
    {
        if (openCheckpoint == null)
            return;

        StopChallengeArtworkAnimation();
        StopCheckpointVoice();
        int index = openCheckpoint.CheckpointIndex;
        bool correct = selectedIndex == 0;
        SetChoiceButtonsVisible(false);

        if (correct)
        {
            GameManager.Instance?.AddScore(ChallengeScore);
            SetChallengeArtwork(correctArtwork[index]);
            challengeFeedback.text = CorrectFeedback[index];
            challengeFeedback.color = new Color(0.02f, 0.47f, 0.2f, 1f);
            PlayCheckpointVoice(CorrectVoiceKeys[index]);
            string actionLabel = index == checkpoints.Count - 1
                ? "RESTORE THE WEB"
                : "CONTINUE";
            if (index == 1)
            {
                // This source image is an eight-frame, 4-by-2 disappearance
                // sheet. Show one frame at a time instead of the whole sheet.
                actionButton.gameObject.SetActive(false);
                challengeArtworkAnimation = StartCoroutine(
                    PlayMalwareDisappearance(actionLabel));
            }
            else
            {
                ConfigureActionButton(actionLabel, FinishCorrectAnswer);
            }
            return;
        }

        GameManager.Instance?.TakeDamage();
        if (SceneManager.GetActiveScene().name != "Game(area6)")
            return;

        Texture2D wrongArt = selectedIndex > 1 &&
                             alternateIncorrectArtwork != null &&
                             alternateIncorrectArtwork.Length > index &&
                             alternateIncorrectArtwork[index] != null
            ? alternateIncorrectArtwork[index]
            : incorrectArtwork[index];
        SetChallengeArtwork(wrongArt);
        challengeFeedback.text = IncorrectFeedback[index] +
                                 IntegritySuffix();
        challengeFeedback.color = new Color(0.72f, 0.05f, 0.1f, 1f);
        PlayCheckpointVoice(IncorrectVoiceKeys[index]);
        ConfigureActionButton("TRY AGAIN", RetryOpenCheckpoint);
    }

    private static string IntegritySuffix()
    {
        GameManager manager = GameManager.Instance;
        return manager == null
            ? string.Empty
            : "  System integrity: " + manager.systemIntegrity + "/3";
    }

    private void RetryOpenCheckpoint()
    {
        if (openCheckpoint != null)
            ShowPrompt(openCheckpoint.CheckpointIndex);
    }

    private void FinishCorrectAnswer()
    {
        if (openCheckpoint == null)
            return;

        StopChallengeArtworkAnimation();
        StopCheckpointVoice();
        Area6Checkpoint resolved = openCheckpoint;
        int resolvedIndex = resolved.CheckpointIndex;
        openCheckpoint = null;
        resolved.MarkResolved();
        currentCheckpoint++;
        challengeCanvas.SetActive(false);

        if (resolvedIndex == checkpoints.Count - 1)
        {
            CompleteMission();
            return;
        }

        SetPlayerControl(true);
    }

    private void CompleteMission()
    {
        StopCheckpointVoice();
        missionComplete = true;
        RestoreStreet();
        SetPlayerControl(false);

        int missionScore = GameManager.Instance != null
            ? GameManager.Instance.CurrentMissionScore
            : ChallengeScore * checkpoints.Count;
        SafeNetApiClient.Instance?.SaveMission(
            "area6",
            missionScore,
            true,
            SafeNetConstants.GradedMissionCount);

        challengeCanvas.SetActive(true);
        challengeTitle.text = "MISSION COMPLETE: FALSE WEB DISCONNECTED";
        challengeFeedback.text =
            "The corrupted browser network has been cleared. The exit is open and the restored cyber street is safe.";
        challengeFeedback.color = new Color(0.02f, 0.47f, 0.2f, 1f);
        SetChallengeArtwork(normalBackgroundSprite.texture);
        SetChoiceButtonsVisible(false);
        PlayCheckpointVoice("level6_complete");
        ConfigureActionButton("CONTINUE", ContinueToGameComplete);
    }

    private void RestoreStreet()
    {
        foreach (SpriteRenderer segment in backgroundSegments)
        {
            if (segment != null)
                segment.sprite = normalBackgroundSprite;
        }
    }

    private void ContinueToGameComplete()
    {
        StopCheckpointVoice();
        GameCompleteController.LoadScene();
    }

    private void StopCheckpointVoice()
    {
        if (!checkpointVoiceActive)
            return;

        VoiceLinePlayer.Stop();
        checkpointVoiceActive = false;
    }

    private void PlayCheckpointVoice(string voiceClipKey)
    {
        VoiceLinePlayer.Play(voiceClipKey);
        checkpointVoiceActive = !string.IsNullOrWhiteSpace(voiceClipKey);
    }

    private void OnDestroy()
    {
        StopChallengeArtworkAnimation();
        StopCheckpointVoice();
    }

    private IEnumerator PlayMalwareDisappearance(string actionLabel)
    {
        // Unity texture UVs start at the bottom-left. The artist arranged the
        // intended order across the top row first, then the bottom row.
        challengeArtwork.rectTransform.sizeDelta = new Vector2(520f, 520f);
        for (int frame = 0; frame < MalwareDisappearFrameCount; frame++)
        {
            int column = frame % 4;
            int row = frame < 4 ? 1 : 0;
            challengeArtwork.uvRect = new Rect(column * 0.25f, row * 0.5f, 0.25f, 0.5f);
            yield return new WaitForSecondsRealtime(MalwareDisappearFrameSeconds);
        }

        // The last painted frame contains only a few particles. Hide the raw
        // image after it has played so the obstacle is completely gone.
        challengeArtwork.gameObject.SetActive(false);
        challengeArtworkAnimation = null;
        ConfigureActionButton(actionLabel, FinishCorrectAnswer);
    }

    private void StopChallengeArtworkAnimation()
    {
        if (challengeArtworkAnimation != null)
        {
            StopCoroutine(challengeArtworkAnimation);
            challengeArtworkAnimation = null;
        }

        if (challengeArtwork != null)
            challengeArtwork.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private void SetPlayerControl(bool allow)
    {
        if (player != null)
            player.enabled = allow;
        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
    }

    private void BuildChallengeInterface()
    {
        challengeCanvas = new GameObject(
            "Area 6 Security Challenge Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        // Keep the overlay as a root canvas so scene hierarchy transforms do
        // not offset or scale it differently on phones and tablets.
        challengeCanvas.transform.SetParent(null, false);

        Canvas canvas = challengeCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 95;

        CanvasScaler scaler = challengeCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image dimmer = CreatePanel(
            challengeCanvas.transform,
            "Dimmed Gameplay",
            Vector2.zero,
            new Vector2(1920f, 1080f),
            new Color(0.005f, 0.012f, 0.03f, 0.88f));
        dimmer.raycastTarget = true;

        Image panel = CreatePanel(
            challengeCanvas.transform,
            "Security Decision Panel",
            Vector2.zero,
            new Vector2(1500f, 1010f),
            new Color(0.94f, 0.98f, 1f, 0.99f));
        Outline panelOutline = panel.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.05f, 0.78f, 0.95f, 1f);
        panelOutline.effectDistance = new Vector2(5f, -5f);

        challengeTitle = CreateText(
            panel.transform,
            "Challenge Title",
            new Vector2(0f, 438f),
            new Vector2(1360f, 82f),
            38f,
            new Color(0.02f, 0.18f, 0.3f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold);

        GameObject artObject = new GameObject(
            "Challenge Artwork (Malware Disappears Here)",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        artObject.transform.SetParent(panel.transform, false);
        challengeArtwork = artObject.GetComponent<RawImage>();
        challengeArtwork.color = Color.white;
        challengeArtwork.raycastTarget = false;
        RectTransform artRect = challengeArtwork.rectTransform;
        artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(0.5f, 0.5f);
        artRect.anchoredPosition = new Vector2(0f, 105f);

        challengeFeedback = CreateText(
            panel.transform,
            "Security Explanation",
            new Vector2(0f, -238f),
            new Vector2(1320f, 90f),
            28f,
            new Color(0.08f, 0.17f, 0.25f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Normal);

        for (int index = 0; index < 3; index++)
        {
            Button button = CreateButton(
                panel.transform,
                "Answer " + (char)('A' + index),
                new Vector2(0f, -330f - index * 78f),
                new Vector2(1220f, 66f),
                "Answer");
            choiceButtons.Add(button);
        }

        actionButton = CreateButton(
            panel.transform,
            "Challenge Action",
            new Vector2(0f, -397f),
            new Vector2(520f, 78f),
            "CONTINUE");
        actionButtonLabel = actionButton.GetComponentInChildren<TMP_Text>(true);
        challengeCanvas.SetActive(false);
    }

    private void ConfigureActionButton(string label, UnityEngine.Events.UnityAction action)
    {
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(action);
        SafeNetSfxPlayer.HookButton(actionButton);
        if (actionButtonLabel != null)
            actionButtonLabel.text = label;
        actionButton.gameObject.SetActive(true);
    }

    private void SetChoiceButtonsVisible(bool visible)
    {
        foreach (Button button in choiceButtons)
            button.gameObject.SetActive(visible);
    }

    private void SetChallengeArtwork(Texture texture)
    {
        challengeArtwork.gameObject.SetActive(true);
        challengeArtwork.texture = texture;
        challengeArtwork.uvRect = new Rect(0f, 0f, 1f, 1f);
        if (texture == null)
            return;

        float aspect = texture.height > 0 ? (float)texture.width / texture.height : 1f;
        const float maxWidth = 1250f;
        const float maxHeight = 580f;
        float width = maxWidth;
        float height = width / Mathf.Max(0.01f, aspect);
        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * aspect;
        }
        challengeArtwork.rectTransform.sizeDelta = new Vector2(width, height);
    }

    private static Image CreatePanel(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject item = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.sprite = null;
        image.color = color;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles style)
    {
        GameObject item = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        TMP_Text text = item.GetComponent<TMP_Text>();
        text.text = string.Empty;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(18f, fontSize - 9f);
        text.fontSizeMax = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        string label)
    {
        Image image = CreatePanel(
            parent,
            name,
            position,
            size,
            new Color(0.96f, 0.99f, 1f, 1f));
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.03f, 0.24f, 0.38f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 0.96f, 1f, 1f);
        colors.pressedColor = new Color(0.3f, 0.85f, 0.95f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        CreateText(
            button.transform,
            "Label",
            Vector2.zero,
            new Vector2(size.x - 50f, size.y - 10f),
            28f,
            new Color(0.015f, 0.08f, 0.13f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold).text = label;
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }
}
