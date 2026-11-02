using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PixelHeroesAccess.Dev
{
    /// <summary>
    /// The game's own selection for the dev /focus endpoint: the gamepad cursor's target button
    /// (<c>InputGamepad.GetTargetButton</c> of the current screen), its path and texts, and the topmost
    /// popup. Host-side and self-contained, so it works even when the module failed to load.
    /// </summary>
    internal static class FocusInspector
    {
        public static string Describe()
        {
            var sb = new StringBuilder();
            var screen = ScreenBase.pCurrentInstance;
            sb.Append("scene: ").Append(ScreenBase.pCurrentScene).Append('\n');
            sb.Append("screen: ").Append(screen != null ? screen.GetType().Name : "(none)").Append('\n');
            var popup = Popup.GetTopMostActivePopup();
            sb.Append("popup: ").Append(popup != null ? HierarchyPath(popup.gameObject) : "(none)").Append('\n');
            sb.Append("shop: ").Append(Shop.pActiveShop != null ? HierarchyPath(Shop.pActiveShop.gameObject) : "(none)").Append('\n');
            sb.Append("talkbox: ").Append(Talkbox.pActiveTalkbox != null ? "yes" : "no").Append('\n');
            var target = screen != null && screen.pInputGamepad != null ? screen.pInputGamepad.GetTargetButton() : null;
            if (target == null)
            {
                sb.Append("gamepad target: (none)\n");
                return sb.ToString();
            }
            sb.Append("gamepad target: ").Append(HierarchyPath(target.gameObject)).Append('\n');
            sb.Append("text: ").Append(LabelText(target.gameObject)).Append('\n');
            return sb.ToString();
        }

        private static string LabelText(GameObject go)
        {
            var parts = new StringBuilder();
            foreach (var label in go.GetComponentsInChildren<tk2dTextMesh>(true))
            {
                string t = label.text;
                if (string.IsNullOrEmpty(t))
                    continue;
                if (parts.Length > 0)
                    parts.Append(" | ");
                parts.Append(t);
            }
            return parts.Length > 0 ? parts.ToString() : "(no text)";
        }

        internal static string HierarchyPath(GameObject go)
        {
            var names = new List<string>();
            Transform t = go.transform;
            while (t != null)
            {
                names.Add(t.name);
                t = t.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
