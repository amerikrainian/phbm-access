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
    /// lines (its head, then one line per part and detail: an item's facts), the hero the control
    /// concerns and that hero's items (each node's side lines), and the battle log
    /// (<see cref="GameEvents"/>, newest first). Every buffer reads
    /// live on each keypress; a focus change re-homes review to the control's buffer and rewinds the
    /// control-fed ones, since a new control is new content. An empty buffer is skipped by the review
    /// keys; a log reviewed while events arrive keeps its cursor on the line it was on.
    /// </summary>
    internal static class Buffers
    {
        public static BufferManager Manager { get; private set; }
        public static BufferControls Controls { get; private set; }

        private static Buffer _ui;
        private static Buffer _hero;
        private static Buffer _items;
        private static ControlId _homed;

        /// <summary>The hero buffer: the hero the focused control concerns, whole
        /// (<see cref="Nodes.HeroLines.Lines"/>), through the node's <see cref="NodeVtable.SideLines"/>.</summary>
        public const string HeroKey = "hero";

        /// <summary>The items buffer: that hero's equipment and skills, a line per slot.</summary>
        public const string ItemsKey = "items";

        // A buffer fed by the focused node's side lines under its key.
        private static Buffer Side(string key, System.Func<string> label)
        {
            var buffer = Manager.Add(new Buffer(key, label));
            buffer.SetSource(() => NodeLines.SideLines(Navigation.FocusedNode, key));
            return buffer;
        }

        public static void Init()
        {
            Manager = new BufferManager();
            Controls = new BufferControls(Manager, (text, interrupt) => Speech.Say(text, interrupt: interrupt));

            _ui = Manager.Add(new Buffer(BufferKeys.Ui, () => Strings.BufferUi));
            _ui.SetSource(() => NodeLines.Lines(Navigation.FocusedNode));
            _hero = Side(HeroKey, () => Strings.BufferHero);
            _items = Side(ItemsKey, () => Strings.BufferItems);
            Log(BufferKeys.Events, () => Strings.BufferLog, GameEvents.Events);
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
            _hero.Reset();
            _items.Reset();
            Manager.SetCurrent(BufferKeys.Ui);
        }
    }
}
