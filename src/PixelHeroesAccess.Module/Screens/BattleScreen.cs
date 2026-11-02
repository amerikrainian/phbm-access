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
    /// (<see cref="BattleGUI.eBattleStates.PlayerSelectedNothing"/>) the first stop is the field, two
    /// regions. The party, a row per hero from the front: the hero ("HP, statuses", "rested" when it
    /// acted last turn, the game's rule that a hero may not act two turns running), then its two
    /// hands' items and its two skills, each "item summary, range" with a skill's cooldown. The game
    /// shows hands or skills at a time (a toggle per hero); here all four are listed and the toggle is
    /// set as needed. A stunned hero's one action is breaking free. Then the enemies as one row from
    /// the front, each "1, Creepy Crawler 2, Healthy": its place (what a range counts: range 1-2 reaches the
    /// first two whoever strikes), its name, the game's condition word (the game shows enemies' health
    /// only as Healthy, Wounded or Critical, and nothing for a boss), its statuses. Enter on an action
    /// selects it as clicking its slot does (<see cref="BattleGUI.SelectHand"/>): the game checks the
    /// rest and the cooldown, says why when it refuses, and otherwise marks the targets the range
    /// allows. Then the characters on the side the action addresses lead with "target" ("all" when it
    /// strikes every one) or "out of range", focus goes to the first target, and Enter on one strikes;
    /// Escape cancels back to the action. The second stop is the message line and the battle's buttons
    /// (Throw away, Quest log, Help, Pause). While an action plays nothing is listed; what happens is
    /// said by <see cref="BattleEvents"/>. Backspace on a hand's item offers to throw it away for a
    /// bare fist (the game's own popup).
    /// </summary>
    public sealed class BattleScreen : SceneScreen<BattleLogic>
    {
        private BattleGUI.eBattleStates _lastState = BattleGUI.eBattleStates.COUNT;
        private BattleLogic _opened;
        private string _bossPage;

        // Enemies sharing a name are numbered by their place when the fight began, per enemy, so a
        // number holds when the game closes the ranks after a death. Identity, not game state.
        private static readonly Dictionary<CharacterEnemy, int> _numbers = new Dictionary<CharacterEnemy, int>();

        public override string Key => "game.battle";

        protected override tk2dButton PauseButton(BattleLogic game)
            => game.v_BattleGUI != null && game.v_BattleGUI.v_Buttons != null && game.v_BattleGUI.v_Buttons.Length > 1 ? game.v_BattleGUI.v_Buttons[1] : null;

        // BattleGUI.EventControllerInputButtonCancelDown: the loot, the throw-away popup, the log page, a
        // hand waiting for its target.
        protected override bool CancelActs(BattleLogic game)
        {
            var gui = game.v_BattleGUI;
            if (gui == null) return false;
            return (gui.v_Lootscreen != null && gui.v_Lootscreen.IsActive())
                || (gui.v_Popup_SwitchFist != null && gui.v_Popup_SwitchFist.IsActiveAndNotMoving())
                || gui.mLogScreenModus
                || gui.mBattleState == BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget;
        }

        private static BattleGUI Gui => Game != null ? Game.v_BattleGUI : null;

        // The loot screen is drawn over the fight; it is its own screen.
        protected override bool Covered(BattleLogic game)
            => base.Covered(game) || (game.v_BattleGUI != null && game.v_BattleGUI.v_Lootscreen != null && game.v_BattleGUI.v_Lootscreen.IsActive());

        public override void OnPop()
        {
            _lastState = BattleGUI.eBattleStates.COUNT;
            _bossPage = null;
        }

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
            // A boss opens with a speech bubble, page by page: each said as it comes, the boss first.
            string page = BossPage(gui);
            if (page != _bossPage)
            {
                bool first = _bossPage == null;
                _bossPage = page;
                if (page != null && Navigation.FocusActive())
                {
                    // The landing on the page would say it again.
                    if (first) Navigation.QuietNextLanding();
                    Speech.Say(first ? GameNodes.Sentences(EnemyName(Game?.GetMainBoss()), page) : page);
                }
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
                FocusFirstTarget();
            }
        }

        // The action the targets are for: Escape returns to it. Where focus was, not game state.
        private static ControlId _chosen;

        // Choosing an action marks its targets on the field; focus goes to the first on the side the
        // action addresses (the enemies for an attack, else the party).
        private static void FocusFirstTarget()
        {
            var gui = Gui;
            if (gui == null || gui.v_Selectors == null) return;
            bool offensive = gui.mSelectedHand != null && gui.mSelectedHand.IsOffensive();
            for (int k = 0; k < gui.v_Selectors.Length; k++)
            {
                if ((k >= 3) != offensive) continue;
                if (IsTarget(k) && Target(k) != null) { Navigation.FocusNode(CharacterId(k)); return; }
            }
        }

        public override void Build(GraphBuilder b)
        {
            var game = Game;
            var gui = Gui;
            if (game == null || gui == null) return;
            var state = gui.mBattleState;
            if (state == BattleGUI.eBattleStates.Intro && BossPage(gui) != null)
            {
                // The boss's speech waits for a click per page: Enter turns it, in one press.
                var bubble = GameNodes.Button(() => BossPage(Gui), () => TurnBossPage(Gui));
                bubble.ControlType = ControlTypes.Text;
                b.AddItem(ControlId.Structural("battle:boss:speech"), bubble);
                return;
            }
            if (state != BattleGUI.eBattleStates.PlayerSelectedNothing && state != BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget) return;

            // One stop: the party, a row per hero, then the enemies front to back, two regions
            // (Alt+Up/Down between them). While an action waits for its target, the characters on the
            // side it addresses say whether they are one ("target", "all", "out of range"), and Enter
            // on a target strikes it.
            bool targeting = state == BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget && gui.mSelectedHand != null;
            bool offensive = targeting && gui.mSelectedHand.IsOffensive();
            b.BeginStop("battle:field");
            b.SetRegion("battle:party");
            b.PushContext(Strings.BattleParty, Strings.RoleList);
            for (int i = 0; i < game.pHeroes.Length; i++)
            {
                var hero = game.pHeroes[i];
                if (hero == null) continue;
                int index = i;
                // The row is the hero's: its name is said on entering it, its state on its first cell.
                b.PushContext(GameText.Line(hero.GetName()), null, positions: false);
                b.StartRow("battle:hero:" + index);
                var head = HeroNodes.Hero(() => Game?.pHeroes[index], () => TargetPrefix(index, HeroState(Game?.pHeroes[index])));
                if (targeting && !offensive) head.OnActivate = () => StrikeOrRefuse(index);
                b.AddItem(CharacterId(index), head);
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

            b.SetRegion("battle:enemies");
            b.PushContext(Strings.BattleEnemies, Strings.RoleList, positions: false);
            // One row, front to back as they stand; entered on the first target while an attack waits
            // for one, else on the front enemy.
            b.StartRow("battle:enemies", FirstEnemyTarget);
            for (int e = 0; e < game.pEnemies.Length; e++)
            {
                var enemy = game.pEnemies[e];
                if (enemy == null || !enemy.IsAlive()) continue;
                int slot = 3 + e;
                var vt = GameNodes.Text(() => TargetPrefix(slot, EnemyRow(slot - 3)));
                if (offensive) vt.OnActivate = () => StrikeOrRefuse(slot);
                vt.Details = () => EnemyLines(Game?.pEnemies[slot - 3]);
                vt.QuietVanish = true;
                b.AddItem(CharacterId(slot), vt);
            }
            b.EndRow();
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
                {
                    gui.SetGUIToNewState(BattleGUI.eBattleStates.PlayerSelectedNothing);
                    if (_chosen != null) Navigation.FocusNode(_chosen);
                }
                else Back();
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

        // The boss bubble's page while it shows, else null.
        private static string BossPage(BattleGUI gui)
        {
            var bubble = gui != null ? gui.v_SpeechBubble_Boss : null;
            if (bubble == null || !bubble.IsVisible() || bubble.v_TextAnimation == null) return null;
            return GameText.Line(bubble.v_TextAnimation.mText);
        }

        private static void TurnBossPage(BattleGUI gui)
        {
            var bubble = gui != null ? gui.v_SpeechBubble_Boss : null;
            if (bubble == null || !bubble.IsVisible()) return;
            if (!bubble.v_TextAnimation.IsFinished()) bubble.v_TextAnimation.MakeFull();
            bubble.EventClick();
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
            _chosen = ActionId(heroIndex, slot);
            if (gui.mBattleState == BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget && gui.mSelectedHand == item)
            {
                FocusFirstTarget();
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

        // A character's control on the field by the game's selector slot: 0 to 2 the heroes, 3 to 5 the
        // enemies.
        private static ControlId CharacterId(int slot)
            => ControlId.Structural(slot < 3 ? "battle:hero:" + slot : "battle:enemy:" + (slot - 3));

        // Whether the chosen action may strike this slot: the game shows that slot's selector.
        private static bool IsTarget(int slot)
        {
            var gui = Gui;
            if (gui == null || gui.v_Selectors == null || slot >= gui.v_Selectors.Length) return false;
            var selector = gui.v_Selectors[slot];
            return selector != null && selector.gameObject.activeSelf;
        }

        // While an action waits for its target, the side it addresses says what each is to it: "target"
        // ("all" when it strikes every one), "out of range"; else nothing.
        private static string TargetPrefix(int slot, string label)
        {
            var gui = Gui;
            if (label == null || gui == null || gui.mBattleState != BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget || gui.mSelectedHand == null)
                return label;
            if ((slot >= 3) != gui.mSelectedHand.IsOffensive()) return label;
            var range = gui.mSelectedHand.GetRange();
            string mark = !IsTarget(slot) ? Strings.BattleOutOfRange
                : range == eRanges.OffensiveAll || range == eRanges.DefensiveAll ? Strings.BattleAll
                : Strings.BattleTarget;
            return mark + ", " + label;
        }

        // The enemies row's entry: the place in the row (living enemies only) of the first an attack
        // may strike, else the front.
        private static int FirstEnemyTarget()
        {
            var game = Game;
            var gui = Gui;
            if (game == null || gui == null || gui.mSelectedHand == null || !gui.mSelectedHand.IsOffensive()
                || gui.mBattleState != BattleGUI.eBattleStates.PlayerSelectedHandNeedsTarget) return 0;
            int place = 0;
            for (int e = 0; e < game.pEnemies.Length; e++)
            {
                var enemy = game.pEnemies[e];
                if (enemy == null || !enemy.IsAlive()) continue;
                if (IsTarget(3 + e)) return place;
                place++;
            }
            return 0;
        }

        private static void StrikeOrRefuse(int slot)
        {
            if (IsTarget(slot)) Strike(slot);
            else Speech.Say(Strings.BattleOutOfRange, interrupt: true);
        }

        // "1, Creepy Crawler 2, Healthy": the enemy's place from the front (what a range counts), then
        // its name, condition and statuses.
        private static string EnemyRow(int index)
        {
            var enemy = Game?.pEnemies[index];
            return enemy == null ? null : (index + 1) + ", " + CharacterLabel(enemy);
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

        // An enemy's further lines: the help's entries for the statuses on it. Its element is not
        // given: the game never shows it (a sighted player learns it from Strong, Weak and Absorbed on
        // a hit, which the narration says too).
        private static IEnumerable<string> EnemyLines(CharacterEnemy enemy)
        {
            if (enemy == null) return new string[0];
            return new Definitions().StatusesOn(enemy).Lines;
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
