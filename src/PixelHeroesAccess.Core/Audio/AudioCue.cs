namespace PixelHeroesAccess.Core.Audio
{
    /// <summary>
    /// A named one-shot cue (the engine owns the sound file behind each name, so Core stays free of
    /// paths). One cue per file under assets/audio; a placeholder is replaced 1:1 by dropping in a file
    /// with the same name. The dd2a11y (Harkest Dungeon) pattern, by way of Guildrun.
    /// </summary>
    public enum AudioCue
    {
        // Battle (assets/audio/battle).
        /// <summary>The heroes' turn begins: an action is theirs to choose.</summary>
        TurnStart,
        /// <summary>A hero falls.</summary>
        HeroFallen,
    }

    /// <summary>The sounds screen's grouping of cues, mirroring the assets/audio folders.</summary>
    public enum AudioCueGroup
    {
        Battle,
    }

    public static class AudioCues
    {
        /// <summary>A cue's group: every cue is a battle cue so far.</summary>
        public static AudioCueGroup GroupOf(AudioCue cue) => AudioCueGroup.Battle;

        /// <summary>The file a cue plays, under the group's folder, without an extension:
        /// "turn_start" for <see cref="AudioCue.TurnStart"/> (the enum name in snake case).</summary>
        public static string FileName(AudioCue cue)
        {
            string name = cue.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>A cue's file under the asset root: "battle/turn_start.wav".</summary>
        public static string RelativePath(AudioCue cue)
            => GroupOf(cue).ToString().ToLowerInvariant() + "/" + FileName(cue) + ".wav";
    }
}
