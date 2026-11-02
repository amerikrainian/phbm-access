using System;
using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using UnityEngine;
using PixelHeroesAccess.Module.Interop;

namespace PixelHeroesAccess.Module.UI
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// Node factories for graph-native screens over the game's 2D Toolkit widgets: assemble
    /// <see cref="NodeVtable"/>s with the shared spoken conventions (role words, disabled/selected state,
    /// "n of m" positions) and route activation through the widget's own event (the game's click handler
    /// runs exactly as a mouse click would). Every label reads LIVE from the widget at speak time.
    /// </summary>
    public static class GameNodes
    {
        /// <summary>The label part (always first in the standard order).</summary>
        public static NodeAnnouncement LabelPart(Func<string> label)
            => new NodeAnnouncement(label, kind: AnnouncementKinds.Label);

        /// <summary>The disabled-state part: silent while enabled, "disabled" otherwise. Read on landing
        /// and on request, not live: the game grays a button out for the instant after it is pressed
        /// (Proceed, Quit to Menu), which would otherwise be announced as the control going dead.</summary>
        public static NodeAnnouncement DisabledPart(Func<bool> enabled)
            => new NodeAnnouncement(() => enabled == null || enabled() ? null : Strings.StateDisabled,
                live: false, kind: AnnouncementKinds.Enabled);

        /// <summary>The selected-state part: "selected" when selected, silent otherwise; LIVE.</summary>
        public static NodeAnnouncement SelectedPart(Func<bool> selected)
            => new NodeAnnouncement(() => selected != null && selected() ? Strings.StateSelected : null,
                live: true, kind: AnnouncementKinds.Selected);

        /// <summary>The spoken description part, read after the value.</summary>
        public static NodeAnnouncement TooltipPart(Func<string> description)
            => new NodeAnnouncement(description, kind: AnnouncementKinds.Tooltip);

        /// <summary>Texts as one line of sentences, the blanks dropped: each joined to the next with a
        /// period unless it already ends in sentence punctuation (game texts often do), so nothing
        /// reads as "..".</summary>
        public static string Sentences(IEnumerable<string> parts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var raw in parts)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string part = raw.Trim();
                if (sb.Length > 0)
                {
                    char last = LastShown(sb);
                    sb.Append(last == '.' || last == '!' || last == '?' || last == ':' ? " " : ". ");
                }
                sb.Append(part);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        public static string Sentences(params string[] parts) => Sentences((IEnumerable<string>)parts);

        // The last character a reader sees: past any closing markup tags ("bonus.</i>" ends in '.').
        private static char LastShown(System.Text.StringBuilder sb)
        {
            int i = sb.Length - 1;
            while (i >= 0)
            {
                if (char.IsWhiteSpace(sb[i])) { i--; continue; }
                if (sb[i] != '>') return sb[i];
                int open = i;
                while (open >= 0 && sb[open] != '<') open--;
                if (open < 0) return sb[i];
                i = open - 1;
            }
            return ' ';
        }

        /// <summary>Detail lines for the ui buffer from single texts: the non-blank ones, in order
        /// (a null or empty text is simply no line).</summary>
        public static IEnumerable<string> Lines(params string[] texts)
        {
            if (texts == null) yield break;
            foreach (var t in texts)
                if (!string.IsNullOrWhiteSpace(t)) yield return t;
        }

        /// <summary>Detail lines from a list (one per tooltip), the blanks dropped.</summary>
        public static IEnumerable<string> Lines(IEnumerable<string> lines)
        {
            if (lines == null) yield break;
            foreach (var t in lines)
                if (!string.IsNullOrWhiteSpace(t)) yield return t;
        }

        /// <summary>A plain read-only text line.</summary>
        public static NodeVtable Text(Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new[] { LabelPart(text) },
            SearchText = text,
        };

        /// <summary>A heading line ("text, heading").</summary>
        public static NodeVtable Heading(Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new List<NodeAnnouncement>
            {
                LabelPart(text),
                new NodeAnnouncement(() => Strings.RoleHeading, kind: AnnouncementKinds.Role),
            },
            SearchText = text,
        };

        /// <summary>A push button backed by a closure: "label, button[, disabled][, n of m]". A disabled
        /// button consumes activation silently.</summary>
        public static NodeVtable Button(Func<string> label, Action activate, Func<bool> enabled = null,
            ControlType type = null)
        {
            var anns = new List<NodeAnnouncement>
            {
                LabelPart(label),
                DisabledPart(enabled),
            };
            return new NodeVtable
            {
                ControlType = type ?? ControlTypes.Button,
                Announcements = anns,
                SearchText = label,
                OnActivate = () =>
                {
                    if (enabled != null && !enabled()) return;
                    activate?.Invoke();
                },
            };
        }

        /// <summary>A tk2d button: label from its texts (or the given override), enabled from the
        /// button's own enabled flag and the game's popup/shop gating (<see cref="tk2dButton.IsTriggerable"/>),
        /// activation through <see cref="tk2dButton.TriggerClick"/>, the call the game's own gamepad A press
        /// makes, so the game's handler runs as for a click.</summary>
        public static NodeVtable Button(tk2dButton button, Func<string> label = null, ControlType type = null)
            => Button(label ?? (() => LabelOf(button)), () => Click(button), () => Usable(button), type);

        /// <summary>Press a tk2d button as the game's gamepad does. Silent when it cannot be pressed now.</summary>
        public static void Click(tk2dButton button)
        {
            if (!Usable(button)) return;
            button.TriggerClick();
        }

        /// <summary>Whether a tk2d button is shown, enabled and not covered by a popup or shop.</summary>
        public static bool Usable(tk2dButton button)
            => button != null && button.gameObject.activeInHierarchy && button.enabled && button.IsTriggerable();

        /// <summary>A checkbox the game draws as a button beside a <c>Checkbox_Checked</c> /
        /// <c>Checkbox_Unchecked</c> sprite (<see cref="ScreenBase.SetCheckbox"/>): "label, toggle,
        /// on/off"; Enter presses the button, whose handler flips the setting and the
        /// sprite, and the new state is spoken from the sprite.</summary>
        public static NodeVtable Checkbox(tk2dButton button, tk2dSprite box, Func<string> label = null)
        {
            Func<string> lbl = label ?? (() => LabelOf(button));
            Func<string> state = () => IsChecked(box) ? Strings.StateOn : Strings.StateOff;
            return new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(lbl),
                    new NodeAnnouncement(state, live: true, kind: AnnouncementKinds.Value),
                    DisabledPart(() => Usable(button)),
                },
                SearchText = lbl,
                OnActivate = () => Click(button),
                StateText = state,
            };
        }

        /// <summary>One choice of a mutually exclusive group the game draws as checkboxes (the music
        /// mode, the language, the window mode): "label, radio button[, selected]"; Enter presses it.</summary>
        public static NodeVtable Radio(tk2dButton button, Func<bool> selected, Func<string> label = null)
        {
            Func<string> lbl = label ?? (() => LabelOf(button));
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(lbl),
                    SelectedPart(selected),
                    DisabledPart(() => Usable(button)),
                },
                SearchText = lbl,
                OnActivate = () => Click(button),
                StateText = () => selected() ? Strings.StateSelected : null,
            };
        }

        /// <summary>A tab button: "label, tab[, selected]"; landing on it or Enter shows its page.</summary>
        public static NodeVtable Tab(tk2dButton button, Func<bool> selected, Func<string> label = null)
        {
            Func<string> lbl = label ?? (() => LabelOf(button));
            Action select = () => { if (!selected()) Click(button); };
            return new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                // Selected, unspoken (landing selects it, so it would always say so): a vertical move
                // into the tab row lands on the shown tab, as Tab into its stop does.
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(lbl), DisabledPart(() => Usable(button)),
                    new NodeAnnouncement(() => selected() ? Strings.StateSelected : null, kind: AnnouncementKinds.Selected) { Spoken = false, Buffered = false },
                },
                SearchText = lbl,
                OnActivate = select,
                OnFocus = select,
            };
        }

        /// <summary>Whether a checkbox sprite shows checked.</summary>
        public static bool IsChecked(tk2dSprite box)
            => box != null && box.CurrentSprite != null && box.CurrentSprite.name == "Checkbox_Checked";

        /// <summary>The node a bare tk2d button reads as: a checkbox (its own sprite a
        /// <c>Checkbox_*</c>) as a toggle, anything else as a button.</summary>
        public static NodeVtable ForButton(tk2dButton button)
        {
            var sprite = button.GetComponent<tk2dSprite>();
            if (sprite != null && sprite.CurrentSprite != null && sprite.CurrentSprite.name.StartsWith("Checkbox_"))
                return Checkbox(button, sprite);
            return Button(button);
        }

        /// <summary>The shown texts under a tk2d button (its "TextMesh" child and any other caption);
        /// a caption-less icon button reads by its icon (<see cref="IconLabels.Of"/>).</summary>
        public static string LabelOf(Component widget)
        {
            if (widget == null) return null;
            return GameText.Under(widget) ?? IconLabels.Of(widget);
        }

        /// <summary>Whether a widget is both active in the hierarchy and worth listing.</summary>
        public static bool IsShown(Component widget)
            => widget != null && widget.gameObject != null && widget.gameObject.activeInHierarchy;
    }
}
