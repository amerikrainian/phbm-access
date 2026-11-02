using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The game's own help (<see cref="PopupHelp"/>, every screen's Help button): four tabs, Status
    /// Effects, Elements, Items, Heroes (<see cref="TabView"/>; a tab button selects on landing). The
    /// page is read from the game's sources, never its meshes (which hold spacers where the icons
    /// were): the statuses in the game's order, each "Burning, 4 turns: Causes constant damage."
    /// (<see cref="DataStatusEffects"/>) then Redemption; the elements by name with what each beats and
    /// loses to (the game draws it as a diagram: <see cref="GameCore.CalculateElementRelationSourceToTarget"/>)
    /// and that the same element absorbs; the items' cooldown, range, damage and status texts; the
    /// heroes' five attributes and six combat values.
    /// </summary>
    public sealed class GameHelpScreen : PopupScreen
    {
        public override string Key => "game.help";

        private static PopupHelp Help => ScreenBase.pCurrentInstance != null ? ScreenBase.pCurrentInstance.v_Popup_Help : null;

        protected override Popup Resolve() => Help;

        private static readonly eStatusEffects[] Order =
        {
            eStatusEffects.Burning, eStatusEffects.Poisoned, eStatusEffects.Bleeding, eStatusEffects.Infested,
            eStatusEffects.Hard, eStatusEffects.Soft, eStatusEffects.Witty, eStatusEffects.Dull,
            eStatusEffects.Blessed, eStatusEffects.Sick, eStatusEffects.Frenzied,
            eStatusEffects.Concealed, eStatusEffects.Cursed,
            eStatusEffects.Stunned, eStatusEffects.Confused, eStatusEffects.Barreled,
        };

        public override void Build(GraphBuilder b)
        {
            var help = Help;
            var tabs = help != null ? help.GetComponent<TabView>() : null;
            if (help == null || tabs == null) return;
            b.PushContext(null, null);
            var back = GameScreens.LiveButtons(help.transform).Find(x => System.Array.IndexOf(tabs.v_Buttons, x) < 0);
            if (back != null) b.AddItem(ControlId.ForObject(back), GameNodes.ForButton(back));
            b.StartRow("help:tabs");
            for (int i = 0; i < tabs.v_Buttons.Length; i++)
            {
                int index = i;
                var button = tabs.v_Buttons[i];
                if (button == null) continue;
                b.AddItem(ControlId.Structural("help:tab:" + index), GameNodes.Tab(button, () => tabs.GetCurrentTabIndex() == index));
            }
            b.EndRow();
            int current = tabs.GetCurrentTabIndex();
            var lines = Page(help, tabs, current);
            for (int l = 0; l < lines.Count; l++)
            {
                string line = lines[l];
                b.AddItem(ControlId.Structural("help:" + current + ":" + l), GameNodes.Text(() => line));
            }
            b.PopContext();
        }

        private static List<string> Page(PopupHelp help, TabView tabs, int index)
        {
            string caption = index < tabs.v_Buttons.Length ? GameNodes.LabelOf(tabs.v_Buttons[index]) : null;
            if (index < tabs.v_Tabs.Length && help.v_Tab_StatusEffects != null && help.v_Tab_StatusEffects.transform.IsChildOf(tabs.v_Tabs[index])) return Statuses();
            if (caption == GameText.Tr(ET.GUI_HL_B_Elements)) return Elements();
            if (caption == GameText.Tr(ET.GUI_HL_B_Items)) return Items();
            if (caption == GameText.Tr(ET.GUI_HL_B_Heroes)) return Heroes();
            return Statuses();
        }

        private static List<string> Statuses()
        {
            var lines = new List<string>();
            var listed = new HashSet<eStatusEffects>(Order);
            var all = new List<eStatusEffects>(Order);
            for (int i = 1; i < (int)eStatusEffects.COUNT; i++)
                if (!listed.Contains((eStatusEffects)i)) all.Add((eStatusEffects)i);
            foreach (var status in all)
            {
                var data = DataStatusEffects.GetData(status);
                if (data == null) continue;
                string head = data.pLifetime > 0 ? Strings.StatusTurns(GameText.Line(data.pName), data.pLifetime.ToString()) : GameText.Line(data.pName);
                lines.Add(head + ": " + GameText.Line(data.pDescription));
            }
            lines.Add(GameText.Tr(ET.GUI_HL_T_Redemption_Name) + ": " + GameText.Line(GameText.Tr(ET.GUI_HL_T_Redemption_Text)));
            return lines;
        }

        private static readonly eElements[] Elemental = { eElements.Phoenix, eElements.Griffon, eElements.Wyrm, eElements.Leviathan };

        private static List<string> Elements()
        {
            var lines = new List<string> { GameText.Line(GameText.Tr(ET.GUI_HL_T_Elements_Title)) };
            foreach (var source in Elemental)
            {
                string beats = null, loses = null;
                foreach (var target in Elemental)
                {
                    var relation = GameCore.CalculateElementRelationSourceToTarget(source, target);
                    if (relation == eElementRelations.Strong) beats = ItemLines.ElementName(target);
                    else if (relation == eElementRelations.Weak) loses = ItemLines.ElementName(target);
                }
                lines.Add(Strings.HelpElement(ItemLines.ElementName(source), beats ?? "-", loses ?? "-"));
            }
            lines.Add(Strings.HelpElementSame(GameText.Tr(ET.ElementRelation_Absorbed)));
            lines.Add(Strings.HelpElementNeutral(ItemLines.ElementName(eElements.Neutral)));
            return lines;
        }

        private static List<string> Items()
        {
            return new List<string>
            {
                GameText.Line(GameText.Tr(ET.GUI_HL_T_Items_CooldownTitle) + ": " + GameText.Tr(ET.GUI_HL_T_Items_CooldownText)),
                GameText.Line(GameText.Tr(ET.GUI_HL_T_Items_RangeTitle) + ": " + GameText.Tr(ET.GUI_HL_T_Items_RangeText)),
                GameText.Line(GameText.Tr(ET.GUI_HL_T_Items_DamageTitle) + ": " + GameText.Tr(ET.GUI_HL_T_Items_DamageText1) + " " + GameText.Tr(ET.GUI_HL_T_Items_DamageText2)
                    + " " + GameText.Tr(ET.GUI_HL_T_Items_DamageText3) + " " + GameText.Tr(ET.GUI_HL_T_Items_DamageText4)),
                GameText.Line(GameText.Tr(ET.GUI_HL_T_Items_StatusEffectsTitle) + ": " + GameText.Tr(ET.GUI_HL_T_Item_StatusEffects) + " " + GameText.Tr(ET.GUI_HL_T_Items_StatusEffectsText)),
            };
        }

        private static List<string> Heroes()
        {
            var lines = new List<string> { GameText.Line(GameText.Tr(ET.GUI_HL_T_Heroes_AttributesTitle)) };
            foreach (var id in new[] { ET.GUI_HL_T_Heroes_AttributesLIF, ET.GUI_HL_T_Heroes_AttributesSTR, ET.GUI_HL_T_Heroes_AttributesDEX, ET.GUI_HL_T_Heroes_AttributesINT, ET.GUI_HL_T_Heroes_AttributesFAI })
                lines.Add(GameText.Line(GameText.Tr(id)));
            lines.Add(GameText.Line(GameText.Tr(ET.GUI_HL_T_Heroes_DerivedTitle)));
            foreach (var id in new[] { ET.GUI_HL_T_Heroes_DerivedPWR, ET.GUI_HL_T_Heroes_DerivedACC, ET.GUI_HL_T_Heroes_DerivedMAG, ET.GUI_HL_T_Heroes_DerivedSPR, ET.GUI_HL_T_Heroes_DerivedDefPhy, ET.GUI_HL_T_Heroes_DerivedDefMag })
                lines.Add(GameText.Line(GameText.Tr(id)));
            return lines;
        }
    }
}
