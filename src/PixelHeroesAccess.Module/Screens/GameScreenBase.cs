using System.Collections.Generic;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Interop;
using Screen = PixelHeroesAccess.Core.Screens.Screen;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// A screen of ours over one game scene (<typeparamref name="T"/>, the scene's
    /// <see cref="ScreenBase"/>): live while that scene's screen is the current one and nothing covers
    /// it (no popup, no open shop, no talk box: <see cref="Covered"/>), which is exactly when the game
    /// lets its buttons be pressed. Escape is the game's own cancel unless a screen says otherwise.
    /// </summary>
    public abstract class SceneScreen<T> : Screen where T : ScreenBase
    {
        /// <summary>The live game screen, or null when another scene is up or a transition runs.</summary>
        protected static T Game => GameScreens.Current<T>();

        // A popup over the scene pops this screen; focus comes back where it was when the popup goes.
        public override bool KeepStateOnPop => true;

        public override bool IsActive()
        {
            var game = Game;
            return game != null && !Covered(game);
        }

        /// <summary>Whether something of the game's sits over the scene: a popup, the shop, the talk box
        /// (each its own screen of ours, or the generic one).</summary>
        protected virtual bool Covered(T game)
            => Popup.GetTopMostActivePopup() != null || Shop.pActiveShop != null || Talkbox.pActiveTalkbox != null;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => GenericScreen.Cancel());
        }
    }

    /// <summary>
    /// A screen of ours over one of the game's popups: live while the popup is the topmost one and done
    /// moving in (<see cref="Popup.IsActiveAndNotMoving"/>; while it slides the game blocks every
    /// button). Escape is the game's cancel (<see cref="GenericScreen.CancelPopup"/>).
    /// </summary>
    public abstract class PopupScreen : Screen
    {
        public override int Layer => 10;

        /// <summary>The popup this screen reads, or null when its scene is not up.</summary>
        protected abstract Popup Resolve();

        public override bool IsActive()
        {
            var popup = Resolve();
            return popup != null && GameScreens.IsTop(popup) && popup.IsActiveAndNotMoving();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => GenericScreen.CancelPopup(Resolve()));
        }
    }
}
