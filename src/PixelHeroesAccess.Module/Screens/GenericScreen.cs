using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
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
    /// The fallback over any game screen no dedicated screen claims: the buttons a player could press
    /// right now (under the topmost popup, else the open shop, else the scene's screen:
    /// <see cref="GameScreens.InputRoot"/>), each read by its own texts, in reading order, pressed as
    /// the game's gamepad presses them. Escape is the game's own cancel
    /// (<see cref="ScreenBase.BaseTriggerEventControllerInputButtonCancelDown"/>: the shop's back, the
    /// topmost popup's cancel, the talk box, else the screen's own). Lowest layer: any dedicated screen
    /// sits above it and wins.
    /// </summary>
    public sealed class GenericScreen : Screen
    {
        public override string Key => "game.generic";
        public override int Layer => -10;

        public override bool IsActive() => ScreenBase.pCurrentInstance != null;

        public override void Build(GraphBuilder b)
        {
            var root = GameScreens.InputRoot();
            var buttons = GameScreens.LiveButtons(root);
            if (buttons.Count == 0) return;
            b.PushContext(null, null);
            foreach (var button in buttons)
                b.AddItem(ControlId.ForObject(button), GameNodes.Button(button));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Cancel());
        }

        internal static void Cancel()
        {
            var screen = ScreenBase.pCurrentInstance;
            if (screen != null) screen.BaseTriggerEventControllerInputButtonCancelDown();
        }
    }
}
