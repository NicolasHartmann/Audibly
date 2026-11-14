using Audibly.Core;

namespace Audibly.Mobile.Services;

/// <summary>The listener's choices for the player's progress bar, kept across restarts.</summary>
public static class PlayerSettings
{
    public static event Action? Changed;

    public static PlayerProgressScope ProgressScope
    {
        get => PlayerProgress.Parse(Preferences.Default.Get(nameof(ProgressScope), 0), PlayerProgressScope.Chapter);
        set
        {
            Preferences.Default.Set(nameof(ProgressScope), (int)value);
            Changed?.Invoke();
        }
    }

    public static PlayerTimeDisplay TimeDisplay
    {
        get => PlayerProgress.Parse(Preferences.Default.Get(nameof(TimeDisplay), 0), PlayerTimeDisplay.ElapsedAndTotal);
        set
        {
            Preferences.Default.Set(nameof(TimeDisplay), (int)value);
            Changed?.Invoke();
        }
    }
}
