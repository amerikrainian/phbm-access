using HarmonyLib;

namespace PixelHeroesAccess.Module.UI
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// When active, our navigation owns the keyboard and the gamepad. The game reads no keyboard for
    /// navigation at all (only the hero rename popup types, through the Input System's
    /// <c>Keyboard.current</c>, which the rename screen handles itself), so only its gamepad needs
    /// silencing: its per-frame <see cref="InputGamepad.HandleControllerInput"/> (the cursor over the
    /// <c>tk2dButton</c>s, A to click, B to cancel, Start to go back) is skipped while focus mode is on,
    /// its cursor hidden. The pads are still polled, for our controller bindings
    /// (<see cref="Input.Gamepad"/>). The mouse is left alone. Turning focus mode off lets the handler
    /// run again. The patch goes with this generation's Harmony id.
    /// </summary>
    [HarmonyPatch(typeof(InputGamepad), nameof(InputGamepad.HandleControllerInput))]
    public static class FocusMode
    {
        private static bool _active;

        public static bool Active => _active;

        public static void Toggle() => Set(!_active);

        public static void Set(bool on) => _active = on;

        /// <summary>Per frame; nothing to re-apply so far (the gamepad patch reads the flag).</summary>
        public static void Tick()
        {
        }

        /// <summary>Module teardown: on a shutdown the game gets its pad back; on a reload the successor
        /// has its own flag and patch.</summary>
        public static void Shutdown(bool restore)
        {
            _active = false;
        }

        // The game's gamepad handling, skipped while we own the input: its cursor hidden, no move, no
        // click. A gamepad press still flips the game's "gamepad over mouse" flag only through the
        // handler, so the mouse stays the game's idea of the pointer.
        private static bool Prefix(InputGamepad __instance)
        {
            if (!_active) return true;
            if (__instance.mCursor != null) __instance.mCursor.gameObject.SetActive(false);
            return false;
        }
    }
}
