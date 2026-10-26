# SafeNet Code Map

This guide separates the code by responsibility so a presenter can explain the
project without searching through every script. Runtime code lives under
`Assets/Scripts`; Unity Editor automation lives under `Assets/Editor` and is not
part of the released game.

## Runtime folders

- `Core/` - application-wide state and services. `GameManager` owns score and
  system integrity, `SafeNetApiClient` owns login/progress data,
  `SafeNetConstants` keeps shared mission totals consistent, and
  `SafeNetApplication` provides the shared exit action.
- `Audio/` - persistent background music, button/interaction effects, voice
  lines, and the scene-to-music library.
- `Input/` - the state and pointer handlers for mobile movement and interaction.
- `UI/` - shared dialogue, pause/score HUD, safe-area layout, and UI theme assets.
- `Gameplay/Dialogue/` - reusable branching dialogue data and its dialogue runner.
- `Gameplay/Interaction/` - the common contract used by NPCs, terminals, doors,
  and checkpoints.
- `Gameplay/Player/` - player movement and occasional idle animation.
- `Gameplay/Progression/` - the shared mission-exit behavior.
- `Gameplay/Visuals/` - background animation and editor-only scene previews.
- `Scenes/` - behavior that belongs to one menu, tutorial, mission, or result scene.

## Editor folders

- `Assets/Editor/Build/` - Android/Windows settings and presentation builds.
- `Assets/Editor/Scenes/` - tools that rebuild or repair a particular scene.
- `Assets/Editor/SharedSetup/` - setup shared by multiple scenes, such as music,
  mobile controls, and editor-only art previews.
- `Assets/Editor/Audits/` - hierarchy cleanup and readiness checks.

Editor scripts may create or repair serialized scene objects, but they do not run
inside the final Android or Windows build. Scene builders, project settings,
audits, and builds run only from explicit `SafeNet` menu commands; opening the
project cannot silently rewrite scenes or launch a build.

## Scene-to-script map

| Scene | Main runtime scripts | Responsibility |
| --- | --- | --- |
| `Login` | `LoginController` | Validates the form and asks `SafeNetApiClient` to authenticate. |
| `Start Menu` | `MainMenu`, `MobileCreditsPanel`, `StartMenuButtonLayout` | Starts the story, shows credits, or exits the current session. |
| `Mission Select` | `MissionSelectionController` | Displays missions and applies progression locks. |
| `Story Cinematic` | `CinematicStoryController` | Plays or skips the introduction, then opens the tutorial. |
| `Tutorial` | `TutorialSceneBootstrap`, `TutorialFlow`, `TutorialAegisInteractable`, `TutorialExit` | Teaches controls and completes the phishing practice before opening Mission 1. |
| `Game(area1)` | `Dialogue1`, `NPC1`, `Area1StoryboardVisuals` | Runs the first phishing conversation and updates its monitor artwork. |
| `Game(area2)` | `Area2SceneBootstrap`, `Area2StoryboardVisuals`, `Area2MessageRoutingMinigame` | Runs two smishing scenarios followed by the message-routing simulation. |
| `Game(area3)` | `Area3SceneBootstrap`, `Area3StageInteractable`, `Area3StoryOverlay` | Orders the social-engineering checkpoints and blocks later stages until they unlock. |
| `Game(area4)` | `Area4SceneBootstrap`, `Area4StageInteractable`, `Area4StoryOverlay` | Runs the password-strength and password-sharing scenarios inside the vault. |
| `Game(area5)` | `Area5SceneBootstrap`, `Area5PrivacyInteraction`, `Area5AutoVault`, `Area5NpcAnimator` | Runs the personal-data sharing choice and completion presentation. |
| `Game(area6)` | `Area6SceneBootstrap`, `Area6Checkpoint` | Runs five ordered false-web challenges and restores the street after completion. |
| `GameOverScene` | `GameOverMenu`, `GameOverThreatEffects` | Presents the breach state and provides retry/menu navigation. |
| `Game Complete` | `GameCompleteController`, `GameCompleteEffects` | Saves final progress and summarizes the completed training run. |

