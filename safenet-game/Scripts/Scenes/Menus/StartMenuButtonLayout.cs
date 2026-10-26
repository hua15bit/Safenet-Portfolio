using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Applies the final Start Menu button artwork, positions, and click handlers
/// after the scene loads, including scenes saved before the latest UI rebuild.
/// </summary>
public class StartMenuButtonLayout : MonoBehaviour
{
    private static readonly Vector2 MenuButtonSize = new Vector2(330f, 60f);
    private const float MenuButtonX = 35f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Start Menu") return;
        MainMenu menu = FindAnyObjectByType<MainMenu>();
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        Button start = buttons.FirstOrDefault(item => item.name == "Play");
        Button settings = buttons.FirstOrDefault(item => item.name == "Settings");
        Button credits = buttons.FirstOrDefault(item => item.name == "Credits");
        Button exit = buttons.FirstOrDefault(item => item.name == "Exit");
        if (menu == null || start == null || settings == null || credits == null || exit == null)
            return;

        RemoveGeneratedStyle(start);
        RemoveGeneratedStyle(exit);
        RestoreSprite(start, "UI/StartOriginal");
        RestoreSprite(exit, "UI/ExitOriginal");

        Button signOut = buttons.FirstOrDefault(item =>
            item.name == "Sign Out" || item.name == "Logout");
        if (signOut == null)
            signOut = CreateSignOutButton(start.transform.parent);
        signOut.gameObject.name = "Sign Out";

        SafeNetPauseTheme theme = Resources.Load<SafeNetPauseTheme>("SafeNetPauseTheme");
        ApplySuppliedArtwork(signOut, theme != null ? theme.signOutButton : null);

        // Keep one centered, uniformly sized stack inside the phone border.
        // The 105-unit rhythm leaves equal visual space between all five controls.
        ApplyMenuButtonLayout(start, -10f);
        ApplyMenuButtonLayout(settings, -115f);
        ApplyMenuButtonLayout(credits, -220f);
        ApplyMenuButtonLayout(signOut, -325f);
        ApplyMenuButtonLayout(exit, -430f);

        start.onClick = new Button.ButtonClickedEvent();
        credits.onClick = new Button.ButtonClickedEvent();
        signOut.onClick = new Button.ButtonClickedEvent();
        exit.onClick = new Button.ButtonClickedEvent();
        start.onClick.AddListener(menu.PlayGame);
        credits.onClick.AddListener(menu.Credits);
        signOut.onClick.AddListener(menu.SignOut);
        exit.onClick.AddListener(menu.Exit);
    }

    private static Button CreateSignOutButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "Sign Out",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = MenuButtonSize;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.68f, 1f, .92f, 1f);
        colors.pressedColor = new Color(.31f, .75f, .92f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        return button;
    }

    private static void ApplyMenuButtonLayout(Button button, float y)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(MenuButtonX, y);
        rect.sizeDelta = MenuButtonSize;
        Image image = button.GetComponent<Image>();
        if (image != null) image.preserveAspect = true;
    }

    private static void ApplySuppliedArtwork(Button button, Sprite sprite)
    {
        if (button == null || sprite == null) return;
        RemoveGeneratedStyle(button);
        Image image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.preserveAspect = true;
    }

    private static void RemoveGeneratedStyle(Button button)
    {
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null && text.name == "Label") Destroy(text.gameObject);
        Outline outline = button.GetComponent<Outline>();
        if (outline != null) Destroy(outline);
        button.GetComponent<Image>().color = Color.white;
    }

    private static void RestoreSprite(Button button, string resource)
    {
        Sprite sprite = Resources.Load<Sprite>(resource);
        if (sprite != null) button.GetComponent<Image>().sprite = sprite;
    }
}
