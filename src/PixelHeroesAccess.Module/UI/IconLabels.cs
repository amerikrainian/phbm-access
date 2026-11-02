using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Strings;
using UnityEngine;

namespace PixelHeroesAccess.Module.UI
{
    /// <summary>
    /// Names for the game's caption-less buttons: an icon drawn as the button's own sprite (the
    /// popups' back arrow, the menu bar's help, pause and quest log, the backpack's scroll arrows).
    /// Known sprites read by the mod's word for them (<see cref="Strings"/>, "button.*"); an unknown
    /// one reads by its object's name less the "Button" prefix ("Button Back" is "Back"), and is
    /// logged once so the gap is visible.
    /// </summary>
    internal static class IconLabels
    {
        private static readonly Dictionary<string, string> BySprite = new Dictionary<string, string>
        {
            { "Arena_BackButton", "button.back" },
        };

        private static readonly Dictionary<string, string> ByObject = new Dictionary<string, string>
        {
            { "Button Back", "button.back" },
        };

        private static readonly HashSet<string> _logged = new HashSet<string>();

        public static string Of(Component widget)
        {
            if (widget == null) return null;
            var sprite = widget.GetComponent<tk2dBaseSprite>();
            string spriteName = sprite != null && sprite.CurrentSprite != null ? sprite.CurrentSprite.name : null;
            if (spriteName != null && BySprite.TryGetValue(spriteName, out var key)) return Strings.Get(key);
            string name = widget.gameObject.name;
            if (ByObject.TryGetValue(name, out key)) return Strings.Get(key);
            if (_logged.Add(name + "|" + spriteName))
                CoreLog.Info("IconLabels: unnamed button '" + name + "' sprite '" + spriteName + "'");
            string cleaned = name.StartsWith("Button ") ? name.Substring("Button ".Length) : name;
            return cleaned.Replace('_', ' ').Trim();
        }
    }
}
