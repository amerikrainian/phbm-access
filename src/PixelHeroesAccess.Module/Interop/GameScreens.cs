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

        /// <summary>A return from the inventory to a shop: the inventory's Shop button changes scene to the
        /// shop's (the town, the library, the arena hub), whose first update opens the shop again
        /// (<see cref="ScreenBase.pSwitchedFromShopScene"/>, consumed there). Until then the scene is
        /// only passed through, and no screen of ours reads it. The inventory reads the same mark for
        /// its Shop button, so only those three scenes count it.</summary>
        public static bool ShopPending()
        {
            var screen = ScreenBase.pCurrentInstance;
            return ScreenBase.pSwitchedFromShopScene != eScenes.COUNT && (screen is Town || screen is Library || screen is ArenaHub);
        }

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
        /// reading order (top to bottom, then left to right) within <see cref="ButtonRank"/>: the
        /// content, then the way out, then the game's system buttons. The root is the one the game lets input
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
            list.Sort(ButtonOrder);
            return list;
        }

        /// <summary><see cref="ButtonRank"/>, then reading order.</summary>
        public static int ButtonOrder(tk2dButton a, tk2dButton b)
        {
            int rank = ButtonRank(a).CompareTo(ButtonRank(b));
            return rank != 0 ? rank : ReadingOrder(a, b);
        }

        /// <summary>Where a button goes in a list: 0 the content (wares, drinks, choices, tabs), 1 the
        /// way out (Leave, Leave Town, Back, Cancel, Close, Exit), 2 the system buttons the game puts
        /// on every screen (Quest Log, Help, Pause). The game lays these out in columns and corners,
        /// so reading order alone puts Leave between the drinks. By the prefab's object name, its
        /// numbering and separators dropped ("Button 02 LeaveTown", "Button_Back").</summary>
        public static int ButtonRank(tk2dButton button)
        {
            if (button == null) return 0;
            var sb = new System.Text.StringBuilder();
            foreach (char c in button.gameObject.name)
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            string n = sb.ToString();
            if (n.StartsWith("button")) n = n.Substring(6);
            switch (n)
            {
                case "leave": case "leavetown": case "back": case "cancel": case "close": case "exit":
                    return 1;
                case "questlog": case "help": case "pause":
                    return 2;
                default:
                    return 0;
            }
        }

        /// <summary>A click target on a figure or the scenery rather than a control: a hero's or an
        /// enemy's selector, an NPC, the tavern's barkeeper and band, the town's walking area. Screens
        /// reach those through their own data; the fallback leaves them out.</summary>
        public static bool IsFigure(tk2dButton button)
        {
            if (button == null) return true;
            if (button.GetComponentInParent<Character>() != null || button.GetComponentInParent<NPC>() != null) return true;
            string n = button.gameObject.name;
            return n == "BandSelector" || n == "Barkeeper" || n.Contains("Sky") || n.StartsWith("Button CtrlInp") || n.StartsWith("Blackness_");
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
