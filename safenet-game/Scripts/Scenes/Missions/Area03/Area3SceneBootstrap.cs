using System.Collections;
using UnityEngine;

/// <summary>
/// Rebuilds Area 3 as a staged social-engineering mission while preserving the
/// existing player, background, controls, integrity, score, and Area 4 exit.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class Area3SceneBootstrap : MonoBehaviour
{
    [Header("Existing Scene")]
    [SerializeField] private Dialogue1 aegisInteraction;

    [Header("Story Artwork")]
    [SerializeField] private Texture alertMessage;
    [SerializeField] private Texture countdownMessage;
    [SerializeField] private Texture verificationMessage;
    [SerializeField] private Texture correctOutcomeSheet;
    [SerializeField] private Texture incorrectOutcomeSheet;
    [SerializeField] private Texture completionSheet;

    [Header("World Props")]
    [SerializeField] private Sprite alertTerminalSprite;
    [SerializeField] private Sprite verificationCharacterSprite;
    [SerializeField] private Sprite identityMarkerSprite;
    [SerializeField] private Sprite profileOneSprite;
    [SerializeField] private Sprite profileTwoSprite;
    [SerializeField] private Sprite profileImposterSprite;

    private Area3StoryOverlay overlay;
    private Area3StageInteractable alertStage;
    private Area3StageInteractable verificationStage;
    private Area3StageInteractable identityStage;
    private Dialogue1 alertDialogue;
    private Dialogue1 verificationDialogue;
    private Dialogue1 identityDialogue;
    private Collider2D exitCollider;
    private SpriteRenderer[] identityProfiles;
    private Sprite sharedPortrait;
    private Collider2D aegisCollider;
    private bool aegisFinished;
    private bool alertFinished;
    private bool verificationFinished;
    private bool identityFinished;
    private float platformFloorY = -4.85f;

    private static Area3SceneBootstrap activeInstance;

    public static bool IsCompleted { get; private set; }
    public static bool SetupFailed { get; private set; }

    private void Awake()
    {
        IsCompleted = false;
        SetupFailed = false;

        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning("Area 3 already has a story controller. The duplicate was disabled.", this);
            enabled = false;
            return;
        }

        activeInstance = this;

        VoiceLinePlayer.Preload(
            "level3_intro",
            "level3_alert_password",
            "level3_alert_countdown",
            "level3_choice_prompt",
            "level3_correct_credentials",
            "level3_incorrect_credentials",
            "level3_verification_advice",
            "level3_identity_prompt",
            "level3_completion",
            "level3_aegis_verification_rules",
            "level3_alert_correct_advice",
            "level3_alert_password_advice",
            "level3_alert_partial_credentials",
            "level3_verification_request",
            "level3_verification_correct",
            "level3_verification_code_warning",
            "level3_verification_unsafe",
            "level3_verification_password_rule",
            "level3_identity_intro",
            "level3_identity_correct",
            "level3_identity_correct_advice",
            "level3_identity_unsafe",
            "level3_identity_warning_signs");

        if (aegisInteraction == null)
        {
            FailOpen("The Aegis interaction reference is missing.");
            return;
        }

        sharedPortrait = aegisInteraction != null && aegisInteraction.dialogueData != null
            ? aegisInteraction.dialogueData.npcPortrait
            : null;

        // Use the already-positioned guide as the authoritative platform line.
        // Imported Mission 3 sprites have different pivots and transparent
        // margins, so a single guessed Y coordinate made several of them float.
        SpriteRenderer guideRenderer = aegisInteraction.GetComponent<SpriteRenderer>();
        if (guideRenderer != null && guideRenderer.sprite != null)
            platformFloorY = guideRenderer.bounds.min.y;

        GameObject overlayObject = new GameObject("Area 3 Story Overlay", typeof(RectTransform));
        overlay = overlayObject.AddComponent<Area3StoryOverlay>();

        ConfigureAegisIntroduction();
        CreateAlertStage();
        CreateVerificationStage();
        CreateIdentityStage();
        ConfigureExit();
        Debug.Log(
            "Area 3 story initialized: Aegis -> account alert -> verification request -> identity check.",
            this);
    }

    private void ConfigureAegisIntroduction()
    {
        if (aegisInteraction == null)
            return;

        aegisInteraction.gameObject.name = "Aegis - Social Engineering Guide";

        // Keep the existing NPC's correct floor height. The previous version
        // accidentally treated its old local Y coordinate as a world Y value,
        // which placed Aegis several units above the platform.
        Vector3 aegisPosition = aegisInteraction.transform.position;
        aegisPosition.x = -8.1f;
        aegisInteraction.transform.position = aegisPosition;
        aegisCollider = aegisInteraction.GetComponent<Collider2D>();
        if (aegisCollider != null)
            aegisCollider.isTrigger = false;

        MatchAegisHeightToPlayer();

        // Aegis is a stationary guide in this scene. Disable only this scene
        // instance; the animator controller and other scenes remain unchanged.
        Animator aegisAnimator = aegisInteraction.GetComponent<Animator>();
        if (aegisAnimator != null)
            aegisAnimator.enabled = false;
        aegisInteraction.dialogueData = CreateDialogue(
            "Aegis",
            sharedPortrait,
            new[]
            {
                "Not every threat attacks the system directly. Some manipulate people.",
                "Question urgency, verify identities, and never surrender passwords or verification codes."
            },
            new[] { false, true },
            voiceClipKeys: new[]
            {
                "level3_intro",
                "level3_aegis_verification_rules"
            });
        aegisInteraction.DialogueEnded += HandleAegisFinished;
    }

    private void CreateAlertStage()
    {
        NPC1 data = CreateDialogue(
            "Suspicious Message",
            sharedPortrait,
            new[]
            {
                "Give me your password so I can fix the problem.",
                "You only have thirty seconds before your account is deleted.",
                "Correct. Authorized personnel should not ask for your password.",
                "Urgency and credential requests are classic social-engineering warning signs.",
                "Credentials compromised. Unsafe decisions will deduct health and points.",
                "Passwords must never be shared through unsolicited messages.",
                "Credentials compromised. Unsafe decisions will deduct health and points.",
                "Attackers can combine partial credentials with other stolen information."
            },
            new[] { false, false, false, true, false, true, false, true },
            new[] { false, false, true, false, true, false, true, false },
            CreateChoice(
                1,
                new[]
                {
                    "A. Refuse and contact official support",
                    "B. Give the password",
                    "C. Share only part of the password"
                },
                new[] { 2, 4, 6 },
                new[] { 10, -1, -1 },
                "level3_choice_prompt"),
            new[]
            {
                "level3_alert_password",
                "level3_alert_countdown",
                "level3_correct_credentials",
                "level3_alert_correct_advice",
                "level3_incorrect_credentials",
                "level3_alert_password_advice",
                "level3_incorrect_credentials",
                "level3_alert_partial_credentials"
            },
            lineSpeakerNames: new[]
            {
                "Suspicious Message", "Suspicious Message", "Aegis", "Aegis",
                "Aegis", "Aegis", "Aegis", "Aegis"
            },
            lineSpeakerPortraits: new[]
            {
                sharedPortrait, sharedPortrait, sharedPortrait, sharedPortrait,
                sharedPortrait, sharedPortrait, sharedPortrait, sharedPortrait
            });

        alertStage = CreateStage(
            "Urgent Account Alert Terminal",
            new Vector3(-3.45f, 0f, 0f),
            alertTerminalSprite,
            3.4f,
            data,
            out alertDialogue,
            alertMessage,
            countdownMessage,
            correctOutcomeSheet,
            incorrectOutcomeSheet,
            true);
        alertDialogue.DialogueEnded += HandleAlertFinished;
    }

    private void CreateVerificationStage()
    {
        NPC1 data = CreateDialogue(
            "Verification Request",
            verificationCharacterSprite,
            new[]
            {
                "A glitched classmate asks you to send the verification code 482731.",
                "Verify the person through another channel. Never share verification codes.",
                "Correct. Independent verification prevents impersonation attacks.",
                "A verification code is a temporary key to your account.",
                "Unsafe. Sending the code can let an attacker complete a login or password reset.",
                "Treat every verification code like a password."
            },
            new[] { false, false, false, true, false, true },
            new[] { false, false, true, false, true, false },
            CreateChoice(
                1,
                new[]
                {
                    "A. Refuse and verify independently",
                    "B. Send the verification code"
                },
                new[] { 2, 4 },
                new[] { 10, -1 }),
            new[]
            {
                "level3_verification_request",
                "level3_verification_advice",
                "level3_verification_correct",
                "level3_verification_code_warning",
                "level3_verification_unsafe",
                "level3_verification_password_rule"
            },
            lineSpeakerNames: new[]
            {
                "Glitched Classmate", "Aegis", "Aegis", "Aegis", "Aegis", "Aegis"
            },
            lineSpeakerPortraits: new[]
            {
                verificationCharacterSprite, sharedPortrait, sharedPortrait,
                sharedPortrait, sharedPortrait, sharedPortrait
            });

        verificationStage = CreateStage(
            "Glitched Classmate Verification",
            new Vector3(2.1f, 0f, 0f),
            verificationCharacterSprite,
            3.25f,
            data,
            out verificationDialogue,
            verificationMessage,
            verificationMessage,
            null,
            null,
            false);
        verificationDialogue.DialogueEnded += HandleVerificationFinished;
    }

    private void CreateIdentityStage()
    {
        NPC1 data = CreateDialogue(
            "Identity Check",
            identityMarkerSprite,
            new[]
            {
                "Three profiles claim to be trusted users. Inspect their channel and behavior.",
                "Find the suspicious identity.",
                "Correct. The ShadowLink profile uses an unofficial channel and urgent pressure.",
                "Verify identities using known records and a separate trusted channel.",
                "Unsafe. That profile uses a consistent official channel and calm behavior.",
                "A suspicious identity usually combines inconsistent details, urgency, and unofficial contact methods."
            },
            new[] { false, false, false, true, false, true },
            new[] { false, false, true, false, true, false },
            CreateChoice(
                1,
                new[]
                {
                    "A. Brya - CivicNet, calm and helpful",
                    "B. Kian - official campus directory, consistent details",
                    "C. 'Kian' - ShadowLink, pushy and urgent"
                },
                new[] { 4, 4, 2 },
                new[] { -1, -1, 10 }),
            new[]
            {
                "level3_identity_intro",
                "level3_identity_prompt",
                "level3_identity_correct",
                "level3_identity_correct_advice",
                "level3_identity_unsafe",
                "level3_identity_warning_signs"
            },
            lineSpeakerNames: new[]
            {
                "Aegis", "Aegis", "Aegis", "Aegis", "Aegis", "Aegis"
            },
            lineSpeakerPortraits: new[]
            {
                sharedPortrait, sharedPortrait, sharedPortrait,
                sharedPortrait, sharedPortrait, sharedPortrait
            });

        identityStage = CreateStage(
            "Suspicious Identity Challenge",
            new Vector3(7.55f, 0f, 0f),
            identityMarkerSprite,
            2.25f,
            data,
            out identityDialogue,
            null,
            null,
            null,
            null,
            false);
        identityDialogue.DialogueEnded += HandleIdentityFinished;

        identityProfiles = new[]
        {
            CreateDecoration("Profile A - Brya", profileOneSprite, 6.2f, 2.45f),
            CreateDecoration("Profile B - Kian", profileTwoSprite, 7.55f, 2.45f),
            CreateDecoration("Profile C - Suspicious Kian", profileImposterSprite, 8.9f, 2.45f)
        };
        SetIdentityProfilesVisible(false);
    }

    private Area3StageInteractable CreateStage(
        string objectName,
        Vector3 position,
        Sprite sprite,
        float displayHeight,
        NPC1 dialogueData,
        out Dialogue1 dialogue,
        Texture incoming,
        Texture choices,
        Texture correct,
        Texture incorrect,
        bool countdown)
    {
        GameObject root = new GameObject(objectName);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 0;
        PlaceSpriteOnFloor(root.transform, sprite, position.x, displayHeight);

        BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
        // The station is both the interaction target and a physical blocker.
        // It retires after resolution, so the player can continue afterward.
        trigger.isTrigger = false;
        trigger.size = sprite != null ? sprite.bounds.size : new Vector2(2.2f, 3f);
        trigger.offset = sprite != null ? (Vector2)sprite.bounds.center : Vector2.zero;

        GameObject dialogueObject = new GameObject(objectName + " Dialogue");
        dialogueObject.transform.SetParent(root.transform, false);
        dialogue = dialogueObject.AddComponent<Dialogue1>();
        dialogue.dialogueData = dialogueData;
        Area3StoryboardVisuals visuals = dialogueObject.AddComponent<Area3StoryboardVisuals>();
        visuals.Configure(overlay, incoming, choices, correct, incorrect, countdown);

        Area3StageInteractable stage = root.AddComponent<Area3StageInteractable>();
        stage.Configure(dialogue, trigger, renderer);
        return stage;
    }

    private SpriteRenderer CreateDecoration(string objectName, Sprite sprite, float x, float displayHeight)
    {
        if (sprite == null)
            return null;

        GameObject decoration = new GameObject(objectName);
        SpriteRenderer renderer = decoration.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 2;
        PlaceSpriteOnFloor(decoration.transform, sprite, x, displayHeight);
        return renderer;
    }

    private void PlaceSpriteOnFloor(Transform target, Sprite sprite, float x, float displayHeight)
    {
        float spriteHeight = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.y) : 1f;
        float scale = displayHeight / spriteHeight;
        float bottom = sprite != null ? sprite.bounds.min.y * scale : -displayHeight * 0.5f;
        target.localScale = Vector3.one * scale;
        target.position = new Vector3(x, platformFloorY - bottom, 0f);
    }

    private void ConfigureExit()
    {
        GameObject exit = GameObject.Find("Door");
        exitCollider = exit != null ? exit.GetComponent<Collider2D>() : null;
        if (exitCollider != null)
            exitCollider.enabled = false;
    }

    private void HandleAegisFinished(Dialogue1 dialogue)
    {
        if (aegisFinished)
            return;

        aegisFinished = true;
        if (aegisCollider != null)
            aegisCollider.enabled = false;
        alertStage?.SetUnlocked(true);
    }

    private void HandleAlertFinished(Dialogue1 dialogue)
    {
        if (alertFinished)
            return;

        alertFinished = true;
        alertStage?.Complete();
        verificationStage?.SetUnlocked(true);
    }

    private void HandleVerificationFinished(Dialogue1 dialogue)
    {
        if (verificationFinished)
            return;

        verificationFinished = true;
        verificationStage?.Complete();
        identityStage?.SetUnlocked(true);
        SpriteRenderer identityPrompt = identityStage != null
            ? identityStage.GetComponent<SpriteRenderer>()
            : null;
        if (identityPrompt != null)
            identityPrompt.enabled = false;
        SetIdentityProfilesVisible(true);
    }

    private void HandleIdentityFinished(Dialogue1 dialogue)
    {
        if (identityFinished || IsCompleted)
            return;

        identityFinished = true;
        identityStage?.Complete(false);
        SetIdentityProfilesVisible(false);
        IsCompleted = true;
        if (exitCollider != null)
            exitCollider.enabled = true;

        VoiceLinePlayer.Play("level3_completion");
        StartCoroutine(ShowMissionCompleteCelebration());
    }

    private IEnumerator ShowMissionCompleteCelebration()
    {
        const string completionSubtitle =
            "Outstanding! You have mastered the art of data privacy. Keep being a responsible and safe digital citizen.";

        if (completionSheet != null)
            overlay.ShowSheetFrame(completionSheet, 0, 4, 3, 620f);

        DialogueController subtitles = DialogueController.Instance;
        if (subtitles != null)
        {
            subtitles.SetNPCInfo("Aegis", sharedPortrait);
            subtitles.setDialogueText(completionSubtitle);
            subtitles.ShowDialogueUI(true);
        }

        while (VoiceLinePlayer.IsPlaying)
            yield return null;

        yield return new WaitForSecondsRealtime(0.2f);
        subtitles?.ShowDialogueUI(false);
        overlay.Hide();
    }

    private void MatchAegisHeightToPlayer()
    {
        SpriteRenderer aegisRenderer = aegisInteraction != null
            ? aegisInteraction.GetComponent<SpriteRenderer>()
            : null;
        GameObject player = GameObject.FindGameObjectWithTag("MC");
        SpriteRenderer playerRenderer = player != null
            ? player.GetComponent<SpriteRenderer>()
            : null;
        if (aegisRenderer == null || aegisRenderer.sprite == null || playerRenderer == null)
            return;

        float targetHeight = Mathf.Max(0.01f, playerRenderer.bounds.size.y);
        float scale = targetHeight / Mathf.Max(0.01f, aegisRenderer.sprite.bounds.size.y);
        float xSign = Mathf.Sign(aegisInteraction.transform.localScale.x);
        if (Mathf.Approximately(xSign, 0f))
            xSign = 1f;
        aegisInteraction.transform.localScale = new Vector3(xSign * scale, scale, 1f);

        Vector3 position = aegisInteraction.transform.position;
        position.y = platformFloorY - aegisRenderer.sprite.bounds.min.y * scale;
        aegisInteraction.transform.position = position;
        aegisRenderer.sortingOrder = 0;
    }

    private void SetIdentityProfilesVisible(bool visible)
    {
        if (identityProfiles == null)
            return;

        foreach (SpriteRenderer profile in identityProfiles)
            if (profile != null)
                profile.gameObject.SetActive(visible);
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
        data.name = "Runtime " + npcName;
        data.hideFlags = HideFlags.DontSave;
        data.npcName = npcName;
        data.npcPortrait = portrait;
        data.dialogueLines = lines;
        data.endDialogueLines = endLines;
        data.autoProgressLines = autoLines ?? new bool[lines.Length];
        data.autoProgressDelay = 1.25f;
        data.typingSpeed = 0.035f;
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
        int[] scoreModifiers,
        string voiceClipKey = null)
    {
        return new DialogueChoice
        {
            dialogueIndex = dialogueIndex,
            choices = choices,
            nextDialogueIndexes = nextIndexes,
            scoreModifiers = scoreModifiers,
            voiceClipKey = voiceClipKey
        };
    }

    private void OnDestroy()
    {
        if (aegisInteraction != null)
            aegisInteraction.DialogueEnded -= HandleAegisFinished;
        if (alertDialogue != null)
            alertDialogue.DialogueEnded -= HandleAlertFinished;
        if (verificationDialogue != null)
            verificationDialogue.DialogueEnded -= HandleVerificationFinished;
        if (identityDialogue != null)
            identityDialogue.DialogueEnded -= HandleIdentityFinished;

        VoiceLinePlayer.Stop();

        if (activeInstance == this)
            activeInstance = null;
    }

    private void FailOpen(string reason)
    {
        // A broken story reference must not strand the player in Area 3. Leave
        // the original scene and exit usable, while making the problem explicit.
        IsCompleted = true;
        SetupFailed = true;
        Debug.LogError($"Area 3 story setup was skipped: {reason} The original exit remains available.", this);
    }
}
