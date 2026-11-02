using System.Collections.Generic;
using PixelHeroesAccess.Module.Interop;

namespace PixelHeroesAccess.Module.Nodes
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The run statistics the quest log and the graveyard show, read off the game's own displays,
    /// which build their texts from a <see cref="Tombstone"/> in the player's language:
    /// <see cref="StatisticsDisplay"/> (two columns of "label: value" lines, read row by row, left then
    /// right, then the two status lines) and <see cref="HeroStatisticsDisplay"/> (a hero's name, race
    /// and class, then a line per statistic; the last three start with a "&lt;" the font draws as a
    /// picture, dropped).
    /// </summary>
    public static class StatsLines
    {
        public static List<string> Run(StatisticsDisplay display)
        {
            var lines = new List<string>();
            if (display == null) return lines;
            Add(lines, GameText.Of(display.v_TextMesh_Title));
            var left = Split(display.v_TextMesh_Left);
            var right = Split(display.v_TextMesh_Right);
            int rows = System.Math.Max(left.Count, right.Count);
            for (int i = 0; i < rows; i++)
            {
                if (i < left.Count) Add(lines, left[i]);
                if (i < right.Count) Add(lines, right[i]);
            }
            Add(lines, GameText.Of(display.v_TextMesh_StatusInflicted_Text));
            Add(lines, GameText.Of(display.v_TextMesh_StatusSuffered_Text));
            return lines;
        }

        public static List<string> Hero(HeroStatisticsDisplay display)
        {
            var lines = new List<string>();
            if (display == null || !display.gameObject.activeInHierarchy) return lines;
            string head = GameText.Of(display.v_TextMesh_Name);
            string kind = GameText.Line((display.v_TextMesh_Race != null ? display.v_TextMesh_Race.text : "") + " " + (display.v_TextMesh_Class != null ? display.v_TextMesh_Class.text : ""));
            Add(lines, string.IsNullOrWhiteSpace(kind) ? head : head + ", " + kind);
            foreach (var raw in Split(display.v_TextMesh_Statistics))
            {
                // The death lines are "-" while the hero has not died.
                string line = raw.StartsWith("<") ? raw.Substring(1).Trim() : raw;
                if (line != "-") Add(lines, line);
            }
            return lines;
        }

        // A mesh's lines by its own breaks, each cleaned (blank ones kept as blanks for row pairing).
        private static List<string> Split(tk2dTextMesh mesh)
        {
            var lines = new List<string>();
            if (mesh == null || string.IsNullOrEmpty(mesh.text)) return lines;
            foreach (var raw in mesh.text.Split('\n')) lines.Add(GameText.Line(raw));
            return lines;
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        }
    }
}
