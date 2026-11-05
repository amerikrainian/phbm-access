using System;
using System.Collections.Generic;
using HarmonyLib;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Battle;
using PixelHeroesAccess.Module.Interop;
using UnityEngine;

namespace PixelHeroesAccess.Module.UI
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The game's speech bubbles, said as each appears, the speaker first ("Calen: Damn you!") and kept
    /// in the log buffer. Every bubble with a single line goes through <see cref="SpeechBubble.ShowWithText"/>:
    /// a hero's remarks in a fight (<see cref="SpeechBubble.ShowBattleSpeech"/>, which rolls its own
    /// 50%, always for the intro and death), the tavern's hired and rejected heroes, the shopkeeper's
    /// thanks, the inventory's hint on swapping heroes, and the idle chatter of the town, tavern, temple,
    /// library and arena. Some come with a delay (the temple's and library's two seconds, the inventory
    /// hint's), so the line is kept per bubble and said when the bubble shows
    /// (<c>InternalShowSpeechBubble</c> without a delay, which the delayed path calls when its wait ends).
    /// The speaker is the bubble's hero, else the NPC it hangs from, else the tavern's band, else nobody
    /// (the shop).
    /// A hit's or heal's remark is raised just before that action's log line
    /// (<c>Action.UpdateGUIWhenDamageNumberAppears</c>), so it is held until that method ends and said
    /// after the outcome it answers. The boss's paged speech is the battle screen's. Two settings: the
    /// bubbles, and the idle chatter among them.
    /// </summary>
    internal static class SpeechBubbles
    {
        public const string OnKey = "speak_speech_bubbles";
        public const string IdleKey = "speak_idle_chatter";

        public static bool On => Setting(OnKey);
        public static bool IdleOn => Setting(IdleKey);

        private static bool Setting(string key)
        {
            var settings = ModuleMain.Host != null ? ModuleMain.Host.Settings : null;
            return settings == null || settings.Store.GetBool(key, true);
        }

        private sealed class Pending
        {
            public string Text;
            public bool Idle;
        }

        // Each bubble's line from ShowWithText until it shows: event data, like a log line.
        private static readonly Dictionary<SpeechBubble, Pending> _pending = new Dictionary<SpeechBubble, Pending>(new ByReference());
        private static int _idleDepth;
        private static bool _hold;
        private static readonly List<string> _held = new List<string>();

        [HarmonyPatch(typeof(SpeechBubble), nameof(SpeechBubble.ShowWithText))]
        private static class Text
        {
            private static void Prefix(SpeechBubble __instance, string __0)
            {
                try
                {
                    Prune();
                    _pending[__instance] = new Pending { Text = __0, Idle = _idleDepth > 0 };
                }
                catch (Exception e) { CoreLog.Warning("SpeechBubbles: text failed: " + e); }
            }
        }

        // The boss's pages never pass through ShowWithText: nothing of an earlier line may ride on them.
        [HarmonyPatch(typeof(SpeechBubble), nameof(SpeechBubble.ShowWithTextArray))]
        private static class Paged
        {
            private static void Prefix(SpeechBubble __instance) => _pending.Remove(__instance);
        }

        [HarmonyPatch(typeof(SpeechBubble), nameof(SpeechBubble.InternalShowSpeechBubble))]
        private static class Shown
        {
            private static void Postfix(SpeechBubble __instance, float __1)
            {
                try
                {
                    if (__1 != 0f) return; // still waiting out its delay
                    if (!_pending.TryGetValue(__instance, out var pending)) return;
                    _pending.Remove(__instance);
                    if (!On || (pending.Idle && !IdleOn)) return;
                    string line = GameText.Line(pending.Text);
                    if (string.IsNullOrWhiteSpace(line)) return;
                    string speaker = Speaker(__instance);
                    string said = string.IsNullOrEmpty(speaker) ? line : Strings.SpeechBubble(speaker, line);
                    if (_hold) _held.Add(said);
                    else BattleEvents.Report(said);
                }
                catch (Exception e) { CoreLog.Warning("SpeechBubbles: shown failed: " + e); }
            }
        }

        [HarmonyPatch(typeof(SpeechBubble), nameof(SpeechBubble.ShowRandomTavernIdle))]
        private static class TavernIdle
        {
            private static void Prefix() => _idleDepth++;
            private static void Finalizer() => _idleDepth--;
        }

        [HarmonyPatch(typeof(SpeechBubble), nameof(SpeechBubble.ShowTownIdleRandom))]
        private static class TownIdle
        {
            private static void Prefix() => _idleDepth++;
            private static void Finalizer() => _idleDepth--;
        }

        [HarmonyPatch(typeof(SpeechBubble), nameof(SpeechBubble.ShowTownIdleRandomNPC))]
        private static class NpcIdle
        {
            private static void Prefix() => _idleDepth++;
            private static void Finalizer() => _idleDepth--;
        }

        [HarmonyPatch(typeof(global::Action), nameof(global::Action.UpdateGUIWhenDamageNumberAppears))]
        private static class AfterOutcome
        {
            private static void Prefix() => _hold = true;

            private static void Finalizer()
            {
                _hold = false;
                try
                {
                    foreach (var said in _held) BattleEvents.Report(said);
                }
                catch (Exception e) { CoreLog.Warning("SpeechBubbles: held line failed: " + e); }
                finally { _held.Clear(); }
            }
        }

        private static string Speaker(SpeechBubble bubble)
        {
            if (bubble.mHeroData != null) return GameText.Line(bubble.mHeroData.GetNameHero());
            var npc = bubble.GetComponentInParent<NPC>();
            if (npc != null) return GameText.Line(npc.GetName());
            var tavern = ScreenBase.pCurrentInstance as Tavern;
            return tavern != null && tavern.v_SpeechBubbleBand == bubble ? Strings.TavernBand : null;
        }

        // Bubbles of a scene gone (Unity's null) leave the table.
        private static void Prune()
        {
            List<SpeechBubble> gone = null;
            foreach (var bubble in _pending.Keys)
                if (bubble == null) (gone ??= new List<SpeechBubble>()).Add(bubble);
            if (gone != null) foreach (var bubble in gone) _pending.Remove(bubble);
        }

        // By reference: Unity's own equality makes every destroyed object equal to every other.
        private sealed class ByReference : IEqualityComparer<SpeechBubble>
        {
            public bool Equals(SpeechBubble a, SpeechBubble b) => ReferenceEquals(a, b);
            public int GetHashCode(SpeechBubble o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        /// <summary>Module teardown (the patches go with the Harmony id).</summary>
        public static void Shutdown()
        {
            _pending.Clear();
            _idleDepth = 0;
            _hold = false;
            _held.Clear();
        }
    }
}
