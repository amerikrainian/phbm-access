using System;
using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;

namespace PixelHeroesAccess.Module.Nodes
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The help page's entries for the terms a reader names, appended after its lines as a tooltip's
    /// keyword section would be (Guildrun's): a status "Poisoned, turns 5: Causes increasing damage."
    /// (<see cref="DataStatusEffects"/>), Redemption, an element "Phoenix: strong against Griffon,
    /// weak against Leviathan" (<see cref="GameCore.CalculateElementRelationSourceToTarget"/>) and,
    /// after the first element that has relations, that the same element absorbs. Each term once, in
    /// the order it was named. Only the terms the mod's lines name from the data: the game's prose
    /// ("rock-hard", "This is sick!") is not scanned.
    /// </summary>
    public sealed class Definitions
    {
        private static readonly eElements[] Elemental = { eElements.Phoenix, eElements.Griffon, eElements.Wyrm, eElements.Leviathan };

        private readonly List<string> _lines = new List<string>();
        private readonly HashSet<string> _seen = new HashSet<string>();
        private bool _absorbSaid;

        /// <summary>The definitions collected, in the order their terms were named.</summary>
        public List<string> Lines => _lines;

        public Definitions Status(eStatusEffects status)
        {
            Add("status:" + status, () => StatusLine(status));
            return this;
        }

        public Definitions Redemption()
        {
            Add("redemption", RedemptionLine);
            return this;
        }

        public Definitions Element(eElements element)
        {
            Add("element:" + element, () => ElementLine(element));
            if (!_absorbSaid && Array.IndexOf(Elemental, element) >= 0)
            {
                _absorbSaid = true;
                Add("element:same", () => Strings.HelpElementSame(GameText.Tr(ET.ElementRelation_Absorbed)));
            }
            return this;
        }

        /// <summary>The statuses an item inflicts, then Redemption where it has it.</summary>
        public Definitions Item(Item item)
        {
            try
            {
                if (item?.pItemActive != null)
                {
                    var active = item.pItemActive;
                    var cls = active.GetItemActiveClass();
                    // The element is named with the damage, which these do not show.
                    bool noOutput = cls == DataItemsActive.eItemActiveClasses.Unique_BreakStunned
                        || cls == DataItemsActive.eItemActiveClasses.Skill_Shapeshifter_1_RandomOne
                        || cls == DataItemsActive.eItemActiveClasses.Skill_Shapeshifter_2_RandomTwo;
                    if (active.IsOffensive() && !noOutput) Element(active.GetElement());
                    if (cls == DataItemsActive.eItemActiveClasses.Prayer_Redemption || cls == DataItemsActive.eItemActiveClasses.Skill_Enchantress_2_Dispel)
                        Redemption();
                    else if (active.GetInflictStatuses() != null)
                        foreach (var s in active.GetInflictStatuses()) Status(s.pStatus);
                }
                else if (item?.pItemPassive != null)
                {
                    var passive = item.pItemPassive;
                    for (int i = 0; i < 5; i++)
                        if (Globals.RoundToInt(passive.GetElementProtection((eElements)i) * 100f) != 0) Element((eElements)i);
                    for (int i = 1; i < (int)eStatusEffects.COUNT; i++)
                        if (Globals.RoundToInt(passive.GetStatusProtection((eStatusEffects)i) * 100f) != 0) Status((eStatusEffects)i);
                }
            }
            catch (Exception e) { CoreLog.Warning("Definitions: item failed: " + e.Message); }
            return this;
        }

        /// <summary>The statuses on a character, in the order the game holds them.</summary>
        public Definitions StatusesOn(Character character)
        {
            var list = character != null ? character.pStatusEffectsCurrentlyActive : null;
            if (list == null) return this;
            foreach (var status in list)
                if (status != null) Status(status.GetStatusEffectType());
            return this;
        }

        /// <summary>"Poisoned, turns 5: Causes increasing damage.", as the help page says it; null for
        /// a status the game has no data for.</summary>
        public static string StatusLine(eStatusEffects status)
        {
            var data = DataStatusEffects.GetData(status);
            if (data == null) return null;
            string head = data.pLifetime > 0 ? Strings.StatusTurns(GameText.Line(data.pName), data.pLifetime.ToString()) : GameText.Line(data.pName);
            return head + ": " + GameText.Line(data.pDescription);
        }

        /// <summary>"Redemption: ...", the help page's line.</summary>
        public static string RedemptionLine()
            => GameText.Tr(ET.GUI_HL_T_Redemption_Name) + ": " + GameText.Line(GameText.Tr(ET.GUI_HL_T_Redemption_Text));

        /// <summary>"Phoenix: strong against Griffon, weak against Leviathan"; Neutral's own line.</summary>
        public static string ElementLine(eElements element)
        {
            if (Array.IndexOf(Elemental, element) < 0) return Strings.HelpElementNeutral(ItemLines.ElementName(eElements.Neutral));
            string beats = null, loses = null;
            foreach (var target in Elemental)
            {
                var relation = GameCore.CalculateElementRelationSourceToTarget(element, target);
                if (relation == eElementRelations.Strong) beats = ItemLines.ElementName(target);
                else if (relation == eElementRelations.Weak) loses = ItemLines.ElementName(target);
            }
            return Strings.HelpElement(ItemLines.ElementName(element), beats ?? "-", loses ?? "-");
        }

        private void Add(string key, Func<string> line)
        {
            if (!_seen.Add(key)) return;
            string text = null;
            try { text = line(); }
            catch (Exception e) { CoreLog.Warning("Definitions: " + key + " failed: " + e.Message); }
            if (!string.IsNullOrWhiteSpace(text)) _lines.Add(text);
        }
    }
}
