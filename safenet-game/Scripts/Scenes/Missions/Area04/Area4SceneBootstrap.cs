using System.Collections;
using UnityEngine;

/// <summary>
/// Rebuilds Area 4 as the weak-credentials vault mission while preserving the
/// existing player, mobile controls, pause menu, score, integrity, and Area 5 exit.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class Area4SceneBootstrap : MonoBehaviour
{
    private static readonly string[] Level4VoiceKeys =
    {
        "level4_aegis_weak_credentials",
        "level4_aegis_predictable_info",
        "level4_weak_result",
        "level4_weak_unsafe",
        "level4_strong_prompt",
        "level4_strong_correct",
        "level4_strong_unsafe",
        "level4_student_reuse_supplement",
        "level4_student_thanks_supplement",
        "level4_friend_request_supplement",
        "level4_friend_refuse_reveal_supplement",
        "level4_friend_trust_reveal",
        "level4_friend_temporary_reveal_supplement",
        "level4_weak_prompt",
        "level4_reuse_choice_prompt",
        "level4_reuse_correct",
        "level4_reuse_unsafe",
        "level4_reuse_understood",
        "level4_friend_choice_prompt",
        "level4_friend_refuse",
        "level4_friend_correct",
        "level4_friend_trust_share",
        "level4_friend_trust_unsafe",
        "level4_friend_temporary_share",
        "level4_friend_temporary_unsafe"
    };

    [Header("Colored Level 4 Environment")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite platformSprite;
    [SerializeField] private Sprite monitorSprite;
    [SerializeField] private Sprite securedMonitorSprite;
    [SerializeField] private Sprite weakPasswordChoicesSprite;
    [SerializeField] private Sprite strongPasswordChoicesSprite;
    [SerializeField] private Sprite studentSprite;
    [SerializeField] private Sprite fakeFriendSprite;
    [SerializeField] private Sprite unsafeDoorSprite;
    [SerializeField] private Sprite securedDoorSprite;
    [SerializeField] private Sprite aegisSprite;

    private Area4StoryOverlay overlay;
    private Area4StageInteractable aegisStage;
    private Area4StageInteractable weakPasswordStage;
    private Area4StageInteractable strongPasswordStage;
    private Area4StageInteractable studentStage;
    private Area4StageInteractable fakeFriendStage;
    private Dialogue1 aegisDialogue;
    private Dialogue1 weakPasswordDialogue;
    private Dialogue1 strongPasswordDialogue;
    private Dialogue1 studentDialogue;
    private Dialogue1 fakeFriendDialogue;
    private Collider2D exitCollider;
    private SpriteRenderer exitRenderer;
    private DialogueController dialogueController;
    private Sprite playerPortrait;
    private float platformFloorY = -5.55f;

    private static Area4SceneBootstrap activeInstance;

    public static bool IsCompleted { get; private set; }
    public static bool SetupFailed { get; private set; }

    private void Awake()
    {
        IsCompleted = false;
        SetupFailed = false;

        if (activeInstance != null && activeInstance != this)
        {
            enabled = false;
            return;
        }
        activeInstance = this;

        // Area 4 builds every conversation at runtime, so prepare its clips
        // before any of those temporary dialogue objects can be interacted with.
        VoiceLinePlayer.Preload(Level4VoiceKeys);

        if (!ValidateAssets())
        {
            FailOpen("One or more colored Level 4 assets are missing.");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("MC");
        SpriteRenderer playerRenderer = player != null ? player.GetComponent<SpriteRenderer>() : null;
        if (playerRenderer == null)
        {
            FailOpen("The MainMC sprite renderer could not be found.");
            return;
        }
        platformFloorY = playerRenderer.bounds.min.y;
        playerPortrait = playerRenderer.sprite;

        dialogueController = FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        if (dialogueController == null)
        {
            FailOpen("The dialogue controller could not be found.");
            return;
        }

        // This layout uses visible, full-size Unity buttons. The background art
        // never handles taps, preventing the mobile mis-inputs seen in Area 2.
        dialogueController.useImmersiveChoiceLayout = false;
        dialogueController.useSpriteBackedChoiceLayout = true;
        dialogueController.useLightStoryChoiceButtons = false;

        DisableLegacyAreaFourArtwork();
        BuildVaultEnvironment();

        GameObject overlayObject = new GameObject("Area 4 Story Overlay", typeof(RectTransform));
        overlay = overlayObject.AddComponent<Area4StoryOverlay>();

        CreateAegisIntroduction();
        CreateWeakPasswordTerminal();
        CreateStrongPasswordTerminal();
        CreateStudentScenario();
        CreateFakeFriendScenario();
        ConfigureExit();

        aegisStage.SetUnlocked(true);
        Debug.Log(
            "Area 4 initialized: Aegis -> weak password -> strong password -> reuse -> fake friend -> vault.",
            this);
    }

    private bool ValidateAssets()
    {
        return backgroundSprite != null && platformSprite != null && monitorSprite != null &&
               securedMonitorSprite != null && weakPasswordChoicesSprite != null &&
               strongPasswordChoicesSprite != null && studentSprite != null &&
               fakeFriendSprite != null && unsafeDoorSprite != null &&
               securedDoorSprite != null && aegisSprite != null;
    }

    private void DisableLegacyAreaFourArtwork()
    {
        GameObject oldBackground = GameObject.Find("background4_0");
        if (oldBackground != null)
            oldBackground.SetActive(false);

        GameObject oldNpc = GameObject.Find("New Old Man NPC_0");
        if (oldNpc != null)
            oldNpc.SetActive(false);
    }

    private void BuildVaultEnvironment()
    {
        // A slightly taller wall prevents the camera clear color from appearing
        // above the artwork on wide Android screens.
        const float backgroundHeight = 13.5f;
        float backgroundWidth = backgroundHeight *
                                backgroundSprite.rect.width / backgroundSprite.rect.height;
        float backgroundBottom = platformFloorY - 1.05f;

        // Keep both wall panels under one transform. The general background
        // animator used to move and enlarge every repeated panel separately,
        // causing the seams to overlap. One shared parallax layer keeps the
        // panorama flush while retaining a subtle sense of depth.
        GameObject panorama = new GameObject("Colored Vault Panorama");
        float firstBackgroundX = -24f;
        for (int i = 0; i < 2; i++)
        {
            CreateScenery(
                $"Vault Wall Panel {i + 1}",
                backgroundSprite,
                firstBackgroundX + i * backgroundWidth,
                backgroundBottom,
                backgroundHeight,
                -20,
                panorama.transform);
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            panorama.AddComponent<CyberParallaxLayer>().Configure(mainCamera, 0.22f);

        const float platformWidth = 23f;
        float platformHeight = platformWidth * platformSprite.rect.height / platformSprite.rect.width;
        GameObject floor = new GameObject("Colored Vault Floor");
        for (int i = 0; i < 2; i++)
        {
            CreateScenery(
                $"Vault Floor Panel {i + 1}",
                platformSprite,
                -23f + i * platformWidth,
                platformFloorY - platformHeight + 0.12f,
                platformHeight,
                -8,
                floor.transform);
        }
    }

    private void CreateAegisIntroduction()
    {
        NPC1 data = CreateDialogue(
            "Aegis",
            aegisSprite,
            new[]
            {
                "The weak credentials have allowed corruption to enter the vault.",
                "Remember, predictable information makes passwords easier to guess."
            },
            new[] { false, true },
            voiceClipKeys: new[]
            {
                "level4_aegis_weak_credentials",
                "level4_aegis_predictable_info"
            });

        aegisStage = CreateStage(
            "Aegis - Vault Security Guide",
            -8.7f,
            aegisSprite,
            1.75f,
            0.35f,
            data,
            out aegisDialogue,
            null,
            null,
            null,
            null);
        aegisDialogue.DialogueEnded += HandleAegisFinished;
    }

    private void CreateWeakPasswordTerminal()
    {
        NPC1 data = CreateDialogue(
            "Weak Password Terminal",
            weakPasswordChoicesSprite,
            new[]
            {
                "Identify and select the weakest password.",
                "That password is too weak and easy to guess.",
                "Strong passwords are your first defense. Make them long, unique, and difficult to predict."
            },
            new[] { false, true, true },
            null,
            CreateChoice(
                0,
                new[] { "A. 8142026", "B. BiRth8_14,2026", "C. 8_14_2026" },
                new[] { 1, 2, 2 },
                new[] { 5, -1, -1 }),
            new[]
            {
                "level4_weak_prompt",
                "level4_weak_result",
                "level4_weak_unsafe"
            });

        weakPasswordStage = CreateStage(
            "Weak Password Terminal",
            -4.7f,
            monitorSprite,
            3.45f,
            0f,
            data,
            out weakPasswordDialogue,
            monitorSprite,
            weakPasswordChoicesSprite,
            securedMonitorSprite,
            weakPasswordChoicesSprite);
        weakPasswordDialogue.DialogueEnded += HandleWeakPasswordFinished;
    }

    private void CreateStrongPasswordTerminal()
    {
        NPC1 data = CreateDialogue(
            "Strong Password Terminal",
            strongPasswordChoicesSprite,
            new[]
            {
                "Choose a secure password for this terminal.",
                "Excellent choice. A long and unique password gives your account stronger protection.",
                "Strong passwords are your first defense. Make them long, unique, and difficult to predict."
            },
            new[] { false, true, true },
            null,
            CreateChoice(
                0,
                new[] { "A. aira203", "B. Aira2026", "C. _AiR@_13_2026" },
                new[] { 2, 2, 1 },
                new[] { -1, -1, 5 }),
            new[]
            {
                "level4_strong_prompt",
                "level4_strong_correct",
                "level4_strong_unsafe"
            });

        strongPasswordStage = CreateStage(
            "Strong Password Terminal",
            -1.05f,
            monitorSprite,
            3.45f,
            0f,
            data,
            out strongPasswordDialogue,
            monitorSprite,
            strongPasswordChoicesSprite,
            securedMonitorSprite,
            strongPasswordChoicesSprite);
        strongPasswordDialogue.DialogueEnded += HandleStrongPasswordFinished;
    }

    private void CreateStudentScenario()
    {
        NPC1 data = CreateDialogue(
            "Student",
            studentSprite,
            new[]
            {
                "I used the same password for my school account, social media, and online shop. It's easier to remember.",
                "Using one password for several accounts is risky. What should the student do?",
                "Correct. Use a different strong password for every account so one breach cannot affect them all.",
                "Thank you for the advice. I'll create a different password for each of my accounts.",
                "Unsafe. Reusing a password, or changing only one number, still leaves every account predictable.",
                "I understand. Each account should have its own unique strong password."
            },
            new[] { false, false, false, true, false, true },
            new[] { false, false, true, false, true, false },
            CreateChoice(
                1,
                new[]
                {
                    "A. Continue using the same password for every account",
                    "B. Use a different strong password for every account",
                    "C. Use the same password but change one number"
                },
                new[] { 4, 2, 4 },
                new[] { -1, 5, -1 }),
            new[]
            {
                "level4_student_reuse_supplement",
                "level4_reuse_choice_prompt",
                "level4_reuse_correct",
                "level4_student_thanks_supplement",
                "level4_reuse_unsafe",
                "level4_reuse_understood"
            },
            lineSpeakerNames: new[]
            {
                "Student", "Aira", "Aira", "Student", "Aira", "Student"
            },
            lineSpeakerPortraits: new[]
            {
                studentSprite, playerPortrait, playerPortrait,
                studentSprite, playerPortrait, studentSprite
            });

        studentStage = CreateStage(
            "Student - Password Reuse Scenario",
            3f,
            studentSprite,
            2.85f,
            0f,
            data,
            out studentDialogue,
            null,
            null,
            null,
            null);
        studentDialogue.DialogueEnded += HandleStudentFinished;
    }

    private void CreateFakeFriendScenario()
    {
        NPC1 data = CreateDialogue(
            "Jane? (Friend)",
            fakeFriendSprite,
            new[]
            {
                "Hey, Aira. I need to check something quickly. Can you send me your password? I promise. I won't share it.",
                "Passwords should remain private, even between friends. Choose your response.",
                "No. I cannot share my password. I will verify your request through another channel.",
                "What? You figured it out? Fine. You protected your account this time.",
                "Correct. Keeping passwords private prevents unauthorized account access.",
                "Okay, I will send it because I trust you.",
                "Perfect. Now I can access your account, and there is nothing you can do about it.",
                "Unsafe. Trust does not make password sharing safe. Verify the person independently.",
                "I will share it temporarily, but I will change it afterward.",
                "Too late, Aira. I only needed a few seconds to take control of your account.",
                "Unsafe. Temporary access is still enough for an attacker to change account settings."
            },
            new[] { false, false, false, false, true, false, false, true, false, false, true },
            new[] { false, false, true, true, false, true, true, false, true, true, false },
            CreateChoice(
                1,
                new[]
                {
                    "A. Share it temporarily and change it later",
                    "B. Share it because they are a friend",
                    "C. Refuse and keep the password private"
                },
                new[] { 8, 5, 2 },
                new[] { -1, -1, 5 }),
            new[]
            {
                "level4_friend_request_supplement",
                "level4_friend_choice_prompt",
                "level4_friend_refuse",
                "level4_friend_refuse_reveal_supplement",
                "level4_friend_correct",
                "level4_friend_trust_share",
                "level4_friend_trust_reveal",
                "level4_friend_trust_unsafe",
                "level4_friend_temporary_share",
                "level4_friend_temporary_reveal_supplement",
                "level4_friend_temporary_unsafe"
            },
            lineSpeakerNames: new[]
            {
                "Jane? (Friend)", "Aira", "Aira", "Jane? (Friend)", "Aegis",
                "Aira", "Jane? (Friend)", "Aegis", "Aira", "Jane? (Friend)", "Aegis"
            },
            lineSpeakerPortraits: new[]
            {
                fakeFriendSprite, playerPortrait, playerPortrait, fakeFriendSprite, aegisSprite,
                playerPortrait, fakeFriendSprite, aegisSprite, playerPortrait, fakeFriendSprite, aegisSprite
            });

        fakeFriendStage = CreateStage(
            "Fake Friend - Password Request",
            7.15f,
            fakeFriendSprite,
            2.85f,
            0f,
            data,
            out fakeFriendDialogue,
            null,
            null,
            null,
            null);
        fakeFriendDialogue.DialogueEnded += HandleFakeFriendFinished;
    }

    private Area4StageInteractable CreateStage(
        string objectName,
        float x,
        Sprite sprite,
        float displayHeight,
        float bottomOffset,
        NPC1 dialogueData,
        out Dialogue1 dialogue,
        Sprite incoming,
        Sprite choices,
        Sprite correct,
        Sprite incorrect)
    {
        GameObject root = new GameObject(objectName);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 0;
        PlaceSpriteOnFloor(root.transform, sprite, x, displayHeight, bottomOffset);

        BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
        // Active characters and terminals are solid world blockers. Completing
        // a stage disables this collider before unlocking the next encounter.
        trigger.isTrigger = false;
        trigger.size = sprite.bounds.size;
        trigger.offset = sprite.bounds.center;

        GameObject dialogueObject = new GameObject(objectName + " Dialogue");
        dialogueObject.transform.SetParent(root.transform, false);
        dialogue = dialogueObject.AddComponent<Dialogue1>();
        dialogue.ConfigureDialogue(dialogueData);
        Area4StoryboardVisuals visuals = dialogueObject.AddComponent<Area4StoryboardVisuals>();
        visuals.Configure(overlay, incoming, choices, correct, incorrect);

        Area4StageInteractable stage = root.AddComponent<Area4StageInteractable>();
        stage.Configure(dialogue, trigger, renderer);
        return stage;
    }

    private void ConfigureExit()
    {
        GameObject exit = GameObject.Find("Door");
        if (exit == null)
        {
            FailOpen("The Area 5 exit object could not be found.");
            return;
        }

        exitRenderer = exit.GetComponent<SpriteRenderer>();
        if (exitRenderer == null)
            exitRenderer = exit.AddComponent<SpriteRenderer>();
        exitRenderer.sprite = unsafeDoorSprite;
        exitRenderer.sortingOrder = 2;
        exitRenderer.flipX = true;
        PlaceSpriteOnFloor(exit.transform, unsafeDoorSprite, 11.55f, 4.65f, 0f);

        exitCollider = exit.GetComponent<Collider2D>();
        if (exitCollider == null)
            exitCollider = exit.AddComponent<BoxCollider2D>();
        if (exitCollider is BoxCollider2D box)
        {
            box.isTrigger = true;
            box.size = unsafeDoorSprite.bounds.size;
            box.offset = unsafeDoorSprite.bounds.center;
        }
        exitCollider.enabled = false;
    }

    private void HandleAegisFinished(Dialogue1 dialogue)
    {
        aegisStage.Complete(null, false);
        weakPasswordStage.SetUnlocked(true);
    }

    private void HandleWeakPasswordFinished(Dialogue1 dialogue)
    {
        weakPasswordStage.Complete(securedMonitorSprite);
        strongPasswordStage.SetUnlocked(true);
    }

    private void HandleStrongPasswordFinished(Dialogue1 dialogue)
    {
        strongPasswordStage.Complete(securedMonitorSprite);

        // Character decisions need the question text above their real choice
        // buttons. Terminal encounters before this point keep their artwork-
        // backed layout; Student and Jane use the readable dialogue layout.
        if (dialogueController != null)
        {
            dialogueController.useSpriteBackedChoiceLayout = false;
            dialogueController.useLightStoryChoiceButtons = true;
        }
        studentStage.SetUnlocked(true);
    }

    private void HandleStudentFinished(Dialogue1 dialogue)
    {
        studentStage.Complete(null, false);
        fakeFriendStage.SetUnlocked(true);
    }

    private void HandleFakeFriendFinished(Dialogue1 dialogue)
    {
        if (IsCompleted)
            return;

        fakeFriendStage.Complete(null, false);
        IsCompleted = true;

        if (exitRenderer != null)
            exitRenderer.sprite = securedDoorSprite;
        if (exitCollider != null)
            exitCollider.enabled = true;

        StartCoroutine(ShowCompletionMessage());
    }

    private IEnumerator ShowCompletionMessage()
    {
        GameObject player = GameObject.FindGameObjectWithTag("MC");
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        movement?.ShowNotification(
            "VAULT SECURED - Password threats neutralized. Area 5 exit unlocked.",
            string.Empty);
        yield return new WaitForSecondsRealtime(2.5f);
        overlay?.Hide();
    }

    private SpriteRenderer CreateScenery(
        string objectName,
        Sprite sprite,
        float left,
        float bottom,
        float displayHeight,
        int sortingOrder,
        Transform parent = null)
    {
        GameObject scenery = new GameObject(objectName);
        if (parent != null)
            scenery.transform.SetParent(parent, false);
        SpriteRenderer renderer = scenery.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;

        float scale = displayHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
        scenery.transform.localScale = Vector3.one * scale;
        scenery.transform.position = new Vector3(
            left - sprite.bounds.min.x * scale,
            bottom - sprite.bounds.min.y * scale,
            0f);
        return renderer;
    }

    private void PlaceSpriteOnFloor(
        Transform target,
        Sprite sprite,
        float x,
        float displayHeight,
        float bottomOffset)
    {
        float scale = displayHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
        float bottom = sprite.bounds.min.y * scale;
        target.localScale = Vector3.one * scale;
        target.position = new Vector3(x, platformFloorY + bottomOffset - bottom, 0f);
    }

    private static NPC1 CreateDialogue(
        string npcName,
        Sprite portrait,
        string[] lines,
        bool[] endLines,
        bool[] autoLines = null,
        DialogueChoice choice = null,
        string[] voiceClipKeys = null,
        string[] lineSpeakerNames = null,
        Sprite[] lineSpeakerPortraits = null)
    {
        NPC1 data = ScriptableObject.CreateInstance<NPC1>();
        data.name = "Runtime Area4 " + npcName;
        data.hideFlags = HideFlags.DontSave;
        data.npcName = npcName;
        data.npcPortrait = portrait;
        data.dialogueLines = lines;
        data.endDialogueLines = endLines;
        data.autoProgressLines = autoLines ?? new bool[lines.Length];
        data.autoProgressDelay = 1.15f;
        data.typingSpeed = 0.03f;
        data.voicePitch = 1f;
        data.voiceClipKeys = voiceClipKeys ?? new string[lines.Length];
        data.lineSpeakerNames = lineSpeakerNames ?? new string[lines.Length];
        data.lineSpeakerPortraits = lineSpeakerPortraits ?? new Sprite[lines.Length];
        data.choices = choice != null ? new[] { choice } : new DialogueChoice[0];
        return data;
    }

    private static DialogueChoice CreateChoice(
        int dialogueIndex,
        string[] choices,
        int[] nextIndexes,
        int[] scoreModifiers)
    {
        return new DialogueChoice
        {
            dialogueIndex = dialogueIndex,
            choices = choices,
            nextDialogueIndexes = nextIndexes,
            scoreModifiers = scoreModifiers
        };
    }

    private void OnDestroy()
    {
        if (aegisDialogue != null)
            aegisDialogue.DialogueEnded -= HandleAegisFinished;
        if (weakPasswordDialogue != null)
            weakPasswordDialogue.DialogueEnded -= HandleWeakPasswordFinished;
        if (strongPasswordDialogue != null)
            strongPasswordDialogue.DialogueEnded -= HandleStrongPasswordFinished;
        if (studentDialogue != null)
            studentDialogue.DialogueEnded -= HandleStudentFinished;
        if (fakeFriendDialogue != null)
            fakeFriendDialogue.DialogueEnded -= HandleFakeFriendFinished;

        if (activeInstance == this)
            activeInstance = null;
    }

    private void FailOpen(string reason)
    {
        IsCompleted = true;
        SetupFailed = true;
        Debug.LogError($"Area 4 story setup failed: {reason} The original exit remains available.", this);

        GameObject exit = GameObject.Find("Door");
        Collider2D originalExit = exit != null ? exit.GetComponent<Collider2D>() : null;
        if (originalExit != null)
            originalExit.enabled = true;
    }
}
