using UnityEngine;

namespace VerdantBlade
{
    /// <summary>Tracks active play time only; menus and interruption pauses never affect records.</summary>
    public sealed class RunClock
    {
        public float ElapsedSeconds { get; private set; }

        public void Reset(float seconds = 0f)
        {
            ElapsedSeconds = Mathf.Max(0f, seconds);
        }

        public void Tick(float unscaledDeltaSeconds, bool isPlaying)
        {
            if (!isPlaying || unscaledDeltaSeconds <= 0f)
            {
                return;
            }
            ElapsedSeconds += unscaledDeltaSeconds;
        }
    }
}
