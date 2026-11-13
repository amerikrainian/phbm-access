using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Strings;
using HarmonyLib;
using UnityEngine;

namespace PixelHeroesAccess.Module.Audio
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>The three volumes the mod adds to the game's Audio options.</summary>
    public enum VolumeChannel { Master, Music, Effects }

    /// <summary>
    /// Master, music and effects volume, 0 to 300 percent in steps of 10, kept in the game's own options
    /// file (<c>Internal\settings.txt</c>) beside its music mode and its sound switch.
    ///
    /// The game has no volume of its own: <see cref="AudioControl"/> writes one constant
    /// (<see cref="AudioControl.mVOLUME"/>, 0.7 on PC) to its four sources every frame, apart from the
    /// music mix's cross-fade. A source's volume stops at 1, so the source alone reaches about 143%;
    /// <see cref="AudioListener.volume"/> amplifies past 1. After the game's Update the sources are
    /// scaled by their channel and divided by K, and the listener set to master times K, where
    /// K = max(1, the game's constant times the larger channel): every source stays at or under 1 and
    /// each hears exactly master times channel times what the game asked. K depends on the settings
    /// alone, so it holds still through the cross-fades. The speech is not Unity's and is untouched.
    ///
    /// The game finds an option by the first line that merely CONTAINS its key
    /// (<see cref="Persistence.ReadValue"/>), so the keys here contain none of the game's (Music, SFX,
    /// Version, Internal, Language, ...), and they go before its EndOfFile, inside the 20 lines it reads.
    /// Without the mod the game's next save drops them, nothing else.
    /// </summary>
    public static class Volume
    {
        public const int Step = 10;
        public const int Max = 300;
        public const int Default = 100;

        private static readonly string[] Keys = { "VolumeMaster", "VolumeSoundtrack", "VolumeEffects" };
        private const string EndOfFile = "EndOfFile";

        private static readonly int[] _percent = { Default, Default, Default };

        /// <summary>Raised after a volume changes (the visible rows redraw).</summary>
        public static event Action Changed;

        public static VolumeChannel[] Channels => new[] { VolumeChannel.Master, VolumeChannel.Music, VolumeChannel.Effects };

        public static int Get(VolumeChannel channel) => _percent[(int)channel];

        public static string Label(VolumeChannel channel)
        {
            switch (channel)
            {
                case VolumeChannel.Music: return Strings.VolumeMusic;
                case VolumeChannel.Effects: return Strings.VolumeEffects;
                default: return Strings.VolumeMaster;
            }
        }

        public static string ValueText(VolumeChannel channel) => Strings.VolumePercent(Get(channel));

        /// <summary>Move a volume by whole steps, saved at once with the game's options and heard as
        /// the game's toggle sound (at the new effects volume). False when it is already at that end.</summary>
        public static bool Adjust(VolumeChannel channel, int steps)
        {
            int before = Get(channel);
            int after = Mathf.Clamp((int)Math.Round(before / (double)Step) * Step + steps * Step, 0, Max);
            if (after == before) return false;
            _percent[(int)channel] = after;
            try { Persistence.SaveOptions(); }
            catch (Exception e) { CoreLog.Warning("Volume: saving the options failed: " + e); }
            try { if (ScreenBase.pAudioControl != null) ScreenBase.pAudioControl.PlaySoundMenu(AudioControl.eSoundsMenu.SwitchToggle); }
            catch (Exception e) { CoreLog.Warning("Volume: the toggle sound failed: " + e); }
            try { Changed?.Invoke(); }
            catch (Exception e) { CoreLog.Warning("Volume: redrawing failed: " + e); }
            return true;
        }

        private static float Factor(VolumeChannel channel) => Get(channel) / 100f;

        /// <summary>Module teardown: on a shutdown the game's own loudness comes back (its Update rewrites
        /// the sources next frame, the listener is ours alone); on a reload the successor sets it.</summary>
        public static void Shutdown(bool restore)
        {
            Changed = null;
            if (restore) AudioListener.volume = 1f;
        }

        // ---- the options file ----

        private static string OptionsPath()
        {
            string dir = Persistence.mPersistencePath;
            return string.IsNullOrEmpty(dir) ? null : dir + Persistence.mFILENAME_OPTIONS;
        }

        /// <summary>The saved volumes, read from the game's options file: at load (the game read its
        /// options before the module came up) and after each load of the game's own. Absent ones are 100.</summary>
        public static void ReadSaved()
        {
            string path = OptionsPath();
            if (path == null || !File.Exists(path)) return;
            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < Keys.Length; i++)
                    _percent[i] = Parse(lines, Keys[i]);
            }
            catch (Exception e) { CoreLog.Warning("Volume: reading " + path + " failed: " + e); }
        }

        private static int Parse(string[] lines, string key)
        {
            string prefix = key + "=";
            foreach (var line in lines)
            {
                if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (int.TryParse(line.Substring(prefix.Length).Trim(), out int value)) return Mathf.Clamp(value, 0, Max);
                CoreLog.Warning("Volume: unreadable line in the options: " + line);
            }
            return Default;
        }

        // After the game writes its options, ours go in before its EndOfFile (any older ones dropped).
        private static void WriteSaved()
        {
            string path = OptionsPath();
            if (path == null || !File.Exists(path)) return;
            try
            {
                var kept = new List<string>();
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    bool ours = false;
                    foreach (var key in Keys)
                        if (line.StartsWith(key + "=", StringComparison.Ordinal)) ours = true;
                    if (!ours) kept.Add(line);
                }
                int end = kept.IndexOf(EndOfFile);
                if (end < 0) end = kept.Count;
                var mine = new List<string>();
                for (int i = 0; i < Keys.Length; i++) mine.Add(Keys[i] + "=" + _percent[i]);
                kept.InsertRange(end, mine);
                File.WriteAllLines(path, kept.ToArray(), Encoding.UTF8);
            }
            catch (Exception e) { CoreLog.Warning("Volume: writing " + path + " failed: " + e); }
        }

        // ---- applying ----

        private static void Apply(AudioControl audio)
        {
            float gameMax = audio.mVOLUME;
            float music = Factor(VolumeChannel.Music);
            float effects = Factor(VolumeChannel.Effects);
            float k = Mathf.Max(1f, gameMax * Mathf.Max(music, effects));
            Scale(audio.v_Source_Music, music / k);
            Scale(audio.v_Source_Music_Alternative, music / k);
            Scale(audio.v_Source_Sound_BattleEffect, effects / k);
            Scale(audio.v_Source_Sound_Menu, effects / k);
            AudioListener.volume = Factor(VolumeChannel.Master) * k;
        }

        private static void Scale(AudioSource source, float factor)
        {
            if (source != null) source.volume *= factor;
        }

        [HarmonyPatch(typeof(AudioControl), nameof(AudioControl.Update))]
        private static class AudioUpdated
        {
            private static void Postfix(AudioControl __instance)
            {
                try { Apply(__instance); }
                catch (Exception e) { CoreLog.Warning("Volume: applying failed: " + e); }
            }
        }

        [HarmonyPatch(typeof(Persistence), nameof(Persistence.SaveOptions))]
        private static class OptionsSaved
        {
            private static void Postfix() => WriteSaved();
        }

        [HarmonyPatch(typeof(Persistence), nameof(Persistence.LoadOptions))]
        private static class OptionsLoaded
        {
            private static void Postfix() => ReadSaved();
        }
    }
}
