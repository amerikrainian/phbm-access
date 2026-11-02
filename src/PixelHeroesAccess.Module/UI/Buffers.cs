using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Buffers;
using PixelHeroesAccess.Core.Events;
using Buffer = PixelHeroesAccess.Core.Buffers.Buffer;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;

namespace PixelHeroesAccess.Module.UI
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The mod's buffer roster and its review keys (Ctrl plus arrows), the Harkest Dungeon pattern:
    /// review lists for the information a focus announcement leaves out: the focused control's own
    /// lines (its head, then one line per part and tooltip: a unit's or a card's details), the game's
    /// event history and the units' chatter (<see cref="GameEvents"/>, newest first). Every buffer reads
    /// live on each keypress; a focus change re-homes review to the control's buffer and rewinds the
    /// control-fed ones, since a new control is new content. An empty buffer is skipped by the review
    /// keys; a log reviewed while events arrive keeps its cursor on the line it was on.
    /// </summary>
    internal static class Buffers
    {
        public static BufferManager Manager { get; private set; }
        public static BufferControls Controls { get; private set; }

        private static Buffer _ui;
        private static ControlId _homed;

        public static void Init()
        {
            Manager = new BufferManager();
            Controls = new BufferControls(Manager, (text, interrupt) => Speech.Say(text, interrupt: interrupt));

            _ui = Manager.Add(new Buffer(BufferKeys.Ui, () => Strings.BufferUi));
            _ui.SetSource(() => NodeLines.Lines(Navigation.FocusedNode));
            Log(BufferKeys.Events, () => Strings.BufferEvents, GameEvents.Events);
            Log(BufferKeys.Quotes, () => Strings.BufferQuotes, GameEvents.Quotes);
        }

        private static void Log(string key, System.Func<string> label, EventLog log)
        {
            var buffer = Manager.Add(new Buffer(key, label));
            buffer.SetSource(() => log.Lines);
            log.Added += buffer.LinesInserted;
        }

        /// <summary>Per frame: a focus change (by key or by the game moving things) re-homes review to
        /// the control's own buffer and rewinds the control-fed buffers.</summary>
        public static void Tick()
        {
            if (Manager == null) return;
            var id = Navigation.FocusedNode?.Id;
            if (Equals(id, _homed)) return;
            _homed = id;
            _ui.Reset();
            Manager.SetCurrent(BufferKeys.Ui);
        }
    }
}
