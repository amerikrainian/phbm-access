using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The progress screen (<see cref="ScreenProgress"/>, the title menu's Achievements): three tabs,
    /// Story, Heroes, Achievements (<see cref="TabView"/>). The game draws them as scrolling grids of
    /// portraits and icons and enables only what is scrolled into view; here each is a list read from
    /// the player's progress (<see cref="PlayerProgress"/>), whatever is scrolled. Story: each
    /// campaign's name with its victories and defeats ("locked" until the one before is won), then each
    /// townsperson's level. Heroes: the game's count line, then each hero class, locked or with its
    /// victories and defeats. Achievements: each by name, unlocked or locked, its description, the
    /// count so far of a counting one, the hero it unlocks. Then Exit.
    /// </summary>
    public sealed class ProgressScreen : SceneScreen<ScreenProgress>
    {
        public override string Key => "game.progress";

        public override void Build(GraphBuilder b)
        {
            var progress = Game;
            var tabs = progress != null ? progress.v_TabView : null;
            if (progress == null || tabs == null) return;
            b.PushContext(null, null);
            b.StartRow("progress:tabs");
            for (int i = 0; i < tabs.v_Buttons.Length; i++)
            {
                int index = i;
                var button = tabs.v_Buttons[i];
                if (!GameNodes.IsShown(button)) continue;
                b.AddItem(ControlId.Structural("progress:tab:" + index), GameNodes.Tab(button, () => tabs.GetCurrentTabIndex() == index));
            }
            b.EndRow();
            int current = tabs.GetCurrentTabIndex();
            var lines = current == 0 ? Story(progress) : current == 1 ? Heroes(progress) : Achievements();
            for (int l = 0; l < lines.Count; l++)
            {
                string line = lines[l];
                b.AddItem(ControlId.Structural("progress:" + current + ":" + l), GameNodes.Text(() => line));
            }
            foreach (var button in GameScreens.LiveButtons(progress.transform))
            {
                if (System.Array.IndexOf(tabs.v_Buttons, button) >= 0 || button.GetComponentInParent<Scrollbar>() != null) continue;
                if (button.transform.IsChildOf(progress.v_ContainerAchievements)) continue;
                b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            }
            b.PopContext();
        }

        private static List<string> Story(ScreenProgress progress)
        {
            var lines = new List<string>();
            for (int i = 0; i < progress.v_Campaigns.Length; i++)
            {
                var row = progress.v_Campaigns[i];
                if (row == null) continue;
                var campaign = (GameSession.eCampaigns)i;
                string name = GameText.Of(row.GetAText(0));
                if (!PlayerProgress.IsCampaignUnlocked(campaign)) { lines.Add(name + ", " + Strings.StateLocked); continue; }
                var counts = PlayerProgress.GetCampaign(campaign);
                lines.Add(name + ", " + Strings.ProgressCounts(counts.GetCountVictory().ToString(), counts.GetCountDefeat().ToString()));
            }
            for (int i = 0; i < PlayerProgress.GetNPCCount(); i++)
            {
                var npc = PlayerProgress.GetNPC(i);
                if (npc == null) continue;
                lines.Add(GameText.Line(DataNPCs.GetData(npc.GetNPCID()).pIngameName) + ", " + Strings.ItemPair(GameText.Tr(ET.Inventory_Hero_Level), npc.GetLevel().ToString()));
            }
            return lines;
        }

        private static List<string> Heroes(ScreenProgress progress)
        {
            var lines = new List<string>();
            string title = GameText.Of(progress.v_TextMesh_HeroesTitle);
            if (title != null) lines.Add(title);
            for (int i = 0; i < (int)DataHeroes.eHeroesClasses.COUNT; i++)
            {
                var cls = (DataHeroes.eHeroesClasses)i;
                var data = DataHeroes.GetData(cls);
                if (data == null) continue;
                string name = GameText.Line(data.pNameClass);
                if (!PlayerProgress.IsHeroUnlocked(cls)) { lines.Add(name + ", " + Strings.StateLocked); continue; }
                var counts = PlayerProgress.GetHero(cls);
                lines.Add(counts != null ? name + ", " + Strings.ProgressCounts(counts.GetCountVictory().ToString(), counts.GetCountDefeat().ToString()) : name);
            }
            return lines;
        }

        private static List<string> Achievements()
        {
            var lines = new List<string>();
            for (int i = 0; i < PlayerProgress.GetAchievementCount(); i++)
            {
                var a = PlayerProgress.GetAchievementByIndex(i);
                if (a == null) continue;
                var parts = new List<string> { GameText.Line(a.GetName()), a.IsUnlocked() ? Strings.StateUnlocked : Strings.StateLocked, GameText.Line(a.GetDescription()) };
                if (a.IsCumulative() && !a.IsUnlocked())
                    parts.Add(Strings.ProgressOf(a.GetCumulativeValueCurrent().ToString(), a.GetCumulativeValueTotal().ToString()));
                if (a.IsUnlockingHero())
                    parts.Add(GameText.Tr(ET.Achievement_UnlocksHero) + ": " + GameText.Line(DataHeroes.GetData(a.GetUnlockedHero()).pNameClass));
                lines.Add(string.Join(", ", parts));
            }
            return lines;
        }
    }
}
