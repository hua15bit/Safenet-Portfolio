/// <summary>
/// Stores the current state of the on-screen movement controls. Interaction is
/// a one-shot input that is cleared as soon as PlayerMovement consumes it.
/// </summary>
public static class SafeNetMobileInput
{
    private static bool leftHeld;
    private static bool rightHeld;
    private static bool interactPressed;

    public static float Horizontal
    {
        get
        {
            if (leftHeld == rightHeld) return 0f;
            return leftHeld ? -1f : 1f;
        }
    }

    public static void SetLeft(bool held)
    {
        leftHeld = held;
    }

    public static void SetRight(bool held)
    {
        rightHeld = held;
    }

    public static void PressInteract()
    {
        interactPressed = true;
    }

    public static bool ConsumeInteractPressed()
    {
        if (!interactPressed) return false;
        interactPressed = false;
        return true;
    }

    public static void Reset()
    {
        leftHeld = false;
        rightHeld = false;
        interactPressed = false;
    }
}
