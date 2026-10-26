using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles the Start Menu actions, including a sign-out path that returns to
/// Login without closing the Android application.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadSceneAsync("Story Cinematic");
    }

    public void Credits()
    {
        MobileCreditsPanel credits = GetComponent<MobileCreditsPanel>();
        if (credits == null) credits = gameObject.AddComponent<MobileCreditsPanel>();
        credits.Show();
    }

    public void Exit()
    {
        SafeNetApiClient api = SafeNetApiClient.Instance;
        if (api != null) api.LogoutAndExit();
        else SafeNetApplication.Quit();
    }

    public void SignOut()
    {
        SafeNetApiClient api = SafeNetApiClient.Instance;
        if (api != null) api.Logout();
        else SceneManager.LoadScene("Login");
    }
}
