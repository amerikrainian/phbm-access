using System.Collections.Generic;
using HarmonyLib;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Battle;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;
using Screen = PixelHeroesAccess.Core.Screens.Screen;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The reward screen (<see cref="LootScreen"/>, drawn over a won fight, an overland event, a
    /// treasure room and a finished quest in town). The game grants everything the moment the loot is
    /// made (<see cref="LootScreen.CreateLootForRoom"/> and kin: the experience to the living heroes,
    /// the gold, the items straight into the backpack, the potions) and the screen then only shows it,
    /// item by item, its queue emptying as it goes; so what was won is taken as it is made
    /// (<see cref="Loot.Last"/>, also kept in the log) and said whole as the screen comes up:
    /// "experience +50, gold +35, found: Fine Sword of Fire, Small Healing Potion 1". The found items
    /// are listed with their facts; Continue (and Escape) is the game's skip
    /// (<see cref="LootScreen.EventSkipLoot"/>), which a normal room's reward obeys at once and the
    /// others once their items have shown.
    /// </summary>
    public sealed class LootScreenScreen : Screen
    {
        public override string Key => "game.loot";
        public override int Layer => 9;

        /// <summary>The scene's reward screen, whichever scene it is.</summary>
        public static LootScreen Current()
        {
            var screen = ScreenBase.pCurrentInstance;
            if (screen is BattleLogic battle) return battle.v_BattleGUI != null ? battle.v_BattleGUI.v_Lootscreen : null;
            if (screen is Overland overland) return overland.v_Lootscreen;
            if (screen is Town town) return town.v_Lootscreen;
            if (screen is RoomTreasure treasure) return treasure.v_Lootscreen;
            return null;
        }

        public override bool IsActive()
        {
            var loot = Current();
            return loot != null && loot.IsActive() && !loot.IsFinished() && Popup.GetTopMostActivePopup() == null;
        }

        public override void OnPush()
        {
            var loot = Current();
            if (loot != null && Loot.Last != null && ReferenceEquals(Loot.LastScreen, loot))
                ScreenIntro.Say(Loot.Last.Summary);
        }

        public override void Build(GraphBuilder b)
        {
            var loot = Current();
            if (loot == null) return;
            b.PushContext(null, null);
            var won = ReferenceEquals(Loot.LastScreen, loot) ? Loot.Last : null;
            if (won != null)
            {
                b.AddItem(ControlId.Structural("loot:summary"), GameNodes.Text(() => won.Summary));
                foreach (var found in won.Items)
                {
                    if (found == null) continue;
                    var item = found;
                    b.AddItem(ControlId.ForObject(item), HeroNodes.Item(() => item, null, reckon: FirstHero));
                }
            }
            string congratulations = GameText.Of(loot.v_TextmeshCongratulations);
            if (congratulations != null) b.AddItem(ControlId.Structural("loot:congratulations"), GameNodes.Text(() => GameText.Of(Current()?.v_TextmeshCongratulations)));
            b.AddItem(ControlId.Structural("loot:continue"), GameNodes.Button(() => Strings.LootContinue, () => Current()?.EventSkipLoot()));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Current()?.EventSkipLoot());
        }

        private static CharacterHero FirstHero()
        {
            var screen = ScreenBase.pCurrentInstance;
            if (screen == null || screen.pHeroes == null) return null;
            foreach (var hero in screen.pHeroes) if (hero != null && hero.IsAlive()) return hero;
            return null;
        }
    }

    /// <summary>
    /// What a reward screen was given, taken the moment the game makes it: the experience, the gold,
    /// the items, the potions (the screen's own potion counts, which the game fills as it makes them).
    /// Event data, like a log line: the screen shows the same and then forgets it.
    /// </summary>
    public sealed class Loot
    {
        public string Summary;
        public List<Item> Items = new List<Item>();

        /// <summary>The latest loot and the screen it was made on.</summary>
        public static Loot Last { get; private set; }
        public static LootScreen LastScreen { get; private set; }

        private static void Take(LootScreen screen)
        {
            var loot = new Loot();
            if (screen.mGainedItems != null) loot.Items.AddRange(screen.mGainedItems);
            var parts = new List<string>();
            if (screen.mGainedEXP > 0) parts.Add(Strings.LootExperience(screen.mGainedEXP.ToString()));
            if (screen.mGainedGold > 0) parts.Add(Strings.LootGold(screen.mGainedGold.ToString()));
            var found = new List<string>();
            foreach (var item in loot.Items) if (item != null) found.Add(ItemLines.Name(item));
            if (screen.mLootedPotions && screen.v_Textmeshs_Potions != null)
            {
                if (screen.v_Textmeshs_Potions.Length > 0) Potion(found, GameText.Tr(ET.Potion_Name_Small), screen.v_Textmeshs_Potions[0]);
                if (screen.v_Textmeshs_Potions.Length > 1) Potion(found, GameText.Tr(ET.Potion_Name_Big), screen.v_Textmeshs_Potions[1]);
            }
            if (found.Count > 0) parts.Add(Strings.LootFound(string.Join(", ", found)));
            loot.Summary = parts.Count > 0 ? string.Join(", ", parts) : Strings.LootNothing;
            Last = loot;
            LastScreen = screen;
            Core.Events.GameEvents.Events.Add(loot.Summary);
        }

        private static void Potion(List<string> found, string name, tk2dTextMesh count)
        {
            if (count == null || !int.TryParse(count.text, out int n) || n <= 0) return;
            found.Add(name + " " + n);
        }

        [HarmonyPatch(typeof(LootScreen), nameof(LootScreen.CreateLootForRoom))]
        private static class RoomLoot
        {
            private static void Postfix(LootScreen __instance) => Take(__instance);
        }

        [HarmonyPatch(typeof(LootScreen), nameof(LootScreen.CreateLootForOverlandEvent))]
        private static class EventLoot
        {
            private static void Postfix(LootScreen __instance) => Take(__instance);
        }

        [HarmonyPatch(typeof(LootScreen), nameof(LootScreen.CreateLootItem))]
        private static class QuestLoot
        {
            private static void Postfix(LootScreen __instance) => Take(__instance);
        }

        /// <summary>Module teardown.</summary>
        public static void Shutdown()
        {
            Last = null;
            LastScreen = null;
        }
    }
}
