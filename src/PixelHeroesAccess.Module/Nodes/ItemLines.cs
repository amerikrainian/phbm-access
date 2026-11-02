using System;
using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using UnityEngine;

namespace PixelHeroesAccess.Module.Nodes
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// An item as speech reads it, built from the item's own getters the way the game's
    /// <see cref="ItemDisplay"/> draws it in its complete form (the battle, inventory and shop panels
    /// show subsets of the same): the name, then one line per fact. An active item (weapon, spell,
    /// prayer, unique, skill): its kind, the damage or healing it does with the hero who holds it
    /// (<see cref="ItemActive.CalculateDamageWithHero"/>), its element and damage type, a skill's
    /// cooldown, the range, the damage formula, a shield's defenses, the statuses it inflicts, the
    /// description. A passive item (body, head, trinket): its kind, the two defenses, the attribute
    /// bonuses, the element defenses, the status immunities, the description. A potion: what it heals,
    /// how many, the description. Then the help's entries for the statuses, elements and Redemption
    /// those lines name (<see cref="Definitions"/>). The game's own words throughout; the joins are
    /// the mod's.
    /// </summary>
    public static class ItemLines
    {
        /// <summary>The item's full name as the game shows it.</summary>
        public static string Name(Item item)
        {
            if (item == null) return null;
            try { return GameText.Line(item.GetNameFull()); }
            catch (Exception e)
            {
                CoreLog.Warning("ItemLines: name failed: " + e.Message);
                return item.gameObject.name;
            }
        }

        /// <summary>The item's one-line summary for a focus readout: its name and its headline value
        /// (damage or healing for an active item, the defenses for armor, the count of a potion).</summary>
        public static string Summary(Item item, CharacterHero hero)
        {
            if (item == null) return null;
            var parts = new List<string> { Name(item) };
            try
            {
                // A potion's name carries its count ("Small Healing Potion x 2").
                if (item.pItemActive != null) parts.Add(Output(item.pItemActive, hero));
                else if (item.pItemPassive != null && item.pItemPassive.GetItemPassiveType() != eItemPassiveTypes.Trinket)
                    parts.Add(Defenses(item.pItemPassive.GetDefPhy(), item.pItemPassive.GetDefMag()));
                else if (item.pItemPassive != null)
                {
                    // A trinket's headline is its first bonus (attributes, element defense, immunity).
                    var lines = new List<string>();
                    Passive(item.pItemPassive, lines);
                    lines.RemoveAll(string.IsNullOrWhiteSpace);
                    if (lines.Count > 2) parts.Add(lines[1]);
                }
            }
            catch (Exception e) { CoreLog.Warning("ItemLines: summary failed: " + e.Message); }
            return Join(parts);
        }

        /// <summary>The item's detail lines, name excluded, in the game's panel order.</summary>
        public static List<string> Lines(Item item, CharacterHero hero)
        {
            var lines = new List<string>();
            if (item == null) return lines;
            try
            {
                if (item.pItemActive != null) Active(item.pItemActive, hero, lines);
                else if (item.pItemPassive != null) Passive(item.pItemPassive, lines);
                else if (item.pItemPotion != null) Potion(item.pItemPotion, lines);
            }
            catch (Exception e) { CoreLog.Warning("ItemLines: lines failed for " + Name(item) + ": " + e); }
            lines.AddRange(new Definitions().Item(item).Lines);
            lines.RemoveAll(string.IsNullOrWhiteSpace);
            return lines;
        }

        /// <summary>The name and every detail as one list (a buffer's whole item).</summary>
        public static List<string> All(Item item, CharacterHero hero)
        {
            var lines = new List<string>();
            if (item == null) return lines;
            lines.Add(Name(item));
            lines.AddRange(Lines(item, hero));
            return lines;
        }

        /// <summary>The kind word of an item ("weapon", "spell", "skill", "body armor", "potion").</summary>
        public static string Kind(Item item)
        {
            if (item == null) return null;
            if (item.pItemActive != null)
            {
                switch (item.pItemActive.GetItemActiveType())
                {
                    case eItemActiveTypes.Weapon: return Strings.ItemKindWeapon;
                    case eItemActiveTypes.Spell: return Strings.ItemKindSpell;
                    case eItemActiveTypes.Prayer: return Strings.ItemKindPrayer;
                    case eItemActiveTypes.Unique: return Strings.ItemKindUnique;
                    default: return Strings.ItemKindSkill;
                }
            }
            if (item.pItemPassive != null)
            {
                switch (item.pItemPassive.GetItemPassiveType())
                {
                    case eItemPassiveTypes.Body: return Strings.ItemKindBody;
                    case eItemPassiveTypes.Head: return Strings.ItemKindHead;
                    default: return Strings.ItemKindTrinket;
                }
            }
            return Strings.ItemKindPotion;
        }

        private static void Active(ItemActive item, CharacterHero hero, List<string> lines)
        {
            var cls = item.GetItemActiveClass();
            lines.Add(Kind(item));
            bool randomSkill = cls == DataItemsActive.eItemActiveClasses.Skill_Shapeshifter_1_RandomOne
                || cls == DataItemsActive.eItemActiveClasses.Skill_Shapeshifter_2_RandomTwo;
            if (!randomSkill)
            {
                if (cls != DataItemsActive.eItemActiveClasses.Unique_BreakStunned) lines.Add(Output(item, hero));
                if (item.IsSkill()) lines.Add(Cooldown((ItemActiveSkill)item));
                lines.Add(Strings.ItemPair(GameText.Tr(ET.ItemDisplay_Range), GameText.Line(item.GetRangeAsString())));
            }
            if (item.IsShield())
                lines.Add(Defenses(item.GetShieldDefenseBonus(eDamageTypes.Physical), item.GetShieldDefenseBonus(eDamageTypes.Magical)));
            if (!item.IsSkill()) lines.Add(Formula(item));
            if (cls == DataItemsActive.eItemActiveClasses.Prayer_Redemption || cls == DataItemsActive.eItemActiveClasses.Skill_Enchantress_2_Dispel)
                lines.Add(GameText.Tr(ET.ItemDisplay_Redemption));
            else lines.Add(Inflicts(item.GetInflictStatuses()));
            lines.Add(GameText.Line(item.GetDescription()));
        }

        /// <summary>What the item does in one fight's action with this hero: "Physical damage 34,
        /// Phoenix" or "Healing 20"; the fist's damage is the game's "?".</summary>
        public static string Output(ItemActive item, CharacterHero hero)
        {
            var cls = item.GetItemActiveClass();
            if (cls == DataItemsActive.eItemActiveClasses.Unique_BreakStunned) return null;
            string value = cls == DataItemsActive.eItemActiveClasses.Unique_Fist || hero == null
                ? "?" : item.CalculateDamageWithHero(hero).ToString();
            if (!item.IsOffensive()) return Strings.ItemPair(GameText.Tr(ET.ItemDisplay_Heal), value);
            string type = item.GetDamageType() == eDamageTypes.Physical ? GameText.Tr(ET.ItemDisplay_DmgPhy) : GameText.Tr(ET.ItemDisplay_DmgMag);
            return Strings.ItemDamage(type, value, ElementName(item.GetElement()));
        }

        private static string Cooldown(ItemActiveSkill skill)
        {
            string line = Strings.ItemPair(GameText.Tr(ET.GUI_HL_T_Items_CooldownTitle), skill.GetCooldownTotal().ToString());
            int current = skill.GetCooldownCurrent();
            return current > 0 ? line + ", " + Strings.ItemCoolingDown(current.ToString()) : line;
        }

        // "10 + Power 50% + Accuracy 20%": the base damage and each hero value's share.
        private static string Formula(ItemActive item)
        {
            var parts = new List<string>();
            int baseDamage = item.GetBaseDamage();
            parts.Add(baseDamage.ToString());
            Share(parts, item.GetPWR(), eHeroValues.PWR);
            Share(parts, item.GetACC(), eHeroValues.ACC);
            Share(parts, item.GetMAG(), eHeroValues.MAG);
            Share(parts, item.GetSPR(), eHeroValues.SPR);
            return parts.Count > 1 || baseDamage != 0 ? Strings.ItemFormula(string.Join(" + ", parts)) : null;
        }

        private static void Share(List<string> parts, float factor, eHeroValues value)
        {
            if (factor <= 0f) return;
            parts.Add(GameText.ValueName(value) + " " + Globals.RoundToInt(factor * 100f) + "%");
        }

        private static string Inflicts(List<StatusChance> statuses)
        {
            if (statuses == null || statuses.Count == 0) return null;
            var parts = new List<string>();
            foreach (var s in statuses)
                parts.Add(GameText.StatusName(s.pStatus) + " " + Globals.RoundToInt(s.pChance * 100f) + "%");
            return GameText.Tr(ET.ItemDisplay_StatusInflicts) + " " + string.Join(", ", parts);
        }

        private static void Passive(ItemPassive item, List<string> lines)
        {
            lines.Add(Kind(item));
            if (item.GetItemPassiveType() != eItemPassiveTypes.Trinket)
                lines.Add(Defenses(item.GetDefPhy(), item.GetDefMag()));
            var bonuses = new List<string>();
            for (int i = 0; i < (int)eHeroAttributes.COUNT; i++)
            {
                var attribute = (eHeroAttributes)i;
                int bonus = item.GetHeroAttributeBonus(attribute);
                if (bonus != 0) bonuses.Add(GameText.AttributeName(attribute) + " " + Signed(bonus));
            }
            if (bonuses.Count > 0) lines.Add(string.Join(", ", bonuses));
            var elements = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                int percent = Globals.RoundToInt(item.GetElementProtection((eElements)i) * 100f);
                if (percent != 0) elements.Add(ElementName((eElements)i) + " " + Mathf.Abs(percent) + "%");
            }
            if (elements.Count > 0) lines.Add(GameText.Tr(ET.ItemDisplay_ElementDefense) + " " + string.Join(", ", elements));
            var immune = new List<string>();
            for (int i = 1; i < (int)eStatusEffects.COUNT; i++)
                if (Globals.RoundToInt(item.GetStatusProtection((eStatusEffects)i) * 100f) != 0)
                    immune.Add(GameText.StatusName((eStatusEffects)i));
            if (immune.Count > 0) lines.Add(GameText.Tr(ET.ItemDisplay_StatusImmunity) + " " + string.Join(", ", immune));
            lines.Add(GameText.Line(item.GetDescription()));
        }

        private static void Potion(ItemPotion potion, List<string> lines)
        {
            lines.Add(Kind(potion));
            lines.Add(Strings.ItemPotionCount(potion.GetCount().ToString()));
            lines.Add(GameText.Line(potion.GetTextValues()));
            lines.Add(GameText.Line(potion.GetDescription()));
        }

        /// <summary>"Physical defense +3, Magical defense +2".</summary>
        public static string Defenses(int physical, int magical)
            => Strings.ValueDefPhy + " " + Signed(physical) + ", " + Strings.ValueDefMag + " " + Signed(magical);

        /// <summary>An element's name as the help page has it.</summary>
        public static string ElementName(eElements element)
        {
            switch (element)
            {
                case eElements.Phoenix: return GameText.Tr(ET.GUI_HL_T_Elements_Phoenix);
                case eElements.Griffon: return GameText.Tr(ET.GUI_HL_T_Elements_Griffon);
                case eElements.Wyrm: return GameText.Tr(ET.GUI_HL_T_Elements_Wyrm);
                case eElements.Leviathan: return GameText.Tr(ET.GUI_HL_T_Elements_Leviathan);
                default: return GameText.Tr(ET.GUI_HL_T_Elements_Neutral);
            }
        }

        /// <summary>A bonus with its sign: "+3", "-2", "0".</summary>
        public static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private static string Join(List<string> parts)
        {
            parts.RemoveAll(string.IsNullOrWhiteSpace);
            return string.Join(", ", parts);
        }
    }
}
