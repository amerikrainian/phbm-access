using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The quest log (<see cref="PopupQuestLog"/>, the menus' Quest Log button): three tabs, Quest Log,
    /// Heroes, Statistics (<see cref="TabView"/>; the arena has no quest log). The quest log is the
    /// campaign's line, then each quest played so far from the session ("#1 Byting Woods, Cheat Mode",
    /// the one under way marked as current, as the game's blinking marker does); the heroes are each
    /// hero's statistics; the statistics are the run's (<see cref="StatsLines"/>).
    /// </summary>
    public sealed class QuestLogScreen : PopupScreen
    {
        public override string Key => "game.questlog";

        private static PopupQuestLog Log => ScreenBase.pCurrentInstance != null ? ScreenBase.pCurrentInstance.v_Popup_QuestLog as PopupQuestLog : null;

        protected override Popup Resolve() => Log;

        public override void Build(GraphBuilder b)
        {
            var log = Log;
            var tabs = log != null ? log.GetComponent<TabView>() : null;
            if (log == null || tabs == null) return;
            b.PushContext(null, null);
            b.StartRow("questlog:tabs");
            for (int i = 0; i < tabs.v_Buttons.Length; i++)
            {
                int index = i;
                var button = tabs.v_Buttons[i];
                if (!GameNodes.IsShown(button)) continue;
                b.AddItem(ControlId.Structural("questlog:tab:" + index), GameNodes.Tab(button, () => tabs.GetCurrentTabIndex() == index));
            }
            b.EndRow();
            int current = tabs.GetCurrentTabIndex();
            var lines = new List<string>();
            if (current == 0) lines.AddRange(Quests(log));
            else if (current == 1)
            {
                for (int h = 0; h < 3; h++)
                {
                    var display = log.v_Container_Heroes != null ? log.v_Container_Heroes.Get<HeroStatisticsDisplay>(h) : null;
                    var hero = StatsLines.Hero(display);
                    if (hero.Count > 0) lines.Add(string.Join(". ", hero));
                }
            }
            else lines.AddRange(StatsLines.Run(log.v_Container_Statistics));
            for (int l = 0; l < lines.Count; l++)
            {
                string line = lines[l];
                b.AddItem(ControlId.Structural("questlog:" + current + ":" + l), GameNodes.Text(() => line));
            }
            foreach (var button in GameScreens.LiveButtons(log.transform))
                if (System.Array.IndexOf(tabs.v_Buttons, button) < 0) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            b.PopContext();
        }

        private static List<string> Quests(PopupQuestLog log)
        {
            var lines = new List<string>();
            string header = log.v_Container_QuestLog != null ? GameText.Of(log.v_Container_QuestLog.GetAText(0)) : null;
            if (header != null) lines.Add(header);
            var session = GameCore.pGameSession;
            if (session == null || session.pPlayedQuests == null) return lines;
            for (int i = 0; i < session.pPlayedQuests.Count; i++)
            {
                var quest = session.pPlayedQuests[i];
                string line = "#" + (i + 1) + " " + GameText.Line(DataDungeons.GetDungeonNameFromID(quest.pDungeon)) + ", " + GameText.Line(quest.GetData().pQuestName);
                if (i == session.pPlayedQuests.Count - 1 && session.pNPCCurrentlyActiveQuest != null) line += ", " + Strings.StateCurrent;
                lines.Add(line);
            }
            return lines;
        }
    }
}
