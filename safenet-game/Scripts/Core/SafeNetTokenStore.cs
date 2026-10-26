using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Keeps the Android login token encrypted with a non-exportable Android
/// Keystore key. PlayerPrefs contains only the authenticated ciphertext and IV.
/// On other platforms, credentials remain in memory for this application run.
/// </summary>
internal static class SafeNetTokenStore
{
    private const string LegacyKey = "SafeNet.StudentToken";
    private const string EncryptedKey = "SafeNet.StudentToken.Encrypted.v1";
    private const string KeyAlias = "SafeNet.StudentSession.v1";
    private static string cachedToken;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMemory()
    {
        // Also reset between Editor play sessions with domain reload disabled.
        cachedToken = "";
        loaded = false;
    }

    public static string Read()
    {
        if (loaded) return cachedToken ?? "";
        loaded = true;
        cachedToken = "";
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            string encrypted = PlayerPrefs.GetString(EncryptedKey, "");
            if (!string.IsNullOrEmpty(encrypted))
            {
                string[] parts = encrypted.Split('.');
                if (parts.Length != 2) throw new FormatException();
                using AndroidJavaObject key = GetKey(false);
                using AndroidJavaClass cipherClass = new AndroidJavaClass("javax.crypto.Cipher");
                using AndroidJavaObject cipher = cipherClass.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding");
                using AndroidJavaObject spec = new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec", 128, Convert.FromBase64String(parts[0]));
                cipher.Call("init", 2, key, spec);
                cachedToken = Encoding.UTF8.GetString(cipher.Call<byte[]>("doFinal", Convert.FromBase64String(parts[1])));
            }
            else if (PlayerPrefs.HasKey(LegacyKey))
            {
                // One-time upgrade of a previously installed APK's session.
                Write(PlayerPrefs.GetString(LegacyKey, ""));
            }
        }
        catch (Exception)
        {
            // Reinstall/device restore/key invalidation requires a new login.
            // Never fall back to a plaintext credential or log the token.
            cachedToken = "";
            PlayerPrefs.DeleteKey(EncryptedKey);
        }
#endif
        PlayerPrefs.DeleteKey(LegacyKey);
        PlayerPrefs.Save();
        return cachedToken;
    }

    public static void Write(string token)
    {
        loaded = true;
        cachedToken = token ?? "";
        PlayerPrefs.DeleteKey(LegacyKey);
        PlayerPrefs.DeleteKey(EncryptedKey);
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(cachedToken))
        {
            try
            {
                using AndroidJavaObject key = GetKey(true);
                using AndroidJavaClass cipherClass = new AndroidJavaClass("javax.crypto.Cipher");
                using AndroidJavaObject cipher = cipherClass.CallStatic<AndroidJavaObject>("getInstance", "AES/GCM/NoPadding");
                cipher.Call("init", 1, key);
                byte[] ciphertext = cipher.Call<byte[]>("doFinal", Encoding.UTF8.GetBytes(cachedToken));
                byte[] iv = cipher.Call<byte[]>("getIV");
                PlayerPrefs.SetString(EncryptedKey, Convert.ToBase64String(iv) + "." + Convert.ToBase64String(ciphertext));
            }
            catch (Exception)
            {
                // Login still works for the current run if persistent storage
                // is unavailable. The next app launch will ask for login again.
                Debug.LogWarning("SafeNet could not securely remember this session. Sign in again after restarting the app.");
            }
        }
#endif
        PlayerPrefs.Save();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject GetKey(bool create)
    {
        using AndroidJavaClass keyStoreClass = new AndroidJavaClass("java.security.KeyStore");
        using AndroidJavaObject keyStore = keyStoreClass.CallStatic<AndroidJavaObject>("getInstance", "AndroidKeyStore");
        keyStore.Call("load", null, null);
        if (!keyStore.Call<bool>("containsAlias", KeyAlias))
        {
            if (!create) throw new InvalidOperationException("Session key unavailable.");
            using AndroidJavaObject builder = new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder", KeyAlias, 3);
            using AndroidJavaObject blockMode = builder.Call<AndroidJavaObject>("setBlockModes", (object)new string[] { "GCM" });
            using AndroidJavaObject padding = builder.Call<AndroidJavaObject>("setEncryptionPaddings", (object)new string[] { "NoPadding" });
            using AndroidJavaObject spec = builder.Call<AndroidJavaObject>("build");
            using AndroidJavaClass generatorClass = new AndroidJavaClass("javax.crypto.KeyGenerator");
            using AndroidJavaObject generator = generatorClass.CallStatic<AndroidJavaObject>("getInstance", "AES", "AndroidKeyStore");
            generator.Call("init", spec);
            using AndroidJavaObject generatedKey = generator.Call<AndroidJavaObject>("generateKey");
        }
        return keyStore.Call<AndroidJavaObject>("getKey", KeyAlias, null);
    }
#endif
}
