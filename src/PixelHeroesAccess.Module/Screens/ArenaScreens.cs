using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The arena's start (<see cref="ArenaStartup"/>): New Game, Continue, Exit; the sessions played,
    /// the highest and the average round reached, each with the game's own label; then a tombstone per
    /// arena session, "round 12", newest first; landing on one selects it as its click does, and its
    /// heroes' and the session's statistics are the control buffer.
    /// </summary>
    public sealed class ArenaStartupScreen : SceneScreen<ArenaStartup>
    {
        public override string Key => "game.arena.start";

        public override void Build(GraphBuilder b)
        {
            var arena = Game;
            if (arena == null) return;
            b.PushContext(null, null);
            foreach (var button in GameScreens.LiveButtons(arena.transform))
            {
                if (GameScreens.IsFigure(button) || button.GetComponentInParent<Scrollbar>() != null) continue;
                if (arena.mTombstoneButtons != null && arena.mTombstoneButtons.Contains(button)) continue;
                b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            }
            Stat(b, "arena:sessions", ET.GUI_AR_T_SessionCount, () => Game?.v_Text_SessionCount);
            Stat(b, "arena:highest", ET.GUI_AR_T_HighestRound, () => Game?.v_Text_HighestRound);
            Stat(b, "arena:average", ET.GUI_AR_T_AverageRound, () => Game?.v_Text_AverageRound);
            if (arena.mTombstoneButtons != null)
                foreach (var stone in arena.mTombstoneButtons)
                {
                    if (stone == null) continue;
                    var button = stone;
                    var vt = GameNodes.Button(() => Strings.ArenaTombstone(GameText.Under(button)), () => Game?.EventSelectTombstone(button));
                    vt.OnFocus = () => Game?.EventSelectTombstone(button);
                    vt.Details = () => Panel(Game);
                    b.AddItem(ControlId.ForObject(button), vt);
                }
            b.PopContext();
        }

        private static void Stat(GraphBuilder b, string id, ET label, System.Func<tk2dTextMesh> value)
            => b.AddItem(ControlId.Structural(id), GameNodes.Text(() => Strings.ItemPair(GameText.Line(GameText.Tr(label)), GameText.Of(value()))));

        private static IEnumerable<string> Panel(ArenaStartup arena)
        {
            var lines = new List<string>();
            if (arena == null) return lines;
            if (arena.v_Heroes != null)
                foreach (var hero in arena.v_Heroes)
                {
                    var stats = StatsLines.Hero(hero);
                    if (stats.Count > 0) lines.Add(string.Join(". ", stats));
                }
            lines.AddRange(StatsLines.Run(arena.v_StatisticsDisplay));
            return lines;
        }
    }

    /// <summary>
    /// The arena's party creation (<see cref="ArenaPartyCreation"/>), the tavern's hiring with every
    /// hero class standing on three floors: the heroes (<see cref="ArenaPartyCreation.mHeroesTavern"/>,
    /// "name, race class", "hired" once chosen; Enter opens the game's hero card, which is
    /// <see cref="ArenaHeroCardScreen"/>), the party of up to three (Enter opens the card, Backspace
    /// dismisses the hero, as the game's cross beside it does), then Start once three are chosen,
    /// Help and Cancel.
    /// </summary>
    public sealed class ArenaPartyScreen : SceneScreen<ArenaPartyCreation>
    {
        public override string Key => "game.arena.party";

        public override void Build(GraphBuilder b)
        {
            var party = Game;
            if (party == null || party.mHeroesTavern == null) return;
            b.BeginStop("arena:heroes");
            b.PushContext(Strings.TavernCandidates);
            foreach (var candidate in party.mHeroesTavern)
            {
                if (candidate == null) continue;
                var hero = candidate;
                b.AddItem(ControlId.ForObject(hero), HeroNodes.Hero(() => hero,
                    () => TavernScreen.HeroLabel(hero) + (Game != null && Game.mHeroesSelected.Contains(hero) ? ", " + Strings.TavernHired : ""),
                    () => Game?.EventSelectHero(hero), tavern: true));
            }
            b.PopContext();

            b.BeginStop("arena:party");
            b.PushContext(Strings.TavernParty);
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                var vt = HeroNodes.Hero(() => Member(slot),
                    () => Strings.PartySlot((slot + 1).ToString(), Member(slot) != null ? TavernScreen.HeroLabel(Member(slot)) : Strings.SlotEmpty),
                    () => Open(slot), tavern: true);
                vt.OnSecondary = () => Dismiss(slot);
                b.AddItem(ControlId.Structural("arena:party:" + slot), vt);
            }
            b.PopContext();

            b.BeginStop("arena:menu");
            b.PushContext(null, null);
            foreach (var button in new[] { party.v_Button_Start, party.v_Button_Help, party.v_Button_Cancel })
                if (GameNodes.IsShown(button)) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            b.PopContext();
        }

        private static CharacterHero Member(int slot)
        {
            var party = Game;
            return party != null && party.mHeroesSelected != null && slot < party.mHeroesSelected.Count ? party.mHeroesSelected[slot] : null;
        }

        private static void Open(int slot)
        {
            var party = Game;
            if (party == null || Member(slot) == null) return;
            if (slot == 0) party.EventHeroDisplay1();
            else if (slot == 1) party.EventHeroDisplay2();
            else party.EventHeroDisplay3();
        }

        private static void Dismiss(int slot)
        {
            var party = Game;
            var hero = Member(slot);
            if (party == null || hero == null) return;
            if (slot == 0) party.EventButtonUnhire1();
            else if (slot == 1) party.EventButtonUnhire2();
            else party.EventButtonUnhire3();
            Speech.Say(Strings.ArenaDismissed(GameText.Line(hero.GetName())), interrupt: true);
        }
    }

    /// <summary>The arena's hero card (<see cref="ArenaPartyCreation.v_Popup_HeroInfo"/>), read as the
    /// tavern's (<see cref="TavernHeroCardScreen"/>): name, race and class, the per-point gains, the
    /// two skills, then Hire, Rename and Back.</summary>
    public sealed class ArenaHeroCardScreen : PopupScreen
    {
        public override string Key => "arena.card";

        private static ArenaPartyCreation Game => GameScreens.Current<ArenaPartyCreation>();

        protected override Popup Resolve() => Game != null ? Game.v_Popup_HeroInfo : null;

        public override void Build(GraphBuilder b)
        {
            var party = Game;
            var hero = party != null ? party.mCurrentPopupHero : null;
            if (hero == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("acard:head"), HeroNodes.Hero(() => Game?.mCurrentPopupHero, () => TavernScreen.HeroLabel(Game?.mCurrentPopupHero), tavern: true));
            b.AddItem(ControlId.Structural("acard:perpoint"), GameNodes.Text(() => HeroLines.PerPoint(Game?.mCurrentPopupHero)));
            for (int i = 0; i < hero.pHeroSkills.Length; i++)
            {
                int index = i;
                b.AddItem(ControlId.Structural("acard:skill:" + index), HeroNodes.Item(
                    () => Game?.mCurrentPopupHero?.pHeroSkills[index], () => Game?.mCurrentPopupHero,
                    () => { if (index == 0) Game?.EventSelectSkill1(); else Game?.EventSelectSkill2(); },
                    () => Strings.SlotLine(Strings.SlotSkill((index + 1).ToString()), ItemLines.Summary(Game?.mCurrentPopupHero?.pHeroSkills[index], Game?.mCurrentPopupHero))));
            }
            foreach (var button in GameScreens.LiveButtons(party.v_Popup_HeroInfo.transform))
            {
                if (button.GetComponentInParent<Item>() != null || button.gameObject.name.StartsWith("SelectorSkill")) continue;
                b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            }
            b.PopContext();
        }
    }

    /// <summary>
    /// The arena's hub between fights (<see cref="ArenaHub"/>): the round, the gold, the next enemies
    /// (by name, as their figures stand), the bonuses on offer (each "name: description, price", or the
    /// game's "active" once bought; Enter buys one as its price button does), then the game's buttons:
    /// re-roll the enemies (with its price), the blacksmith, the library and the temple, the inventory,
    /// start the fight, pause. The shops are <see cref="ShopScreen"/>.
    /// </summary>
    public sealed class ArenaHubScreen : SceneScreen<ArenaHub>
    {
        public override string Key => "game.arena.hub";

        public override void Build(GraphBuilder b)
        {
            var hub = Game;
            if (hub == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("hub:round"), GameNodes.Text(() => GameText.Of(Game?.v_TextMesh_BattleNumber)));
            b.AddItem(ControlId.Structural("hub:gold"), GameNodes.Text(TavernScreen.GoldLine));
            b.AddItem(ControlId.Structural("hub:enemies"), GameNodes.Text(Enemies));
            if (hub.mOfferedBoni != null)
                foreach (var offer in hub.mOfferedBoni)
                {
                    if (offer == null || offer.mButton == null || !GameNodes.IsShown(offer.mButton)) continue;
                    var bonus = offer;
                    b.AddItem(ControlId.ForObject(bonus.mButton), GameNodes.Button(() => BonusLabel(bonus),
                        () => Purchase.Press(bonus.mButton), () => GameNodes.Usable(bonus.mButton)));
                }
            foreach (var button in GameScreens.LiveButtons(hub.transform))
            {
                if (GameScreens.IsFigure(button)) continue;
                if (hub.mOfferedBoni != null && hub.mOfferedBoni.Exists(o => o.mButton == button)) continue;
                if (button == hub.v_Button_ReRoll)
                {
                    var reroll = button;
                    b.AddItem(ControlId.ForObject(button), GameNodes.Button(
                        () => GameText.Line(hub.v_Textmesh_Button_ReRoll != null ? hub.v_Textmesh_Button_ReRoll.text : GameNodes.LabelOf(reroll)),
                        () => Purchase.Press(reroll), () => GameNodes.Usable(reroll)));
                }
                else if (button == hub.v_Button_ShopBlacksmith)
                    b.AddItem(ControlId.ForObject(button), GameNodes.Button(button, () => GameText.Line(GameText.Tr(ET.Shop_Blacksmith))));
                else if (button == hub.v_Button_ShopLibrary)
                    b.AddItem(ControlId.ForObject(button), GameNodes.Button(button, () => GameText.Line(GameText.Tr(ET.Town_Building_Library))));
                else if (button == hub.v_Button_ShopTemple)
                    b.AddItem(ControlId.ForObject(button), GameNodes.Button(button, () => GameText.Line(GameText.Tr(ET.Town_Building_Temple))));
                else if (button.gameObject.name == "Button_Go")
                    b.AddItem(ControlId.ForObject(button), GameNodes.Button(button, () => Strings.ArenaFight));
                else
                    b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            }
            b.PopContext();
        }

        private static string Enemies()
        {
            var hub = Game;
            var names = new List<string>();
            if (hub != null && hub.mEnemies != null)
                foreach (var enemy in hub.mEnemies)
                    if (enemy != null) names.Add(GameText.Line(enemy.GetName()));
            return names.Count == 0 ? null : Strings.ArenaNext(string.Join(", ", names));
        }

        private static string BonusLabel(object offer)
        {
            var bonus = (ArenaHub.OfferedBonus)offer;
            string name = bonus.mData != null ? GameText.Line(bonus.mData.mDisplayName) : null;
            // The button shows the price, or the game's "active" once bought.
            string price = GameNodes.LabelOf(bonus.mButton);
            if (!string.IsNullOrEmpty(price) && char.IsDigit(price[0])) price = Strings.Cost(price);
            return GameNodes.Sentences(name, GameText.Of(bonus.mDescription), price);
        }
    }
}
