using System;
using System.Collections.Generic;
using System.Text;
using PixelHeroesAccess.Core;
using UnityEngine;

namespace PixelHeroesAccess.Module.Interop
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The game's text as speech reads it. Every word comes from the game's own tables (already in the
    /// player's language): <see cref="Tr"/> for the GUI and message texts (<see cref="DataTexts"/>, ids
    /// in <see cref="ET"/>), the data classes for names. What the game draws needs three fixes:
    /// <list type="bullet">
    /// <item>Icons: the font draws characters from U+00C0 up as pictures (<see cref="FontIcon"/>: the
    /// five elements, the six hero values, a clock, sixteen statuses, physical and magical damage,
    /// heal, range). Each reads as the game's own word for it.</item>
    /// <item>'|' is a thin spacer (icons and numbers are padded with it, and
    /// <see cref="FontIcon.ExtractFontIcons"/> replaces an icon in a text by a run of them while it
    /// draws the icon in a mesh of its own): a run reads as one space.</item>
    /// <item>'~' is the data files' line break, '\n' the meshes': both end a line.</item>
    /// </list>
    /// </summary>
    public static class GameText
    {
        /// <summary>A GUI or message text by id, as the game has it in the current language; null when
        /// the table is not loaded yet.</summary>
        public static string Tr(ET id)
        {
            try { return DataTexts.GetData(id); }
            catch (Exception e)
            {
                CoreLog.Warning("GameText: " + id + " unavailable: " + e.Message);
                return null;
            }
        }

        /// <summary>A text as one spoken line: icons as words, spacers and breaks as spaces.</summary>
        public static string Line(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var sb = new StringBuilder(raw.Length + 16);
            bool space = false;
            foreach (char c in raw)
            {
                if (c == '|' || c == '~' || c == '\n' || c == '\r' || c == ' ' || c == '\t')
                {
                    space = true;
                    continue;
                }
                string word = IconWord(c);
                if (space && sb.Length > 0) sb.Append(' ');
                space = false;
                if (word != null)
                {
                    // An icon between words reads as its own word.
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    sb.Append(word);
                    space = true;
                }
                else sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>A text as lines, split where the game breaks it ('~' and '\n'), each cleaned, the
        /// blank ones dropped.</summary>
        public static List<string> Lines(string raw)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(raw)) return lines;
            foreach (var part in raw.Split('~', '\n'))
            {
                string line = Line(part);
                if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
            }
            return lines;
        }

        /// <summary>The game's word for an icon character, or null when the character is no icon.</summary>
        public static string IconWord(char c)
        {
            if (c < 'À' || c > 'ã') return null;
            switch (c)
            {
                case 'À': return Tr(ET.GUI_HL_T_Elements_Neutral);
                case 'Á': return Tr(ET.GUI_HL_T_Elements_Phoenix);
                case 'Â': return Tr(ET.GUI_HL_T_Elements_Griffon);
                case 'Ã': return Tr(ET.GUI_HL_T_Elements_Wyrm);
                case 'Å': return Tr(ET.GUI_HL_T_Elements_Leviathan);
                case 'Æ': return ValueName(eHeroValues.PWR);
                case 'Ç': return ValueName(eHeroValues.ACC);
                case 'È': return ValueName(eHeroValues.MAG);
                case 'É': return ValueName(eHeroValues.SPR);
                case 'Ê': return ValueName(eHeroValues.DefPhy);
                case 'Ë': return ValueName(eHeroValues.DefMag);
                case 'Ì': return Tr(ET.GUI_HL_T_Items_CooldownTitle);
                case 'à': return Tr(ET.ItemDisplay_DmgPhy);
                case 'á': return Tr(ET.ItemDisplay_DmgMag);
                case 'â': return Tr(ET.ItemDisplay_Heal);
                case 'ã': return Tr(ET.ItemDisplay_Range);
            }
            for (int i = 1; i < (int)eStatusEffects.COUNT; i++)
            {
                var status = (eStatusEffects)i;
                if (StatusIcon(status) == c) return StatusName(status);
            }
            return null;
        }

        // The status icons as FontIcon.Init lays them out (one code skipped after 00D5 and 00DB).
        private static char StatusIcon(eStatusEffects status)
        {
            int i = (int)status;
            int code = 0xCC + i;
            if (i >= 10) code++;
            if (i >= 15) code++;
            return (char)code;
        }

        /// <summary>A status effect's name as the game has it.</summary>
        public static string StatusName(eStatusEffects status)
        {
            try { return DataStatusEffects.GetStatusEffectName(status); }
            catch (Exception e)
            {
                CoreLog.Warning("GameText: status " + status + " unavailable: " + e.Message);
                return status.ToString();
            }
        }

        /// <summary>A derived hero value's name (Power, Accuracy, Magic, Spirit, the defenses): the
        /// help page's line for it ("PWR: Power. For physical damage, mostly melee.") from its colon to
        /// the end of its first clause.</summary>
        public static string ValueName(eHeroValues value)
        {
            ET id;
            switch (value)
            {
                case eHeroValues.PWR: id = ET.GUI_HL_T_Heroes_DerivedPWR; break;
                case eHeroValues.ACC: id = ET.GUI_HL_T_Heroes_DerivedACC; break;
                case eHeroValues.MAG: id = ET.GUI_HL_T_Heroes_DerivedMAG; break;
                case eHeroValues.SPR: id = ET.GUI_HL_T_Heroes_DerivedSPR; break;
                case eHeroValues.DefPhy: id = ET.GUI_HL_T_Heroes_DerivedDefPhy; break;
                default: id = ET.GUI_HL_T_Heroes_DerivedDefMag; break;
            }
            return Term(Tr(id)) ?? value.ToString();
        }

        /// <summary>A hero attribute's name (Life, Strength, Dexterity, Intelligence, Faith), from the
        /// help page's line for it ("LIF: Life, affects HP.").</summary>
        public static string AttributeName(eHeroAttributes attribute)
        {
            ET id;
            switch (attribute)
            {
                case eHeroAttributes.LIF: id = ET.GUI_HL_T_Heroes_AttributesLIF; break;
                case eHeroAttributes.STR: id = ET.GUI_HL_T_Heroes_AttributesSTR; break;
                case eHeroAttributes.DEX: id = ET.GUI_HL_T_Heroes_AttributesDEX; break;
                case eHeroAttributes.INT: id = ET.GUI_HL_T_Heroes_AttributesINT; break;
                default: id = ET.GUI_HL_T_Heroes_AttributesFAI; break;
            }
            return Term(Tr(id)) ?? attribute.ToString();
        }

        /// <summary>The help line's full explanation of a hero value or attribute, as one line.</summary>
        public static string ValueHelp(eHeroValues value)
        {
            switch (value)
            {
                case eHeroValues.PWR: return Line(Tr(ET.GUI_HL_T_Heroes_DerivedPWR));
                case eHeroValues.ACC: return Line(Tr(ET.GUI_HL_T_Heroes_DerivedACC));
                case eHeroValues.MAG: return Line(Tr(ET.GUI_HL_T_Heroes_DerivedMAG));
                case eHeroValues.SPR: return Line(Tr(ET.GUI_HL_T_Heroes_DerivedSPR));
                case eHeroValues.DefPhy: return Line(Tr(ET.GUI_HL_T_Heroes_DerivedDefPhy));
                default: return Line(Tr(ET.GUI_HL_T_Heroes_DerivedDefMag));
            }
        }

        // "PWR: Power. For physical..." -> "Power": after the colon, to the first period or comma.
        private static string Term(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int colon = text.IndexOf(':');
            string rest = colon >= 0 ? text.Substring(colon + 1) : text;
            int end = rest.IndexOfAny(new[] { '.', ',', '~', (char)10 });
            if (end > 0) rest = rest.Substring(0, end);
            rest = rest.Replace("|", "").Trim();
            return rest.Length > 0 ? rest : null;
        }

        /// <summary>A text mesh's text as one line, or null when the mesh is missing or hidden.</summary>
        public static string Of(tk2dTextMesh mesh)
        {
            if (mesh == null || !mesh.gameObject.activeInHierarchy) return null;
            string line = Line(mesh.text);
            return string.IsNullOrWhiteSpace(line) ? null : line;
        }

        /// <summary>Every shown text under an object in draw order (the hierarchy's), shadows skipped,
        /// as one line.</summary>
        public static string Under(Component root)
        {
            if (root == null) return null;
            var parts = new List<string>();
            foreach (var mesh in root.GetComponentsInChildren<tk2dTextMesh>(false))
            {
                if (IsShadow(mesh)) continue;
                string t = Of(mesh);
                if (t != null) parts.Add(t);
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        /// <summary>A text mesh the game made as another's drop shadow (<see cref="ShadowTextMesh"/>).</summary>
        public static bool IsShadow(Component c)
            => c != null && (c.name == Shadow.pSHADOW_NAME || c.name == Shadow.pSHADOW_NAME_TEXTMESH);
    }
}
