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
    /// A treasure room (<see cref="RoomTreasure"/>): a chest behind an obstacle that tests one
    /// attribute. While the party decides (<see cref="RoomTreasure.eStates.ePlayerDecision"/>): the
    /// obstacle, the difficulty ("Hard! This tests Strength!"), the message line, then a control per
    /// living hero to try it, "Ghada, Strength 8, 45% chance" (the game's own chance,
    /// <see cref="RoomTreasure.CalculateChance"/>; a failure hurts that hero), then Destroy (everyone
    /// takes damage, a smaller reward) and Leave. As an attempt plays, the message line is said each
    /// time it changes (who tries, the success or failure text). The reward is the loot screen.
    /// </summary>
    public sealed class TreasureScreen : SceneScreen<RoomTreasure>
    {
        private string _message;

        public override string Key => "game.treasure";

        public override void OnPop() => _message = null;

        public override void OnUpdate()
        {
            var room = Game;
            if (room == null) return;
            string message = GameText.Of(room.v_Textmesh_Message);
            if (message == _message) return;
            _message = message;
            if (!string.IsNullOrWhiteSpace(message) && room.mState != RoomTreasure.eStates.ePlayerDecision && Navigation.FocusActive())
                Speech.Say(message);
        }

        public override void Build(GraphBuilder b)
        {
            var room = Game;
            if (room == null || room.mState != RoomTreasure.eStates.ePlayerDecision) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("treasure:obstacle"), GameNodes.Text(() => GameText.Of(Game?.v_Textmesh_Obstacle)));
            b.AddItem(ControlId.Structural("treasure:difficulty"), GameNodes.Text(() => GameText.Of(Game?.v_Textmesh_Difficulty)));
            b.AddItem(ControlId.Structural("treasure:message"), GameNodes.Text(() => GameText.Of(Game?.v_Textmesh_Message)));
            ControlId first = null;
            for (int i = 0; i < room.pHeroes.Length && i < room.v_HeroButtons.Length; i++)
            {
                var hero = room.pHeroes[i];
                var button = room.v_HeroButtons[i];
                if (hero == null || !hero.IsAlive() || !GameNodes.IsShown(button)) continue;
                int index = i;
                var vt = HeroNodes.Hero(() => Game?.pHeroes[index], () => TryLabel(index), () => Try(index));
                var id = ControlId.Structural("treasure:try:" + index);
                if (first == null) first = id;
                b.AddItem(id, vt);
            }
            if (room.v_Container_Decision != null)
                foreach (var button in GameScreens.LiveButtons(room.v_Container_Decision.transform))
                    b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            if (room.v_MenuButtons != null)
                foreach (var button in room.v_MenuButtons)
                    if (GameNodes.IsShown(button)) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            if (first != null) b.SetStart(ControlId.Structural("treasure:obstacle"));
            b.PopContext();
        }

        // "Ghada, Strength 8, 45% chance, HP 90 of 96".
        private static string TryLabel(int index)
        {
            var room = Game;
            var hero = room?.pHeroes[index];
            if (hero == null) return null;
            var attribute = room.mTestAttribute;
            int chance = Globals.RoundToInt(Globals.PercentFromFloat(room.CalculateChance(hero)));
            return string.Join(", ", new[]
            {
                GameText.Line(hero.GetName()),
                GameText.AttributeName(attribute) + " " + hero.GetAttribute(attribute),
                Strings.TreasureChance(chance.ToString()),
                HeroLines.Health(hero),
            });
        }

        private static void Try(int index)
        {
            var room = Game;
            if (room == null || room.mState != RoomTreasure.eStates.ePlayerDecision) return;
            var button = room.v_HeroButtons[index];
            GameNodes.Click(button);
        }
    }
}
