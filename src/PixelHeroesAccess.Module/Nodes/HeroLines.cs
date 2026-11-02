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
    /// A hero as speech reads it, from the live <see cref="CharacterHero"/> (the scene's figure, whose
    /// derived values the game recomputes from the attributes and the passive items:
    /// <see cref="CharacterHero.CalculateDerivedValues"/>, a pure recalculation). The hero buffer is
    /// the whole hero: name, race, class and level; health; experience; unspent attribute points; the
    /// five attributes with what one point adds to each; the six combat values; the protections
    /// (element defenses and status immunities, summed over the passive items, as the inventory's
    /// panel shows them); the statuses on the hero in a fight; the help's entries for the elements and
    /// statuses named (<see cref="Definitions"/>). The items buffer is the hero's
    /// equipment slot by slot, then the skills, each item as one line (name and headline value).
    /// </summary>
    public static class HeroLines
    {
        /// <summary>"Name, Race Class, Level 3".</summary>
        public static string Head(CharacterHero hero)
        {
            if (hero == null) return null;
            var parts = new List<string> { GameText.Line(hero.GetName()), GameText.Line(hero.GetNameRace() + " " + hero.GetNameClass()) };
            parts.Add(Strings.ItemPair(GameText.Tr(ET.Inventory_Hero_Level), hero.GetLevel().ToString()));
            return string.Join(", ", parts);
        }

        /// <summary>"HP 30 of 45", or the dead word when the hero has fallen.</summary>
        public static string Health(CharacterHero hero)
        {
            if (hero == null) return null;
            if (!hero.IsAlive()) return Strings.HeroDead;
            return Strings.HeroHp(hero.GetHPCurrent().ToString(), hero.GetHPMax().ToString());
        }

        /// <summary>The whole hero, one fact per line.</summary>
        public static List<string> Lines(CharacterHero hero, bool tavern = false)
        {
            var lines = new List<string>();
            if (hero == null) return lines;
            try
            {
                hero.CalculateDerivedValues();
                lines.Add(Head(hero));
                if (!tavern) lines.Add(Health(hero));
                if (!tavern) lines.Add(Experience(hero));
                int points = hero.GetSkillPointsCurrent();
                if (points > 0) lines.Add(Strings.HeroPoints(points.ToString()));
                lines.Add(Attributes(hero, !tavern));
                lines.Add(PerPoint(hero));
                if (!tavern) lines.Add(Values(hero));
                lines.Add(Protections(hero));
                lines.Add(Statuses(hero));
                lines.AddRange(Defined(hero).Lines);
            }
            catch (Exception e) { CoreLog.Warning("HeroLines: " + e); }
            lines.RemoveAll(string.IsNullOrWhiteSpace);
            return lines;
        }

        // The help's entries for the elements and statuses the protections and the statuses name.
        private static Definitions Defined(CharacterHero hero)
        {
            var defs = new Definitions();
            for (int i = 1; i < 5; i++)
                if (Globals.RoundToInt(hero.GetTotalProtectionFactorAgainstElement((eElements)i) * 100f) > 0) defs.Element((eElements)i);
            for (int i = 1; i < (int)eStatusEffects.COUNT; i++)
                if (hero.GetProtectionAgainstStatus((eStatusEffects)i) > 0f) defs.Status((eStatusEffects)i);
            return defs.StatusesOn(hero);
        }

        /// <summary>"Experience 120, next level at 200", or the game's maximum level line.</summary>
        public static string Experience(CharacterHero hero)
        {
            int level = hero.GetLevel();
            string xp = hero.GetEXP().ToString();
            if (level >= DataBalancing.pMAX_HERO_LEVEL || DataBalancing.pEXPForLevelUp == null || level + 1 >= DataBalancing.pEXPForLevelUp.Length)
                return Strings.HeroXpMax(xp, GameText.Tr(ET.Inventory_MaximumLevel));
            return Strings.HeroXp(xp, DataBalancing.pEXPForLevelUp[level + 1].ToString());
        }

        /// <summary>"Life 12, Strength 8, Dexterity 5, Intelligence 3, Faith 4": the current values
        /// (with the passive items' bonuses) or, for a tavern candidate, the base ones.</summary>
        public static string Attributes(CharacterHero hero, bool withItems = true)
        {
            var parts = new List<string>();
            for (int i = 0; i < (int)eHeroAttributes.COUNT; i++)
            {
                var a = (eHeroAttributes)i;
                parts.Add(GameText.AttributeName(a) + " " + hero.GetAttribute(a));
            }
            return string.Join(", ", parts);
        }

        /// <summary>What one attribute point adds to each attribute ("per point: Life 3, ..."); the
        /// Shapeshifter's are the game's "?".</summary>
        public static string PerPoint(CharacterHero hero)
        {
            var parts = new List<string>();
            for (int i = 0; i < (int)eHeroAttributes.COUNT; i++)
            {
                var a = (eHeroAttributes)i;
                string value = hero.IsShapeshifter() ? "?" : hero.GetAttributeLevelUpValue(a).ToString();
                parts.Add(GameText.AttributeName(a) + " " + value);
            }
            return Strings.HeroPerPoint(string.Join(", ", parts));
        }

        /// <summary>"Power 20, Accuracy 15, Magic 10, Spirit 5, Physical defense 12, Magical defense 8".</summary>
        public static string Values(CharacterHero hero)
        {
            return string.Join(", ", new[]
            {
                GameText.ValueName(eHeroValues.PWR) + " " + hero.pSkillPWR,
                GameText.ValueName(eHeroValues.ACC) + " " + hero.pSkillACC,
                GameText.ValueName(eHeroValues.MAG) + " " + hero.pSkillMAG,
                GameText.ValueName(eHeroValues.SPR) + " " + hero.pSkillSPR,
                Strings.ValueDefPhy + " " + hero.pSkillDefPhy,
                Strings.ValueDefMag + " " + hero.pSkillDefMag,
            });
        }

        /// <summary>The game's "Protection" line: element defenses with their percentages, then the
        /// statuses the hero is immune to; null when there are none.</summary>
        public static string Protections(CharacterHero hero)
        {
            var parts = new List<string>();
            for (int i = 1; i < 5; i++)
            {
                int percent = Globals.RoundToInt(hero.GetTotalProtectionFactorAgainstElement((eElements)i) * 100f);
                if (percent > 0) parts.Add(ItemLines.ElementName((eElements)i) + " " + percent + "%");
            }
            for (int i = 1; i < (int)eStatusEffects.COUNT; i++)
                if (hero.GetProtectionAgainstStatus((eStatusEffects)i) > 0f) parts.Add(GameText.StatusName((eStatusEffects)i));
            return parts.Count == 0 ? null : Strings.ItemPair(GameText.Tr(ET.Inventory_Immunities), string.Join(", ", parts));
        }

        /// <summary>The statuses on a character with the turns each has left, or null.</summary>
        public static string Statuses(Character character)
        {
            var list = character != null ? character.pStatusEffectsCurrentlyActive : null;
            if (list == null || list.Count == 0) return null;
            var parts = new List<string>();
            foreach (var status in list)
            {
                if (status == null) continue;
                var type = status.GetStatusEffectType();
                int left = status.mRemainingTicks;
                parts.Add(left > 0 ? Strings.StatusTurns(GameText.StatusName(type), left.ToString()) : GameText.StatusName(type));
            }
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>The hero's equipment and skills, one line per slot: "Left hand: Iron Sword,
        /// Physical damage 12, Neutral", "Head: empty".</summary>
        public static List<string> Items(CharacterHero hero)
        {
            var lines = new List<string>();
            if (hero == null) return lines;
            try
            {
                for (int i = 0; i < hero.pItemsActive.Length; i++)
                    lines.Add(Strings.SlotLine(SlotName(i), Slot(hero.pItemsActive[i], hero)));
                for (int i = 0; i < hero.pItemsPassive.Length; i++)
                    lines.Add(Strings.SlotLine(SlotName(2 + i), Slot(hero.pItemsPassive[i], hero)));
                if (hero.pHeroSkills != null)
                    for (int i = 0; i < hero.pHeroSkills.Length; i++)
                        lines.Add(Strings.SlotLine(Strings.SlotSkill((i + 1).ToString()), Slot(hero.pHeroSkills[i], hero)));
            }
            catch (Exception e) { CoreLog.Warning("HeroLines.Items: " + e); }
            return lines;
        }

        private static string Slot(Item item, CharacterHero hero)
            => item != null ? ItemLines.Summary(item, hero) : Strings.SlotEmpty;

        /// <summary>A slot's name by the game's combined index: 0 and 1 the hands, 2 body, 3 head,
        /// 4 trinket.</summary>
        public static string SlotName(int index)
        {
            switch (index)
            {
                case 0: return Strings.SlotHandLeft;
                case 1: return Strings.SlotHandRight;
                case 2: return Strings.SlotBody;
                case 3: return Strings.SlotHead;
                default: return Strings.SlotTrinket;
            }
        }
    }
}
