using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Battle;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// A fight (<see cref="BattleLogic"/>, its interface <see cref="BattleGUI"/>). On the heroes' turn
    /// (<see cref="BattleGUI.eBattleStates.PlayerSelectedNothing"/>) the first stop is the party, a row
    /// per hero: the hero ("name, HP, statuses", "rested" when it acted last turn, the game's rule that
    /// a hero may not act two turns running), then its two hands' items and its two skills, each
    /// "item summary, range" with a skill's cooldown. The game shows hands or skills at a time (a toggle
    /// per hero); here all four are listed and the toggle is set as needed. A stunned hero's one action
    /// is breaking free. Enter on an action selects it as clicking its slot does
    /// (<see cref="BattleGUI.SelectHand"/>): the game checks the rest and the cooldown, says why when
    /// it refuses, and otherwise shows the targets the item's range allows, which become the second
    /// stop (focus lands there): each "name, health", "all" when the action strikes every one; Enter
    /// strikes. Escape there cancels the choice. Then the enemies (name, the game's condition word,
    /// statuses; the game shows enemies' health only as Healthy, Wounded or Critical, and nothing for
    /// a boss), and the battle's buttons (Throw away, Quest log, Help, Pause) and its message line.
    /// While an action plays nothing is listed; what happens is said by <see cref="BattleEvents"/>.
    /// Backspace on a hand's item offers to throw it away for a bare fist (the game's own popup).
    /// </summary>
    public sealed class BattleScreen : SceneScreen<BattleLogic>
    {
        private BattleGUI.eBattleStates _lastState = BattleGUI.eBattleStates.COUNT;
        private BattleLogic _opened;

        // Enemies sharing a name are numbered by their place when the fight began, per enemy, so a
        // number holds when the game closes the ranks after a death. Identity, not game state.
        private static readonly Dictionary<CharacterEnemy, int> _numbers = new Dictionary<CharacterEnemy, int>();

        public override string Key => "game.battle";

        private static BattleGUI Gui => Game != null ? Game.v_BattleGUI : null;

        // The loot screen is drawn over the fight; it is its own screen.
        protected override bool Covered(BattleLogic game)
            => base.Covered(game) || (game.v_BattleGUI != null && game.v_BattleGUI.v_Lootscreen != null && game.v_BattleGUI.v_Lootscreen.IsActive());

        public override void OnPop() => _lastState = BattleGUI.eBattleStates.COUNT;

        public override void OnUpdate()
        {
            var gui = Gui;
            if (gui == null) return;
            // A new fight: who stands against the party, once, as it begins.
            if (!ReferenceEquals(_opened, Game))
            {
                _opened = Game;
                NumberEnemies();
                BattleEvents.Report(Strings.BattleBegins(EnemyNames()));
            }
            var state = gui.mBattleState;
            if (state == _lastState) return;
            var previous = _lastState;
            _lastState = state;
            if (state == BattleGUI.eBattleStates.PlayerSelectedNothing && previous != BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget)
            {
                // A new turn of the heroes': focus on the first action of the first hero who may act.
                var hero = FirstReadyHero();
                if (hero != null) Navigation.FocusNode(ActionId(hero.pHeroIndex, 0));
            }
            else if (state == BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget)
            {
                Navigation.FocusStop("battle:targets");
            }
        }

        public override void Build(GraphBuilder b)
        {
            var game = Game;
            var gui = Gui;
            if (game == null || gui == null) return;
            var state = gui.mBattleState;
            if (state != BattleGUI.eBattleStates.PlayerSelectedNothing && state != BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget) return;

            b.BeginStop("battle:party");
            b.PushContext(Strings.BattleParty, Strings.RoleList);
            for (int i = 0; i < game.pHeroes.Length; i++)
            {
                var hero = game.pHeroes[i];
                if (hero == null) continue;
                int index = i;
                // The row is the hero's: its name is said on entering it, its state on its first cell.
                b.PushContext(GameText.Line(hero.GetName()), null, positions: false);
                b.StartRow("battle:hero:" + index);
                b.AddItem(ControlId.Structural("battle:hero:" + index), HeroNodes.Hero(() => Game?.pHeroes[index], () => HeroState(Game?.pHeroes[index])));
                if (hero.IsAlive())
                {
                    if (hero.HasStatusEffect(eStatusEffects.Stunned))
                        AddAction(b, index, 0, () => Game?.pHeroes[index].pBreakFreeFromStunned, false);
                    else
                    {
                        for (int h = 0; h < hero.pItemsActive.Length; h++)
                        {
                            int hand = h;
                            AddAction(b, index, hand, () => Game?.pHeroes[index].pItemsActive[hand], false);
                        }
                        for (int s = 0; s < hero.pHeroSkills.Length; s++)
                        {
                            int skill = s;
                            AddAction(b, index, 2 + skill, () => Game?.pHeroes[index].pHeroSkills[skill], true);
                        }
                    }
                }
                b.EndRow();
                b.PopContext();
            }
            b.PopContext();

            if (state == BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget && gui.mSelectedHand != null)
            {
                b.BeginStop("battle:targets");
                b.PushContext(Strings.BattleTargets(GameText.Line(gui.mSelectedHand.GetNameFull())), Strings.RoleList);
                bool all = gui.mSelectedHand.GetRange() == eRanges.OffensiveAll || gui.mSelectedHand.GetRange() == eRanges.DefensiveAll;
                for (int k = 0; k < gui.v_Selectors.Length; k++)
                {
                    var selector = gui.v_Selectors[k];
                    if (selector == null || !selector.gameObject.activeSelf) continue;
                    int slot = k;
                    Character target = slot < 3 ? (Character)game.pHeroes[slot] : game.pEnemies[slot - 3];
                    if (target == null) continue;
                    var vt = GameNodes.Button(() => (all ? Strings.BattleAll + ", " : "") + CharacterLabel(Target(slot)), () => Strike(slot));
                    if (slot < 3) vt.SideLines = HeroNodes.Sides(() => Game?.pHeroes[slot]);
                    else vt.Details = () => EnemyLines(Target(slot) as CharacterEnemy);
                    vt.QuietVanish = true;
                    b.AddItem(ControlId.Structural("battle:target:" + slot), vt);
                }
                b.PopContext();
            }

            b.BeginStop("battle:enemies");
            b.PushContext(Strings.BattleEnemies, Strings.RoleList);
            for (int e = 0; e < game.pEnemies.Length; e++)
            {
                var enemy = game.pEnemies[e];
                if (enemy == null || !enemy.IsAlive()) continue;
                int slot = e;
                var vt = GameNodes.Text(() => CharacterLabel(Game?.pEnemies[slot]));
                vt.Details = () => EnemyLines(Game?.pEnemies[slot]);
                b.AddItem(ControlId.Structural("battle:enemy:" + slot), vt);
            }
            b.PopContext();

            b.BeginStop("battle:menu");
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("battle:message"), GameNodes.Text(() => GameText.Of(Gui?.v_Textmesh_Message)));
            foreach (var button in GameScreens.LiveButtons(game.transform))
            {
                if (!IsMenuButton(gui, button)) continue;
                b.AddItem(ControlId.ForObject(button), GameNodes.Button(button, () => MenuLabel(button)));
            }
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var gui = Gui;
                if (gui != null && gui.mBattleState == BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget)
                    gui.SetGUIToNewState(BattleGUI.eBattleStates.PlayerSelectedNothing);
                else GenericScreen.Cancel();
            });
        }

        private static void NumberEnemies()
        {
            _numbers.Clear();
            var game = Game;
            if (game == null || game.pEnemies == null) return;
            var counts = new Dictionary<string, int>();
            foreach (var enemy in game.pEnemies)
                if (enemy != null) counts[enemy.GetName()] = counts.TryGetValue(enemy.GetName(), out int n) ? n + 1 : 1;
            var seen = new Dictionary<string, int>();
            foreach (var enemy in game.pEnemies)
            {
                if (enemy == null || counts[enemy.GetName()] < 2) continue;
                int k = seen.TryGetValue(enemy.GetName(), out int m) ? m + 1 : 1;
                seen[enemy.GetName()] = k;
                _numbers[enemy] = k;
            }
        }

        /// <summary>An enemy's name, numbered when others of the fight share it ("Tiny Shroom 2").</summary>
        public static string EnemyName(Character enemy)
        {
            if (enemy == null) return null;
            string name = GameText.Line(enemy.GetName());
            return enemy is CharacterEnemy e && _numbers.TryGetValue(e, out int n) ? name + " " + n : name;
        }

        // "Swarm of Bees, Creepy Crawler, Common Scorpion".
        private static string EnemyNames()
        {
            var game = Game;
            var names = new List<string>();
            if (game != null && game.pEnemies != null)
                foreach (var enemy in game.pEnemies)
                    if (enemy != null && enemy.IsAlive()) names.Add(EnemyName(enemy));
            return string.Join(", ", names);
        }

        private static ControlId ActionId(int hero, int slot) => ControlId.Structural("battle:action:" + hero + ":" + slot);

        // An action of a hero: slot 0 and 1 the hands, 2 and 3 the skills.
        private static void AddAction(GraphBuilder b, int hero, int slot, System.Func<ItemActive> item, bool skill)
        {
            var vt = HeroNodes.Item(() => item(), () => Game?.pHeroes[hero], () => Choose(hero, slot, item(), skill),
                () => ActionLabel(Game?.pHeroes[hero], item()));
            if (!skill) vt.OnSecondary = () => OfferThrowAway(hero, slot, item());
            b.AddItem(ActionId(hero, slot), vt);
        }

        private static string ActionLabel(CharacterHero hero, ItemActive item)
        {
            if (hero == null || item == null) return null;
            var parts = new List<string> { ItemLines.Summary(item, hero) };
            parts.Add(Strings.ItemPair(GameText.Tr(ET.ItemDisplay_Range), GameText.Line(item.GetRangeAsString())));
            if (item.IsSkill())
            {
                var s = (ItemActiveSkill)item;
                if (!s.IsReadyForUse()) parts.Add(Strings.ItemCoolingDown(s.GetCooldownCurrent().ToString()));
            }
            return string.Join(", ", parts);
        }

        // Selecting an action as its slot's click does; the game refuses a resting hero and a skill still
        // cooling down with a message line, which is said.
        private static void Choose(int heroIndex, int slot, ItemActive item, bool skill)
        {
            var game = Game;
            var gui = Gui;
            if (game == null || gui == null || item == null) return;
            var hero = game.pHeroes[heroIndex];
            if (!hero.IsAlive()) return;
            if (!hero.CanActPossibly()) { Speech.Say(GameText.Tr(ET.MessageSelectedHeroHasToRest), interrupt: true); return; }
            if (skill && !((ItemActiveSkill)item).IsReadyForUse()) { Speech.Say(GameText.Tr(ET.MessageSelectedCoolingDown), interrupt: true); return; }
            if (gui.mBattleState == BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget && gui.mSelectedHand == item)
            {
                Navigation.FocusStop("battle:targets");
                return;
            }
            if (!hero.HasStatusEffect(eStatusEffects.Stunned))
            {
                var mode = skill ? BattleGUI.eHandModes.eModeSkills : BattleGUI.eHandModes.eModeItems;
                if (hero.pBattleHandDisplayMode != mode) gui.SetHandMode(hero, mode);
            }
            gui.SelectHand(heroIndex, (BattleGUI.eHandIndex)(slot % 2));
        }

        private static void OfferThrowAway(int heroIndex, int slot, ItemActive item)
        {
            var gui = Gui;
            if (gui == null || item == null) return;
            Choose(heroIndex, slot, item, false);
            if (gui.mSelectedHand == item) gui.EventButtonPopupSwitchFistShow();
        }

        // Striking a target as its selector's click does.
        private static void Strike(int slot)
        {
            var gui = Gui;
            if (gui == null) return;
            switch (slot)
            {
                case 0: gui.EventTargetHero1(); break;
                case 1: gui.EventTargetHero2(); break;
                case 2: gui.EventTargetHero3(); break;
                case 3: gui.EventTargetEnemy1(); break;
                case 4: gui.EventTargetEnemy2(); break;
                default: gui.EventTargetEnemy3(); break;
            }
        }

        private static Character Target(int slot)
        {
            var game = Game;
            if (game == null) return null;
            return slot < 3 ? (Character)game.pHeroes[slot] : game.pEnemies[slot - 3];
        }

        private static CharacterHero FirstReadyHero()
        {
            var game = Game;
            if (game == null) return null;
            foreach (var hero in game.pHeroes)
                if (hero != null && hero.IsAlive() && hero.CanActPossibly()) return hero;
            return null;
        }

        /// <summary>"Ghada, HP 30 of 45, Burning 2 turns, rested".</summary>
        public static string HeroLabel(CharacterHero hero)
            => hero == null ? null : GameText.Line(hero.GetName()) + ", " + HeroState(hero);

        /// <summary>"HP 30 of 45, Burning 2 turns, rested": a hero's state without its name.</summary>
        public static string HeroState(CharacterHero hero)
        {
            if (hero == null) return null;
            var parts = new List<string> { HeroLines.Health(hero) };
            string statuses = HeroLines.Statuses(hero);
            if (statuses != null) parts.Add(statuses);
            if (hero.IsAlive() && hero.pHasBeenUsedThisRound) parts.Add(Strings.BattleRested);
            return string.Join(", ", parts);
        }

        /// <summary>A character as a target or an enemy row: a hero by its health, an enemy by the
        /// game's condition word (or its boss line), then the statuses.</summary>
        public static string CharacterLabel(Character character)
        {
            if (character == null) return null;
            if (character is CharacterHero hero) return HeroLabel(hero);
            var parts = new List<string> { EnemyName(character), EnemyCondition(character) };
            string statuses = HeroLines.Statuses(character);
            if (statuses != null) parts.Add(statuses);
            return string.Join(", ", parts);
        }

        // The enemy info panel's word (UpdateInfoboxForEnemies): Healthy, Wounded or Critical by thirds,
        // or "No info on bosses" in a boss fight.
        private static string EnemyCondition(Character enemy)
        {
            if (GameCore.IsBoss()) return GameText.Tr(ET.Battle_EnemyInfoBoss);
            float ratio = enemy.GetHPMax() > 0 ? (float)enemy.GetHPCurrent() / enemy.GetHPMax() : 0f;
            if (ratio >= 0.66f) return GameText.Tr(ET.Battle_EnemyInfoHPHigh);
            if (ratio >= 0.33f) return GameText.Tr(ET.Battle_EnemyInfoHPMedium);
            return GameText.Tr(ET.Battle_EnemyInfoHPLow);
        }

        // An enemy's further lines: its element.
        private static IEnumerable<string> EnemyLines(CharacterEnemy enemy)
        {
            if (enemy == null) yield break;
            yield return ItemLines.ElementName(enemy.GetElement());
        }

        private static bool IsMenuButton(BattleGUI gui, tk2dButton button)
        {
            if (GameScreens.IsFigure(button)) return false;
            if (gui.mHandSlotsButtons != null && gui.mHandSlotsButtons.Contains(button)) return false;
            if (System.Array.IndexOf(gui.v_Selectors, button) >= 0) return false;
            if (System.Array.IndexOf(gui.v_ButtonsSwitchItemsSkills, button) >= 0) return false;
            // The game's log view hides the fight's interface; the log buffer holds the same lines.
            if (button.gameObject.name == "Button_SwitchLog" || button.gameObject.name == "Selector") return false;
            return true;
        }

        private static string MenuLabel(tk2dButton button)
        {
            if (button.gameObject.name == "Button_SwitchFist") return GameText.Tr(ET.GUI_BT_B_ThrowItemAway);
            return GameNodes.LabelOf(button);
        }
    }
}
