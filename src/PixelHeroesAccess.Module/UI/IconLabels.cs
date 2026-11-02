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

        // Keywords of the game's object names for its icon buttons ("Button 02 LeaveTown",
        // "Button QuestLog"), first match wins.
        private static readonly KeyValuePair<string, string>[] ByKeyword =
        {
            new KeyValuePair<string, string>("QuestLog", "button.quest_log"),
            new KeyValuePair<string, string>("LeaveTown", "button.leave_town"),
            new KeyValuePair<string, string>("Inventory", "button.inventory"),
            new KeyValuePair<string, string>("Pause", "button.pause"),
            new KeyValuePair<string, string>("Help", "button.help"),
            new KeyValuePair<string, string>("Back", "button.back"),
        };

        private static readonly HashSet<string> _logged = new HashSet<string>();

        public static string Of(Component widget)
        {
            if (widget == null) return null;
            var sprite = widget.GetComponent<tk2dBaseSprite>();
            string spriteName = sprite != null && sprite.CurrentSprite != null ? sprite.CurrentSprite.name : null;
            if (spriteName != null && BySprite.TryGetValue(spriteName, out var key)) return Strings.Get(key);
            string name = widget.gameObject.name;
            foreach (var kv in ByKeyword)
                if (name.IndexOf(kv.Key, System.StringComparison.OrdinalIgnoreCase) >= 0) return Strings.Get(kv.Value);
            if (_logged.Add(name + "|" + spriteName))
                CoreLog.Info("IconLabels: unnamed button '" + name + "' sprite '" + spriteName + "'");
            string cleaned = name.StartsWith("Button ") ? name.Substring("Button ".Length) : name;
            return cleaned.Replace('_', ' ').Trim();
        }
    }
}
