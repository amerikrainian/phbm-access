namespace PixelHeroesAccess.Dev
{
    /// <summary>
    /// The dev /input fallback while no module is loaded. The game's own keyboard-free navigation is its
    /// gamepad cursor over <c>tk2dButton</c>s; with no module there is nothing of ours to drive, so the
    /// one verb offered is "confirm": the gamepad target's <c>TriggerClick</c>, as the pad's A press does.
    /// </summary>
    internal static class InputInjector
    {
        public static string Inject(string verb)
        {
            string v = (verb ?? "").Trim().ToLowerInvariant();
            if (v != "confirm")
                return "[no module] only 'confirm' (the game's gamepad target) works without the module\n";
            var screen = ScreenBase.pCurrentInstance;
            var target = screen != null && screen.pInputGamepad != null ? screen.pInputGamepad.GetTargetButton() : null;
            if (target == null)
                return "confirm: no gamepad target\n";
            target.TriggerClick();
            return "confirm -> " + target.name + "\n";
        }
    }
}
