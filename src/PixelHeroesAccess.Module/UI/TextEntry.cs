namespace PixelHeroesAccess.Module.UI
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// A text field the player is typing into (the hero rename popup): while it owns the keyboard the
    /// mod's keys stand down (<see cref="Core.Input.InputManager.TextFieldFocused"/>). Nothing types yet.
    /// </summary>
    internal static class TextEntry
    {
        public static bool OwnsKeyboard => false;

        public static void Tick()
        {
        }

        public static void Shutdown()
        {
        }
    }
}
