using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and controls the full-screen credits panel used by the Start Menu.
/// The panel closes only through its visible Close button. The full-screen
/// dimmer intentionally absorbs outside taps without dismissing the credits.
/// </summary>
public class MobileCreditsPanel : MonoBehaviour
{
    private GameObject overlay;

    public void Show()
    {
        if (overlay == null) Build();
        overlay.SetActive(true);
    }

    public void Hide()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    private void Build()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        overlay = CreateUi("Mobile Credits Overlay", canvas.transform, typeof(Image));
        Stretch(overlay.GetComponent<RectTransform>());
        Image dimmer = overlay.GetComponent<Image>();
        dimmer.color = new Color(0, .015f, .04f, .92f);
        dimmer.raycastTarget = true;
        overlay.transform.SetAsLastSibling();

        GameObject phone = CreateUi("Credits Phone Panel", overlay.transform, typeof(Image), typeof(Outline));
        RectTransform phoneRect = phone.GetComponent<RectTransform>();
        phoneRect.anchorMin = new Vector2(.5f, .5f);
        phoneRect.anchorMax = new Vector2(.5f, .5f);
        phoneRect.pivot = new Vector2(.5f, .5f);
        phoneRect.sizeDelta = new Vector2(620, 1040);
        phone.GetComponent<Image>().color = new Color(.025f, .075f, .13f, .98f);
        Outline outline = phone.GetComponent<Outline>();
        outline.effectColor = new Color(.22f, .9f, 1f, .8f);
        outline.effectDistance = new Vector2(3, -3);

        GameObject header = CreateText("Header", phone.transform, "CREDITS TERMINAL", 34, FontStyles.Bold);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(.5f, 1);
        headerRect.anchoredPosition = new Vector2(0, -48);
        headerRect.sizeDelta = new Vector2(-90, 60);
        header.GetComponent<TextMeshProUGUI>().color = new Color(.35f, .95f, 1f);

        string copy =
            "<b>SafeNet</b>\nGamified 2D Cybersecurity Awareness\n\n" +
            "<color=#59F1FF><b>DEVELOPED BY</b></color>\n" +
            "Bautista, Joshua Angelo S.\n" +
            "Lazaro, Calvin Kyle A.\n" +
            "Majait, Cristan Rush G.\n" +
            "Rumuar, Hero Jhon C.\n" +
            "Villacarlos, Jake Rei Inri G.\n\n" +
            "<color=#59F1FF><b>CAPSTONE ADVISER</b></color>\n" +
            "John Clement Escobañez\n\n" +
            "<color=#59F1FF><b>INSTITUTION</b></color>\n" +
            "National University\n" +
            "College of Computing and\nInformation Technologies";

        GameObject body = CreateText("Credits Content", phone.transform, copy, 27, FontStyles.Normal);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0, 0);
        bodyRect.anchorMax = new Vector2(1, 1);
        bodyRect.offsetMin = new Vector2(58, 112);
        bodyRect.offsetMax = new Vector2(-58, -125);
        TextMeshProUGUI bodyText = body.GetComponent<TextMeshProUGUI>();
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.color = new Color(.88f, .96f, 1f);
        bodyText.lineSpacing = 8;

        GameObject close = CreateUi("Close Credits", phone.transform, typeof(Image), typeof(Button));
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, 0);
        closeRect.pivot = new Vector2(.5f, 0);
        closeRect.anchoredPosition = new Vector2(0, 28);
        closeRect.sizeDelta = new Vector2(300, 68);
        close.GetComponent<Image>().color = new Color(.04f, .3f, .42f, 1f);
        close.GetComponent<Button>().onClick.AddListener(Hide);

        GameObject closeLabel = CreateText("Label", close.transform, "CLOSE", 25, FontStyles.Bold);
        Stretch(closeLabel.GetComponent<RectTransform>());
        closeLabel.GetComponent<TextMeshProUGUI>().color = Color.white;
    }

    private static GameObject CreateUi(string name, Transform parent, params System.Type[] components)
    {
        GameObject item = new GameObject(name, components);
        item.transform.SetParent(parent, false);
        return item;
    }

    private static GameObject CreateText(string name, Transform parent, string value, float size, FontStyles style)
    {
        GameObject item = CreateUi(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        return item;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
