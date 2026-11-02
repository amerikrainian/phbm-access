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
    /// The tavern. At a new game (<see cref="Tavern.eTavernStates.HeroSelection"/>) it is three
    /// stops: the heroes waiting at the tables (<see cref="Tavern.mHeroesTavern"/>, each "name, race
    /// class", "hired" once in the party; Enter opens the game's hero card, where Hire is), the party
    /// of up to three (<see cref="Tavern.mHeroesSelected"/>, which the game keeps sorted by how much a
    /// hero can take: a row per slot, the hero (Enter opens the card) and its Unhire button), and the
    /// rest: the re-roll line and button, Start once three are hired, Help and Pause. On a later visit
    /// (<see cref="Tavern.eTavernStates.FreeDecision"/>) it is the game's buttons (the four drinks,
    /// Talk, Leave) and the gold. Every hero carries the hero and items buffers.
    /// </summary>
    public sealed class TavernScreen : SceneScreen<Tavern>
    {
        public override string Key => "game.tavern";

        // Live over the whole scene, so the walks in and out (the heroes to the table, the campaign's
        // NPC, entering and leaving) are quiet: nothing to press then, and nothing listed.
        public override void Build(GraphBuilder b)
        {
            var game = Game;
            if (game == null) return;
            if (game.mState == Tavern.eTavernStates.HeroSelection) BuildSelection(b, game);
            else if (game.mState == Tavern.eTavernStates.FreeDecision) BuildVisit(b, game);
        }

        private static void BuildSelection(GraphBuilder b, Tavern game)
        {
            b.BeginStop("tavern:candidates");
            b.PushContext(Strings.TavernCandidates, Strings.RoleList);
            if (game.mHeroesTavern != null)
            {
                foreach (var candidate in game.mHeroesTavern)
                {
                    if (candidate == null) continue;
                    var hero = candidate;
                    b.AddItem(ControlId.ForObject(hero), HeroNodes.Hero(() => hero,
                        () => CandidateLabel(game, hero),
                        () => game.EventSelectHero(hero), tavern: true));
                }
            }
            b.PopContext();

            b.BeginStop("tavern:party");
            b.PushContext(Strings.TavernParty, Strings.RoleList);
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                b.StartRow("tavern:party:" + slot);
                b.AddItem(ControlId.Structural("tavern:party:hero:" + slot), HeroNodes.Hero(
                    () => PartyHero(game, slot),
                    () => Strings.PartySlot((slot + 1).ToString(), PartyHero(game, slot) != null ? HeroLabel(PartyHero(game, slot)) : Strings.SlotEmpty),
                    () => OpenPartyCard(game, slot), tavern: true));
                var unhire = game.v_UnhireButtons != null && slot < game.v_UnhireButtons.Length ? game.v_UnhireButtons[slot] : null;
                if (unhire != null && PartyHero(game, slot) != null && unhire.gameObject.activeInHierarchy)
                    b.AddItem(ControlId.Structural("tavern:party:unhire:" + slot), GameNodes.Button(unhire, () => GameText.Tr(ET.GUI_TV_B_Unhire)));
                b.EndRow();
            }
            b.PopContext();

            b.BeginStop("tavern:menu");
            b.PushContext(null, null);
            var reroll = game.v_Container_Reroll;
            if (reroll != null && reroll.gameObject.activeInHierarchy)
            {
                b.AddItem(ControlId.Structural("tavern:reroll:text"), GameNodes.Text(() => GameText.Of(reroll.GetAText(0))));
                var button = reroll.GetAButton(1);
                if (button != null) b.AddItem(ControlId.Structural("tavern:reroll"), GameNodes.Button(button));
            }
            if (GameNodes.IsShown(game.v_Button_Start))
                b.AddItem(ControlId.Structural("tavern:start"), GameNodes.Button(game.v_Button_Start));
            AddIfShown(b, "tavern:help", game.v_Button_Help);
            AddIfShown(b, "tavern:pause", game.v_Button_Pause);
            b.PopContext();
        }

        private static void BuildVisit(GraphBuilder b, Tavern game)
        {
            b.PushContext(null, null);
            var decision = game.v_Container_InterfaceDecision;
            var drinks = new HashSet<tk2dButton>();
            for (int i = 0; i < 4 && decision != null; i++)
            {
                var drink = decision.GetAButton(i);
                if (drink == null) continue;
                drinks.Add(drink);
            }
            foreach (var button in GameScreens.LiveButtons(game.transform))
            {
                if (GameScreens.IsFigure(button)) continue;
                int index = DrinkIndex(decision, button);
                if (index >= 0)
                    b.AddItem(ControlId.ForObject(button), GameNodes.Button(button,
                        () => GameText.Tr((ET)((int)ET.Tavern_Drink_1 + index)) + ", " + Strings.Cost(game.GetPriceDrink(index).ToString())));
                else
                    b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            }
            b.AddItem(ControlId.Structural("tavern:gold"), GameNodes.Text(() => GoldLine()));
            b.AddItem(ControlId.Structural("tavern:drinks"), GameNodes.Text(() => GameText.Of(game.v_Textmesh_DrinkCount)));
            b.PopContext();
        }

        private static int DrinkIndex(Container decision, tk2dButton button)
        {
            for (int i = 0; i < 4 && decision != null; i++)
                if (decision.GetAButton(i) == button) return i;
            return -1;
        }

        private static void AddIfShown(GraphBuilder b, string id, tk2dButton button)
        {
            if (GameNodes.IsShown(button)) b.AddItem(ControlId.Structural(id), GameNodes.Button(button));
        }

        /// <summary>The party's gold, "Gold 120".</summary>
        public static string GoldLine()
            => GameCore.pGameSession != null ? Strings.Gold(GameCore.pGameSession.GetGold().ToString()) : null;

        private static CharacterHero PartyHero(Tavern game, int slot)
            => game.mHeroesSelected != null && slot < game.mHeroesSelected.Count ? game.mHeroesSelected[slot] : null;

        private static void OpenPartyCard(Tavern game, int slot)
        {
            if (PartyHero(game, slot) == null) return;
            if (slot == 0) game.EventHeroDisplay1();
            else if (slot == 1) game.EventHeroDisplay2();
            else game.EventHeroDisplay3();
        }

        private static string CandidateLabel(Tavern game, CharacterHero hero)
        {
            string label = HeroLabel(hero);
            return game.mHeroesSelected != null && game.mHeroesSelected.Contains(hero) ? label + ", " + Strings.TavernHired : label;
        }

        /// <summary>"Name, Race Class".</summary>
        public static string HeroLabel(CharacterHero hero)
            => GameText.Line(hero.GetName()) + ", " + GameText.Line(hero.GetNameRace() + " " + hero.GetNameClass());
    }

    /// <summary>
    /// The tavern's hero card (<see cref="Tavern.v_Popup_HeroInfo"/>): the hero's name, race and
    /// class as a heading; the backstory, a line per story (each naming the attribute it raised) then
    /// the anecdotes; what one level-up point adds to each attribute; the two skills, each an item
    /// whose facts are in the control buffer (the game's skill panel shows one at a time, chosen by
    /// clicking: Enter on a skill shows it there too); Hire (or the game's "Already hired"), Change
    /// Name, Back. The hero buffer holds the whole hero.
    /// </summary>
    public sealed class TavernHeroCardScreen : PopupScreen
    {
        public override string Key => "tavern.card";

        private static Tavern Game => GameScreens.Current<Tavern>();

        protected override Popup Resolve() => Game != null ? Game.v_Popup_HeroInfo : null;

        public override void Build(GraphBuilder b)
        {
            var game = Game;
            var hero = game != null ? game.mCurrentPopupHero : null;
            if (hero == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("card:head"), HeroNodes.Hero(() => game.mCurrentPopupHero,
                () => TavernScreen.HeroLabel(game.mCurrentPopupHero), tavern: true));
            foreach (var line in Story(game))
            {
                string text = line;
                b.AddItem(ControlId.Structural("card:story:" + text.GetHashCode()), GameNodes.Text(() => text));
            }
            b.AddItem(ControlId.Structural("card:perpoint"), GameNodes.Text(() => HeroLines.PerPoint(game.mCurrentPopupHero)));
            if (hero.pHeroSkills != null)
            {
                for (int i = 0; i < hero.pHeroSkills.Length; i++)
                {
                    int index = i;
                    b.AddItem(ControlId.Structural("card:skill:" + index), HeroNodes.Item(
                        () => game.mCurrentPopupHero != null ? game.mCurrentPopupHero.pHeroSkills[index] : null,
                        () => game.mCurrentPopupHero,
                        () => { if (index == 0) game.EventSelectSkill1(); else game.EventSelectSkill2(); },
                        () => Strings.SlotLine(Strings.SlotSkill((index + 1).ToString()),
                            ItemLines.Summary(game.mCurrentPopupHero.pHeroSkills[index], game.mCurrentPopupHero))));
                }
            }
            foreach (var button in GameScreens.LiveButtons(game.v_Popup_HeroInfo.transform))
            {
                // The skills' own click targets are the skill items above.
                if (button.GetComponentInParent<Item>() != null || button.gameObject.name.StartsWith("SelectorSkill")) continue;
                b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            }
            b.PopContext();
        }

        // The card's backstory as the game composes it (ShowHeroInfoPopup): Hack's and Ribwort's are
        // fixed texts; anyone else's is a line per attribute story, naming the attribute it raised,
        // then the anecdotes. Read from the hero, never the mesh, which carries the wrap's breaks.
        private static List<string> Story(Tavern game)
        {
            var lines = new List<string>();
            var hero = game.mCurrentPopupHero;
            if (hero == null) return lines;
            var data = hero.GetInstanceData();
            var id = data.GetData().pID;
            if (id == DataHeroes.eHeroesClasses.Hack)
            {
                lines.Add(GameText.Line(GameText.Tr(ET.Backstory_Hack_Part1) + GameText.Tr(ET.Backstory_Hack_Part2) + GameText.Tr(ET.Backstory_Hack_Part3)));
                return lines;
            }
            if (id == DataHeroes.eHeroesClasses.Ribwort)
            {
                lines.Add(GameText.Line(GameText.Tr(ET.Backstory_Ribwort_Part1) + GameText.Tr(ET.Backstory_Ribwort_Part2) + GameText.Tr(ET.Backstory_Ribwort_Part3)));
                return lines;
            }
            if (data.pStoriesAttributes != null)
                foreach (var story in data.pStoriesAttributes)
                    if (story != null)
                        lines.Add(GameText.Line(Language.Genderify(story.pStory, hero.GetGender())) + " (" + GameText.AttributeName(story.pAttribute) + "+)");
            if (data.pStoriesRandom != null)
                foreach (var anecdote in data.pStoriesRandom)
                    if (!string.IsNullOrWhiteSpace(anecdote)) lines.Add(GameText.Line(anecdote));
            return lines;
        }
    }
}
