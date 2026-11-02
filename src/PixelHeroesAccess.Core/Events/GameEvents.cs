using System;
using System.Collections.Generic;

namespace PixelHeroesAccess.Core.Events
{
    /// <summary>
    /// A log of event lines, newest first (the history the events buffer reviews). Bounded: the oldest
    /// lines fall off past <see cref="Capacity"/>. <see cref="Added"/> tells a reviewing buffer how many
    /// lines went in on top, so its cursor stays on the line it was on.
    /// </summary>
    public sealed class EventLog
    {
        public const int Capacity = 500;
        private readonly List<string> _lines = new List<string>();

        public IReadOnlyList<string> Lines => _lines;

        /// <summary>Raised with the number of lines put on top.</summary>
        public event Action<int> Added;

        /// <summary>One event's text; a multi-line text goes in as its lines in their own order.</summary>
        public void Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var parts = new List<string>();
            foreach (var line in text.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) parts.Add(line.Trim());
            if (parts.Count == 0) return;
            _lines.InsertRange(0, parts);
            if (_lines.Count > Capacity) _lines.RemoveRange(Capacity, _lines.Count - Capacity);
            Added?.Invoke(parts.Count);
        }

        public void Clear() => _lines.Clear();
    }

    /// <summary>
    /// The mod's log of what happened (a fight's actions, the rewards), newest first, which the log
    /// buffer reviews. Lines go in as they are spoken.
    /// </summary>
    public static class GameEvents
    {
        public static EventLog Events { get; } = new EventLog();
    }
}
