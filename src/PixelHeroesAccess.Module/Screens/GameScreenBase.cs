using System.Collections.Generic;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;
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
        /// (each its own screen of ours, or the generic one), or a shop about to open over it.</summary>
        protected virtual bool Covered(T game)
            => Popup.GetTopMostActivePopup() != null || Shop.pActiveShop != null || Talkbox.pActiveTalkbox != null
                || GameScreens.ShopPending();

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Back());
        }

        /// <summary>The scene's pause button, or null where it has none (the title menu, the arena's
        /// own screens, those whose cancel already leaves).</summary>
        protected virtual tk2dButton PauseButton(T game) => null;

        /// <summary>Whether the game's cancel has something to do in the scene as it stands. Over the
        /// uncovered scene most cancels have nothing left (they close popups, which cover it).</summary>
        protected virtual bool CancelActs(T game) => false;

        /// <summary>Escape: the game's cancel; where that has nothing to do, the pause button, as the
        /// pad's Start does on these scenes.</summary>
        protected void Back()
        {
            var game = Game;
            var pause = game != null && !CancelActs(game) ? PauseButton(game) : null;
            if (GameNodes.Usable(pause)) pause.TriggerClick();
            else GenericScreen.Cancel();
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
