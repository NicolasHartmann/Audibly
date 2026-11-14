namespace Audibly.ReadAlong;

/// <summary>
/// Chooses the word to highlight for each rendered frame.
/// The UI samples the playback position once per frame, and frames can be 60-130 ms apart.
/// Short words ("des", "in", "er") last only 100-200 ms of audio, less at higher speeds, so
/// plain sampling jumps over them. The follower shows a word that was missed between two
/// frames on the next frame, one word per frame, and jumps directly after a seek.
/// </summary>
public sealed class NarrationFollower
{
    // More words behind than this, or further behind in time, is a seek rather than a missed frame.
    private const int MaxCatchUpWords = 4;
    private const double MaxCatchUpSeconds = 1.5;

    private AlignmentMap? _map;
    private int _source = -1;
    private int _shown = -1;

    public (Cue Cue, Passage Passage)? Next(AlignmentMap map, int sourceIndex, double seconds)
    {
        var target = map.IndexAt(sourceIndex, seconds);
        if (target < 0) { Reset(map, sourceIndex, -1); return null; }

        var continuous = ReferenceEquals(map, _map) && sourceIndex == _source && _shown >= 0 &&
                         target > _shown && target - _shown <= MaxCatchUpWords &&
                         seconds - map.At(sourceIndex, _shown + 1)!.Value.Cue.EndSeconds <= MaxCatchUpSeconds;
        if (continuous)
        {
            // Step through the missed words, one per frame. A word that has already ended is
            // still shown for this one frame, even when a pause follows it.
            _shown++;
            return map.At(sourceIndex, _shown);
        }
        else Reset(map, sourceIndex, target);

        var current = map.At(sourceIndex, target)!.Value;
        // In the pause after a word nothing is being spoken, so nothing is highlighted.
        return seconds < current.Cue.EndSeconds ? current : null;
    }

    private void Reset(AlignmentMap map, int sourceIndex, int shown)
    {
        _map = map; _source = sourceIndex; _shown = shown;
    }
}
