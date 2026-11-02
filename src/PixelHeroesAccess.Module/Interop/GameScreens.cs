using System.Collections.Generic;
using UnityEngine;

namespace PixelHeroesAccess.Module.Interop
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The game's own registry of what is on show, read on every call (never kept): one
    /// <see cref="ScreenBase"/> per scene (<see cref="ScreenBase.pCurrentInstance"/>, null during a scene
    /// transition's fade), the popups as a stack (<see cref="Popup.mActivePopups"/>, the topmost last),
    /// the open shop (<see cref="Shop.pActiveShop"/>), and the speech box (<see cref="Talkbox.pActiveTalkbox"/>).
    /// The game itself gates every button by these (<see cref="tk2dButton.IsTriggerable"/>: under the
    /// topmost popup, else under the open shop), so a screen of ours is "on show" exactly when its game
    /// object is the one these name.
    /// </summary>
    public static class GameScreens
    {
        /// <summary>The scene's screen of type T while it is the live one, else null.</summary>
        public static T Current<T>() where T : ScreenBase
        {
            var screen = ScreenBase.pCurrentInstance;
            return screen != null ? screen as T : null;
        }

        /// <summary>The topmost active popup, or null.</summary>
        public static Popup TopPopup()
        {
            var t = Popup.GetTopMostActivePopup();
            return t != null ? t.GetComponent<Popup>() : null;
        }

        /// <summary>Whether this popup is the topmost active one.</summary>
        public static bool IsTop(Popup popup)
            => popup != null && TopPopup() == popup;

        /// <summary>The open shop, or null.</summary>
        public static Shop OpenShop()
            => Shop.pActiveShop != null ? Shop.pActiveShop.GetComponent<Shop>() : null;

        /// <summary>The root the game's buttons are live under right now: the topmost popup, else the
        /// open shop, else the scene's screen. Null between scenes.</summary>
        public static Transform InputRoot()
        {
            var popup = Popup.GetTopMostActivePopup();
            if (popup != null) return popup;
            if (Shop.pActiveShop != null) return Shop.pActiveShop;
            var screen = ScreenBase.pCurrentInstance;
            return screen != null ? screen.transform : null;
        }

        /// <summary>The buttons a player sees under a root, as the game's own gamepad lists them
        /// (<see cref="InputGamepad.HandleControllerInput"/>: shown, wired, not a scroll handle), the
        /// disabled ones too (a greyed-out Continue is information; pressing one does nothing), in
        /// reading order: top to bottom, then left to right. The root is the one the game lets input
        /// reach (<see cref="InputRoot"/>), so a shown button is pressable unless disabled.</summary>
        public static List<tk2dButton> LiveButtons(Transform root)
        {
            var list = new List<tk2dButton>();
            if (root == null) return list;
            foreach (var b in root.GetComponentsInChildren<tk2dButton>())
            {
                if (b == null || b.gameObject.name == "Scroll Handle") continue;
                if (!b.gameObject.activeInHierarchy || !b.HasTriggerTarget()) continue;
                list.Add(b);
            }
            list.Sort(ReadingOrder);
            return list;
        }

        /// <summary>Top to bottom (rows within a few units count as one), then left to right.</summary>
        public static int ReadingOrder(Component a, Component b)
        {
            Vector3 pa = a.transform.position, pb = b.transform.position;
            float dy = pb.y - pa.y;
            if (Mathf.Abs(dy) > RowTolerance(a)) return dy > 0 ? 1 : -1;
            return pa.x.CompareTo(pb.x);
        }

        // The game draws at 320x240 world units under tk2dCamera's scale: a row is a few units tall.
        private static float RowTolerance(Component c)
        {
            float scale = tk2dCamera.inst != null ? Mathf.Abs(tk2dCamera.inst.transform.localScale.y) : 1f;
            return 4f * (scale > 0 ? scale : 1f);
        }
    }
}
