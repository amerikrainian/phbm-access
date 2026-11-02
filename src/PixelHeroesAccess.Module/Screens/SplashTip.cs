using System;
using HarmonyLib;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The splash screen (<see cref="Splashscreen"/>, no <see cref="ScreenBase"/>: nothing to press on
    /// PC, where it moves on to the title menu by itself after a few seconds) shows a random tip
    /// ("Tip: ..."), set once in its <c>Update</c> as it starts waiting (<see cref="Splashscreen.mTipCreated"/>).
    /// A postfix there says the tip, once per splash, queued after the launch line.
    /// </summary>
    [HarmonyPatch(typeof(Splashscreen), "Update")]
    internal static class SplashTip
    {
        private static Splashscreen _said;

        private static void Postfix(Splashscreen __instance)
        {
            try
            {
                if (ReferenceEquals(_said, __instance) || !__instance.mTipCreated) return;
                _said = __instance;
                string tip = GameText.Of(__instance.v_TextMesh_Tip);
                if (tip != null && FocusMode.Active) Speech.Say(tip);
            }
            catch (Exception e) { CoreLog.Warning("SplashTip: " + e.Message); }
        }
    }
}