## Startup and data flow

```text
Application starts
  -> audio and sound-effect services install automatically
  -> LoginController obtains or creates SafeNetApiClient
  -> successful login opens Start Menu
  -> story cinematic -> tutorial -> mission selection/gameplay

Player input
  -> keyboard input or MobileControlButton
  -> SafeNetMobileInput
  -> PlayerMovement
  -> nearby IInteractable (NPC, terminal, checkpoint, or door)

Mission decision
  -> dialogue/minigame checks the answer
  -> GameManager updates score or system integrity
  -> SafeNetApiClient records the best mission progress
  -> mission exit opens the next scene
```

`GameManager`, `SafeNetApiClient`, and the audio services persist between scenes.
Mission-specific bootstrap scripts do not replace those services; they configure
only the current environment, encounters, and presentation.

## Dialogue flow

1. An NPC or stage component implements `IInteractable`.
2. The interaction starts `Dialogue1` or a mission-specific conversation.
3. `NPC1` supplies speaker data, lines, branches, and score modifiers.
4. `DialogueController` displays the speaker, types the line, and creates real
   Unity buttons for choices.
5. The chosen branch updates `GameManager` and notifies the mission visual adapter.

Using real buttons keeps the visible answer and touch area identical on mobile.

## Progress and backend modes

- `GameManager` tracks the total score, the current mission's score segment, and
  system integrity for the active run.
- `SafeNetApiClient` exposes events such as login success, profile updates, and
  request failure so UI scripts do not perform network requests themselves.
- Development/tester mode keeps a temporary profile and mission scores in memory.
- Live mode is intentionally disabled until a replacement API is configured.
  The Unity app should receive an HTTPS API address, never a MongoDB connection string.
- The serializable API model fields use `snake_case` because their names match the
  server's JSON contract; they are an intentional exception to normal C# casing.

## Audio flow

`SafeNetMusicLibrary` maps exact scene names to tracks. `SafeNetAudioManager`
keeps one music source alive across scenes and crossfades only when the assigned
track changes. `SafeNetSfxPlayer` connects both saved buttons and buttons created
at runtime, while `VoiceLinePlayer` plays one dialogue clip at a time.

## Safe naming caveats

Three older names remain because Unity scenes and assets already depend on them:

- `Dialogue1` is the general branching dialogue runner, not dialogue for only one level.
- `NPC1` is the reusable dialogue-data asset type, not a single NPC instance.
- `Level2` is the shared mission-exit component used by several areas, not Mission 2 itself.

Treat these as legacy compatibility names during the presentation. Rename them
only in a separate migration pass that preserves each `.meta` GUID, updates every
serialized scene/asset reference, and reruns all scene tests.

Other exact identifiers also form part of the current scene contract:

- Gameplay scenes are named `Game(area1)` through `Game(area6)`.
- The player uses the `MC` tag.
- Resource paths and UI object names are case-sensitive lookup keys.

## Presentation talking points

1. **Separation of responsibilities:** global services, shared gameplay systems,
   and scene-specific missions are stored in different folders.
2. **Persistent consequences:** score and integrity belong to `GameManager`, so a
   scene change does not silently erase the player's run state.
3. **Mobile-safe interaction:** one input path supports keyboard and touch, and
   choices use visible Unity buttons instead of hidden image hitboxes.
4. **Data-driven dialogue:** conversation text and branches are stored separately
   from the UI that renders them.
5. **Ordered mission logic:** mission stage/checkpoint components prevent players
   from skipping required cybersecurity decisions.
6. **Backend isolation:** gameplay talks to one API client rather than directly to
   a database, making a future server replacement much safer.
7. **Editor versus runtime:** rebuild and audit tools help maintain scenes but are
   excluded from the released game.

When explaining any class, start with its XML summary, then cover serialized
Inspector references, Unity lifecycle methods (`Awake`, `Start`, `Update`), public
actions/events, and finally private helper methods.
