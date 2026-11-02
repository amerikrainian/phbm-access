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
    /// The graveyard (<see cref="Graveyard"/>): a tombstone per finished campaign, newest first, which
    /// the game shows one at a time by scrolling a strip and clicking. Here the gravedigger's count,
    /// then a control per tombstone, "Defeat, 02.11.2026, 1 (Hard) "Fools of Radiance"" from the run's
    /// own record (<see cref="Tombstone"/>); landing on one selects it as its click does
    /// (<see cref="Graveyard.EventSelectTombstone"/>, which works for one scrolled out of view), and its
    /// panel, read back from the game's displays, is the control buffer: the campaign, the dungeons
    /// and the people whose quests were done, each hero's statistics, the run's. Then Exit.
    /// </summary>
    public sealed class GraveyardScreen : SceneScreen<Graveyard>
    {
        public override string Key => "game.graveyard";

        public override void Build(GraphBuilder b)
        {
            var yard = Game;
            if (yard == null || yard.mTombstoneButtons == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("grave:count"), GameNodes.Text(() => GameText.Of(Game?.v_SpeechBubbleGravedigger)));
            foreach (var stone in yard.mTombstoneButtons)
            {
                if (stone == null) continue;
                var button = stone;
                var record = Record(yard, button);
                if (record == null) continue;
                var vt = GameNodes.Button(() => Label(record), () => Game?.EventSelectTombstone(button));
                vt.OnFocus = () => Game?.EventSelectTombstone(button);
                vt.Details = () => Panel(Game);
                b.AddItem(ControlId.ForObject(button), vt);
            }
            foreach (var button in GameScreens.LiveButtons(yard.transform))
            {
                if (yard.mTombstoneButtons.Contains(button) || GameScreens.IsFigure(button)) continue;
                if (button.GetComponentInParent<Scrollbar>() != null) continue;
                b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            }
            b.PopContext();
        }

        // The tombstone a button stands for: its parent is named "Tombstone_<index into the list>".
        private static Tombstone Record(Graveyard yard, tk2dButton button)
        {
            string name = button.transform.parent != null ? button.transform.parent.name : button.name;
            int cut = name.LastIndexOf('_');
            if (cut < 0 || !int.TryParse(name.Substring(cut + 1), out int index)) return null;
            return yard.mTombstones != null && index >= 0 && index < yard.mTombstones.Count ? yard.mTombstones[index] : null;
        }

        /// <summary>"Defeat, 02.11.2026, 1 (Hard) "Fools of Radiance"" (the arena's: its rounds).</summary>
        public static string Label(Tombstone stone)
        {
            ET tag;
            switch (stone.pVictoryTag)
            {
                case GameSession.eVictoryTags.Victory: tag = ET.Graveyard_Tombstone_Victory; break;
                case GameSession.eVictoryTags.Abandoned: tag = ET.Graveyard_Tombstone_Abandoned; break;
                case GameSession.eVictoryTags.Arena: tag = ET.Graveyard_Tombstone_Arena; break;
                default: tag = ET.Graveyard_Tombstone_Defeat; break;
            }
            var parts = new List<string> { GameText.Tr(tag), GameText.Line(Globals.FormatDate(stone.pDateOfPlay)) };
            if (stone.pVictoryTag == GameSession.eVictoryTags.Arena)
                parts.Add(stone.pArenaFinishedEnemyGroups.Count + " " + GameText.Tr(ET.Graveyard_Stat_ArenaRounds));
            else
                parts.Add((1 + (int)stone.pCampaign) + " (" + GameText.Tr((ET)((int)ET.Campaign_Difficulty_1 + (int)stone.pCampaign)) + ") \""
                    + GameText.Line(DataNPCs.GetNPCWithTag(DataNPCs.pTAG_CAMPAIGN + (1 + (int)stone.pCampaign)).GetFinalQuest().pQuestName) + "\"");
            return string.Join(", ", parts);
        }

        // The selected tombstone's panel as the game draws it.
        private static IEnumerable<string> Panel(Graveyard yard)
        {
            var lines = new List<string>();
            if (yard == null) return lines;
            Add(lines, GameText.Of(yard.v_TextCampaign));
            Columns(lines, yard.v_TextsDungeonsColumns);
            Columns(lines, yard.v_TextsNPCsColumns);
            if (yard.v_Heroes != null)
                foreach (var hero in yard.v_Heroes)
                {
                    var stats = StatsLines.Hero(hero);
                    if (stats.Count > 0) lines.Add(string.Join(". ", stats));
                }
            lines.AddRange(StatsLines.Run(yard.v_StatisticsDisplay));
            return lines;
        }

        // A column set's header (0) and its two halves (2 and 4), the halves' lines joined.
        private static void Columns(List<string> lines, tk2dTextMesh[] columns)
        {
            if (columns == null || columns.Length < 5) return;
            var entries = new List<string>();
            foreach (int i in new[] { 2, 4 })
                if (columns[i] != null) entries.AddRange(GameText.Lines(columns[i].text));
            if (entries.Count == 0) return;
            string head = GameText.Of(columns[0]);
            lines.Add((head != null ? head + " " : "") + string.Join(", ", entries));
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        }
    }
}
