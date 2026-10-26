using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animates the restored network elements on the Game Complete screen and
/// rotates through short status messages without filtering the full display.
/// </summary>
public sealed class GameCompleteEffects : MonoBehaviour
{
    [SerializeField] private RectTransform shieldCore;
    [SerializeField] private RectTransform[] defenseRings;
    [SerializeField] private RectTransform[] dataPackets;
    [SerializeField] private Graphic[] securedNodes;
    [SerializeField] private TMP_Text networkStatusText;

    private readonly string[] networkMessages =
    {
        "NETWORK STATUS: ALL SAFENET SECTORS RESTORED",
        "DEFENSE STATUS: MALICIOUS PROCESSES CONTAINED",
        "TRAINING STATUS: CYBER AWARENESS PROFILE UPDATED",
        "SYSTEM STATUS: READY FOR THE NEXT MISSION"
    };

    private readonly List<Vector2> packetOrigins = new List<Vector2>();
    private readonly List<Color> nodeColors = new List<Color>();
    private float nextMessage;
    private int messageIndex;

    public void Configure(RectTransform core, RectTransform[] rings, RectTransform[] packets,
        Graphic[] nodes, TMP_Text status)
    {
        shieldCore = core;
        defenseRings = rings;
        dataPackets = packets;
        securedNodes = nodes;
        networkStatusText = status;
        CacheState();
    }

    private void Awake() => CacheState();

    private void CacheState()
    {
        packetOrigins.Clear();
        if (dataPackets != null)
            foreach (RectTransform packet in dataPackets)
                packetOrigins.Add(packet != null ? packet.anchoredPosition : Vector2.zero);

        nodeColors.Clear();
        if (securedNodes != null)
            foreach (Graphic node in securedNodes)
                nodeColors.Add(node != null ? node.color : Color.clear);

        nextMessage = Time.unscaledTime + 2.2f;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.8f);

        if (shieldCore != null)
        {
            float scale = Mathf.Lerp(0.96f, 1.045f, pulse);
            shieldCore.localScale = new Vector3(scale, scale, 1f);
        }

        for (int i = 0; defenseRings != null && i < defenseRings.Length; i++)
        {
            RectTransform ring = defenseRings[i];
            if (ring == null) continue;
            float direction = i % 2 == 0 ? 1f : -1f;
            ring.localEulerAngles = new Vector3(0f, 0f, now * (10f + i * 5f) * direction);
        }

        for (int i = 0; dataPackets != null && i < dataPackets.Length; i++)
        {
            RectTransform packet = dataPackets[i];
            if (packet == null || i >= packetOrigins.Count) continue;
            Vector2 origin = packetOrigins[i];
            float travel = Mathf.Repeat(now * (75f + i * 12f) + i * 135f, 1160f) - 580f;
            if ((i & 1) == 1) travel = -travel;
            packet.anchoredPosition = new Vector2(travel, origin.y);
        }

        for (int i = 0; securedNodes != null && i < securedNodes.Length; i++)
        {
            Graphic node = securedNodes[i];
            if (node == null || i >= nodeColors.Count) continue;
            Color color = nodeColors[i];
            float wave = 0.5f + 0.5f * Mathf.Sin(now * 3.5f - i * 0.8f);
            color.a *= Mathf.Lerp(0.55f, 1f, wave);
            node.color = color;
            node.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.93f, 1.08f, wave);
        }

        if (networkStatusText != null && now >= nextMessage)
        {
            messageIndex = (messageIndex + 1) % networkMessages.Length;
            networkStatusText.text = networkMessages[messageIndex];
            nextMessage = now + 2.6f;
        }
    }
}
