using System;
using System.Collections.Generic;
using HarmonyLib;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Events;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Battle
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// What a fight does, spoken as it happens and kept in the log buffer (<see cref="GameEvents.Events"/>),
    /// in the game's own words: the battle log's entry for each action (<see cref="LogScreen.AddMessageForAction"/>,
    /// raised once per standard action as its damage numbers appear, for heroes and enemies alike:
    /// "Ghada used Acid Rain: Leviathan Magical. Scorpion -23 HP Effective!"), a stunned character's
    /// lost turn (<see cref="LogScreen.AddMessageForStunned"/>), and what the game's log leaves out:
    /// the statuses' ticks between turns (<see cref="StatusEffect.UpdateTick"/>: burning and poison
    /// damage, a status running out). The log's marks are rearranged for speech: the target's "&gt;"
    /// arrow dropped, its KILL mark put last. Every hook is a postfix; nothing in the fight is changed.
    /// </summary>
    internal static class BattleEvents
    {
        private static ActionOutcome _lastOutcome;

        /// <summary>Speak a line and keep it in the log.</summary>
        public static void Report(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            GameEvents.Events.Add(line);
            if (FocusMode.Active) Speech.Say(line);
        }

        [HarmonyPatch(typeof(LogScreen), nameof(LogScreen.AddMessageForAction))]
        private static class ActionLogged
        {
            private static void Postfix(ActionOutcome __0)
            {
                var bOutcome = __0;
                try
                {
                    // The game logs one outcome again when its numbers appear at different times.
                    if (bOutcome == null || ReferenceEquals(bOutcome, _lastOutcome)) return;
                    _lastOutcome = bOutcome;
                    var lines = new List<string>();
                    new LogscreenEntryAction(bOutcome).GenerateMessage(ref lines);
                    Report(Speakable(lines));
                }
                catch (Exception e) { CoreLog.Warning("BattleEvents: action log failed: " + e); }
            }
        }

        [HarmonyPatch(typeof(LogScreen), nameof(LogScreen.AddMessageForStunned))]
        private static class StunLogged
        {
            private static void Postfix(Character __0)
            {
                var bCharacter = __0;
                try
                {
                    var lines = new List<string>();
                    new LogscreenEntryStunned(bCharacter).GenerateMessage(ref lines);
                    Report(Speakable(lines));
                }
                catch (Exception e) { CoreLog.Warning("BattleEvents: stun log failed: " + e); }
            }
        }

        [HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.UpdateTick))]
        private static class StatusTicked
        {
            private static void Prefix(StatusEffect __instance, out int __state) => __state = __instance.mRemainingTicks;

            private static void Postfix(StatusEffect __instance, ActionTarget __result, int __state)
            {
                try
                {
                    var character = __instance.mInflictedCharacter;
                    if (character == null) return;
                    string name = GameText.Line(character.GetName());
                    string status = GameText.StatusName(__instance.GetStatusEffectType());
                    if (__result != null && __result.pDamage != 0)
                    {
                        string line = Strings.BattleStatusDamage(status, name, ItemSigned(-__result.pDamage));
                        if (__result.pTargetWasKilled) line += ", " + GameText.Tr(ET.LogTargetWasKilled);
                        Report(line);
                    }
                    if (__state > 0 && __instance.mRemainingTicks == 0 && character.IsAlive())
                        Report(Strings.BattleStatusEnds(status, name));
                }
                catch (Exception e) { CoreLog.Warning("BattleEvents: status tick failed: " + e); }
            }
        }

        private static string ItemSigned(int value) => value > 0 ? "+" + value : value.ToString();

        // The log's lines as one utterance: each cleaned, a target's arrow dropped, its KILL mark last.
        private static string Speakable(List<string> lines)
        {
            string kill = GameText.Tr(ET.LogTargetWasKilled);
            var parts = new List<string>();
            foreach (var raw in lines)
            {
                string line = GameText.Line(raw);
                if (string.IsNullOrWhiteSpace(line)) continue;
                bool killed = false;
                if (!string.IsNullOrEmpty(kill) && line.StartsWith(kill))
                {
                    killed = true;
                    line = line.Substring(kill.Length).TrimStart();
                }
                if (line.StartsWith(">")) line = line.Substring(1).TrimStart();
                if (killed) line += ", " + kill;
                parts.Add(line);
            }
            return GameNodes.Sentences(parts);
        }

        /// <summary>Module teardown: forget the last outcome (the patches go with the Harmony id).</summary>
        public static void Shutdown() => _lastOutcome = null;
    }
}
