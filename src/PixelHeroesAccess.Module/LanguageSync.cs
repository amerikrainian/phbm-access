using System;
using System.IO;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Strings;

namespace PixelHeroesAccess.Module
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The mod's authored strings follow the GAME's language (<see cref="Options.pLanguage"/>: English
    /// or German, the only two the game ships), not the system's: lang/&lt;code&gt;.txt beside the
    /// plugin ("en", "de"), English (the defaults) when there is no file for it. The game sets the
    /// language in three places (its options file at boot, the first-launch language choice on the
    /// splash screen, the title menu's options popup) and raises nothing, so the module tick compares
    /// the static enum with the one applied: one field read a frame.
    /// </summary>
    internal static class LanguageSync
    {
        private static string _langDir;
        private static string _applied;
        private static Action _onFirstLanguage;

        /// <summary>Whether the game's language has been read: the launch line and the update line
        /// wait for it, so they are spoken in the language the game came up in.</summary>
        public static bool Resolved => _applied != null;

        /// <summary>The language code whose strings are in effect ("de"), or null before the first read.</summary>
        public static string Applied => _applied;

        /// <summary>Begin following the game's language. <paramref name="onFirstLanguage"/> runs once,
        /// right after the first language is in place.</summary>
        public static void Start(string pluginDir, Action onFirstLanguage)
        {
            _langDir = Path.Combine(pluginDir, "lang");
            _onFirstLanguage = onFirstLanguage;
            Apply(CurrentCode());
        }

        /// <summary>Module teardown: the hook goes with this generation's Harmony id.</summary>
        public static void Stop()
        {
            _onFirstLanguage = null;
        }

        /// <summary>Per module tick: a language the game switched to since the last read.</summary>
        public static void Tick()
        {
            try { Apply(CurrentCode()); }
            catch (Exception e) { CoreLog.Error("LanguageSync: applying a language switch failed: " + e); }
        }

        /// <summary>The game's language as a lang file code.</summary>
        public static string CurrentCode()
            => Options.pLanguage == Options.eLanguages.German ? "de" : "en";

        private static void Apply(string code)
        {
            if (code == null || code == _applied) return;
            bool first = _applied == null;
            _applied = code;
            Load(code);
            if (!first || _onFirstLanguage == null) return;
            try { _onFirstLanguage(); }
            catch (Exception e) { CoreLog.Error("LanguageSync: the first-language callback failed: " + e); }
        }

        private static void Load(string code)
        {
            try
            {
                foreach (var name in LanguageFiles.Candidates(code))
                {
                    string path = Path.Combine(_langDir, name);
                    if (!File.Exists(path)) continue;
                    var report = Strings.LoadTranslation(File.ReadAllLines(path));
                    foreach (var problem in report.Problems()) CoreLog.Warning("lang " + name + ": " + problem);
                    CoreLog.Info("LanguageSync: " + code + ", " + report.Applied + " strings from " + name);
                    return;
                }
                Strings.LoadTranslation(null);
                CoreLog.Info("LanguageSync: " + code + ", no lang file for it, English");
            }
            catch (Exception e)
            {
                Strings.LoadTranslation(null);
                CoreLog.Error("LanguageSync: loading the strings of " + code + " failed, English: " + e);
            }
        }
    }
}
