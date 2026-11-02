using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The road between the town and a dungeon (<see cref="Overland"/>). The party walks by itself
    /// and meets three events on the way, each a talk with options (<see cref="TalkScreen"/>), which may
    /// lead to a reward or a fight. While it walks this says where it is going, "to Byting Woods,
    /// event 2 of 3" (back to town on the way home), once as the road starts, and lists the menu's
    /// buttons (Quest Log, Help, Pause) with that line. The pause menu and the "break for the
    /// inventory" popup after a reward are their own screens.
    /// </summary>
    public sealed class OverlandScreen : SceneScreen<Overland>
    {
        public override string Key => "game.overland";

        protected override tk2dButton PauseButton(Overland game) => game.v_ButtonsMenu != null && game.v_ButtonsMenu.Length > 2 ? game.v_ButtonsMenu[2] : null;
        protected override bool CancelActs(Overland game)
            => (game.v_Lootscreen != null && game.v_Lootscreen.IsActive()) || (game.v_Popup_Inventory != null && game.v_Popup_Inventory.IsActiveOrMovingIn());

        public override void OnPush()
        {
            var road = Game;
            if (road != null && road.mState == Overland.eStates.eEntering) ScreenIntro.Say(Progress());
        }

        public override void Build(GraphBuilder b)
        {
            var road = Game;
            if (road == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("road:progress"), GameNodes.Text(Progress));
            if (road.v_ButtonsMenu != null)
                foreach (var button in road.v_ButtonsMenu)
                    if (GameNodes.IsShown(button)) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            b.PopContext();
        }

        /// <summary>"to Byting Woods, event 2 of 3", or "to town" on the way home.</summary>
        public static string Progress()
        {
            var road = Game;
            var session = GameCore.pGameSession;
            if (road == null || session == null) return null;
            string where = road.mMode == Overland.eModes.MoveDungeonToTown
                ? Strings.RoadToTown
                : Strings.RoadTo(GameText.Of(road.v_DestinationTitle) ?? "");
            int next = (int)session.pOverlandEventState;
            if (next >= (int)Overland.eEventStates.TowardsEvent1 && next <= (int)Overland.eEventStates.TowardsEvent3)
                return where + ", " + Strings.RoadEvent(next.ToString(), "3");
            return where;
        }
    }
}
