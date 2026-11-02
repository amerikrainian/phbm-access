using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using UnityEngine;

namespace PixelHeroesAccess.Module.UI
{
    /// <summary>
    /// The hero rename popup (<see cref="PopupHeroRename"/>, from the tavern's hero card), the game's one
    /// text field. The game types into it itself, from the Input System keyboard by physical key
    /// (letters, Shift for capitals, space, period, Backspace and Delete; on a German game the keys
    /// under Ä, Ö, Ü and ß, and Y and Z swapped as on a German keyboard), up to the width its label
    /// allows. While it is open the mod's keys stand down entirely
    /// (<see cref="Core.Input.InputManager.TextFieldFocused"/>), each change of the name is echoed (the
    /// letter typed, the letter erased), Enter presses OK (the game takes a name of two characters or
    /// more with a letter in it; otherwise it says OK is disabled) and Escape cancels. Opening says
    /// "Rename Hero: Ghada".
    /// </summary>
    internal static class TextEntry
    {
        private static PopupHeroRename _open;
        private static string _name;

        private static PopupHeroRename Rename()
        {
            var top = GameScreens.TopPopup() as PopupHeroRename;
            return top != null && top.IsActiveAndNotMoving() ? top : null;
        }

        public static bool OwnsKeyboard => FocusMode.Active && Rename() != null;

        public static void Tick()
        {
            if (!FocusMode.Active) { _open = null; return; }
            var popup = Rename();
            if (popup != _open)
            {
                _open = popup;
                _name = popup != null ? popup.mHeroName : null;
                if (popup != null)
                    Speech.Say(Strings.RenameOpen(GameText.Line(GameText.Tr(ET.GUI_TV_B_ChangeHeroName)), _name), interrupt: true);
                return;
            }
            if (popup == null) return;
            Echo(popup.mHeroName);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (GameNodes.Usable(popup.v_Button_OK))
                {
                    string name = popup.mHeroName;
                    popup.EventButtonOK();
                    Speech.Say(name, interrupt: true);
                }
                else Speech.Say(GameNodes.LabelOf(popup.v_Button_OK) + ", " + Strings.StateDisabled, interrupt: true);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                popup.EventButtonCancel();
                Speech.Say(Strings.KeysCancelled, interrupt: true);
            }
        }

        // What changed since the last frame: the character added, the character erased, or the name.
        private static void Echo(string name)
        {
            if (name == _name) return;
            string before = _name ?? "";
            _name = name ?? "";
            if (_name.Length == before.Length + 1 && _name.StartsWith(before))
                Speech.Say(Spoken(_name[_name.Length - 1]), interrupt: true);
            else if (_name.Length + 1 == before.Length && before.StartsWith(_name))
                Speech.Say(Strings.EditDeleted(Spoken(before[before.Length - 1])), interrupt: true);
            else
                Speech.Say(_name.Length == 0 ? Strings.ValueBlank : _name, interrupt: true);
        }

        private static string Spoken(char c) => c == ' ' ? Strings.ValueSpace : c.ToString();

        public static void Shutdown()
        {
            _open = null;
            _name = null;
        }
    }
}

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// Holds the rename popup's place among the screens while <see cref="UI.TextEntry"/> types into it:
    /// the name, nothing else (the popup's on-screen keyboard is the game's mouse aid), landed on
    /// quietly, since the entry says the popup itself.
    /// </summary>
    public sealed class RenameScreen : PopupScreen
    {
        public override string Key => "game.rename";
        public override int Layer => 12;

        protected override Popup Resolve() => Interop.GameScreens.TopPopup() as PopupHeroRename;

        public override void OnPush() => Core.UI.Navigation.QuietNextLanding();

        public override void Build(Core.Graph.GraphBuilder b)
        {
            var popup = Resolve() as PopupHeroRename;
            if (popup == null) return;
            b.AddItem(Core.Graph.ControlId.Structural("rename:name"), UI.GameNodes.Text(() => (Resolve() as PopupHeroRename)?.mHeroName));
        }
    }
}
