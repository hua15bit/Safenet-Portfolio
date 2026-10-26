/// <summary>
/// Shared values that must remain consistent across gameplay, UI, and backend progress data.
/// </summary>
public static class SafeNetConstants
{
    /// <summary>Number of graded missions, excluding the tutorial.</summary>
    public const int GradedMissionCount = 6;

    /// <summary>Wrong-answer capacity shared by every mission in one run.</summary>
    public const int MaximumSystemIntegrity = 3;
}
