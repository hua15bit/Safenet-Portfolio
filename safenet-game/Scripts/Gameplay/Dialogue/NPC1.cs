using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Stores one branching NPC conversation as a reusable Unity asset. The
/// historic name NPC1 is retained because existing scene assets reference it.
/// </summary>
[CreateAssetMenu(fileName = "NewNPCDialogue", menuName = "NPC Dialogue")]
public class NPC1 : ScriptableObject
{
    public string npcName;
    public Sprite npcPortrait;
    public string[] dialogueLines;

    [Header("Per-Line Speaker Identity")]
    [Tooltip("Optional speaker names matched by dialogue line index. Empty entries use npcName.")]
    public string[] lineSpeakerNames;

    [Tooltip("Optional portraits matched by dialogue line index. Missing entries use npcPortrait.")]
    public Sprite[] lineSpeakerPortraits;

    [Header("Voice Dubbing")]
    [Tooltip("Optional Resources/VoiceLines keys matched by dialogue line index. " +
             "When this array is populated, an empty entry intentionally keeps that line silent.")]
    public string[] voiceClipKeys;

    public bool[] autoProgressLines;
    public bool[] endDialogueLines;
    public float autoProgressDelay = 1.5f;
    public float typingSpeed = 0.05f;
    public float voicePitch = 1f;

    public DialogueChoice[] choices;
}

[System.Serializable]

/// <summary>
/// Defines the choices shown at one dialogue line and the line, score change,
/// and integrity outcome reached by each selection.
/// </summary>
public class DialogueChoice
{
    public int dialogueIndex;
    public string[] choices;
    public int[] nextDialogueIndexes;

    [Tooltip("Optional Resources/VoiceLines key played when these choices appear.")]
    public string voiceClipKey;

    [Header("Thesis Mechanics")]
    [Tooltip("Matches array length of choices. Positive values reward, negative values penalize and deal damage.")]
    public int[] scoreModifiers;
}
