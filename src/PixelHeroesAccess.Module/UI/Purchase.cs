using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Screens;

namespace PixelHeroesAccess.Module.UI
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// What a purchase or a sale says, everywhere gold changes hands: the gold, then, as an
    /// announcement of its own, the control focus now rests on (the same potion with one more
    /// carried, the next item where the one bought left the list, the re-roll at its new price).
    /// What was bought is not said: the player chose it.
    /// </summary>
    internal static class Purchase
    {
        /// <summary>The gold, then the focused control.</summary>
        public static void Said()
        {
            Speech.Say(TavernScreen.GoldLine(), interrupt: true);
            Navigation.AnnounceFocused();
        }

        /// <summary>Press a button whose click spends gold (a drink, an arena bonus, the re-roll), and
        /// say <see cref="Said"/> once the game has taken the gold. Silent when the click spent
        /// nothing: the game refused it, and the button says so as disabled.</summary>
        public static void Press(tk2dButton button)
        {
            if (!GameNodes.Usable(button)) return;
            var session = GameCore.pGameSession;
            int before = session != null ? session.GetGold() : 0;
            button.TriggerClick();
            Later.Frames(2, () =>
            {
                var now = GameCore.pGameSession;
                if (now != null && now.GetGold() < before) Said();
            }, "purchase");
        }
    }
}
