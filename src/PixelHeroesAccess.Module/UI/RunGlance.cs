using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.Screens;

namespace PixelHeroesAccess.Module.UI
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// Read keys for any screen of a run, focus unmoved: the party's health (each hero "Ghada, HP 30
    /// of 45", its statuses in a fight), the gold, the quest under way and its dungeon. Each is a line
    /// that is null where it has nothing to say (outside a run), so the key help lists it only where it
    /// speaks (<see cref="Has"/>).
    /// </summary>
    internal static class RunGlance
    {
        public static bool InRun => GameCore.IsGameLoaded() && ScreenBase.pCurrentInstance != null && ScreenBase.pCurrentInstance.pHeroes != null;

        public static string Party()
        {
            if (!InRun) return null;
            var parts = new List<string>();
            foreach (var hero in ScreenBase.pCurrentInstance.pHeroes)
            {
                if (hero == null) continue;
                parts.Add(BattleScreen.HeroLabel(hero));
            }
            return parts.Count == 0 ? null : string.Join(". ", parts);
        }

        public static string Gold() => InRun ? TavernScreen.GoldLine() : null;

        public static string Quest() => InRun && !GameCore.IsArenaMode() ? TownScreen.QuestLine() : null;

        public static bool Has(System.Func<string> line)
        {
            try { return !string.IsNullOrWhiteSpace(line()); }
            catch (System.Exception e) { CoreLog.Warning("RunGlance: " + e.Message); return false; }
        }

        public static void Say(System.Func<string> line)
        {
            string text = null;
            try { text = line(); }
            catch (System.Exception e) { CoreLog.Warning("RunGlance: " + e.Message); }
            if (!string.IsNullOrWhiteSpace(text)) Speech.Say(text, interrupt: true);
        }
    }
}
