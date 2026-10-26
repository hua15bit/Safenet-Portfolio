using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Centralizes optional keyboard and gamepad input for Editor/desktop testing.
/// Mobile gameplay continues to use SafeNetMobileInput's large on-screen
/// buttons. Keeping all desktop reads on Unity's Input System lets Android use
/// one supported input backend instead of the unsupported "Both" setting.
/// </summary>
public static class SafeNetDesktopInput
{
    public static bool PausePressed => WasPressed(Keyboard.current?.escapeKey);

    public static bool SubmitPressed =>
        WasPressed(Keyboard.current?.enterKey) ||
        WasPressed(Keyboard.current?.numpadEnterKey);

    public static bool ContinuePressed =>
        SubmitPressed || WasPressed(Keyboard.current?.spaceKey);

    public static bool SkipPressed =>
        PausePressed || WasPressed(Keyboard.current?.spaceKey);

    public static bool InteractPressed => WasPressed(Keyboard.current?.eKey);

    public static float Horizontal
    {
        get
        {
            Keyboard keyboard = Keyboard.current;
            float keyboardValue = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                    keyboardValue -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                    keyboardValue += 1f;
            }

            float gamepadValue = Gamepad.current != null
                ? Gamepad.current.leftStick.x.ReadValue()
                : 0f;
            return Mathf.Abs(gamepadValue) > Mathf.Abs(keyboardValue)
                ? Mathf.Clamp(gamepadValue, -1f, 1f)
                : Mathf.Clamp(keyboardValue, -1f, 1f);
        }
    }

    private static bool WasPressed(ButtonControl button)
    {
        return button != null && button.wasPressedThisFrame;
    }
}
