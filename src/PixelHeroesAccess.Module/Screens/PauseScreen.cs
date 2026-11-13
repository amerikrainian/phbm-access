using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Module.Audio;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;
using UnityEngine;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The pause menu and the options (<see cref="PopupPause"/>, every screen's
    /// <see cref="ScreenBase.v_Popup_Pause"/>; the title menu opens it on its options page alone). The
    /// pause page is its buttons in reading order (Resume, Options, Help, Save and Exit). The options
    /// page is the back button, the General and Audio tabs (one shown at a time, its button the
    /// selected tab), and the page's choices grouped as the game draws them: the window mode and the
    /// language each a group of radio buttons (the language is offered on the title menu alone, where
    /// a change reloads the game), Run in Background a toggle; the music mode a group of four, Sound
    /// Effects a toggle, then the mod's master, music and effects volumes as sliders (Left and Right),
    /// which it also draws on the tab. Every game choice presses the game's own checkbox button.
    /// </summary>
    public sealed class PauseScreen : PopupScreen
    {
        public override string Key => "game.pause";

        protected override Popup Resolve() => ScreenBase.pCurrentInstance != null ? ScreenBase.pCurrentInstance.v_Popup_Pause : null;

        private static PopupPause Pause => ScreenBase.pCurrentInstance != null ? ScreenBase.pCurrentInstance.v_Popup_Pause : null;

        public override void Build(GraphBuilder b)
        {
            var pause = Pause;
            if (pause == null) return;
            bool options = pause.v_Tabs != null && pause.v_Tabs.Length > 1 && pause.v_Tabs[1] != null && pause.v_Tabs[1].activeInHierarchy;
            if (!options)
            {
                b.PushContext(GameText.Tr(ET.GUI_GL_T_TitlePause));
                foreach (var button in GameScreens.LiveButtons(pause.v_Tabs[0].transform))
                    b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
                b.PopContext();
                return;
            }

            b.PushContext(GameText.Tr(ET.GUI_GL_T_TitleOptions), null);
            var back = FindButton(pause.transform, "Button Back");
            if (back != null) b.AddItem(ControlId.Structural("pause:back"), GameNodes.Button(back));
            b.StartRow("pause:tabs");
            b.AddItem(ControlId.Structural("pause:tab:general"), GameNodes.Tab(pause.v_ButtonGeneral, () => Shown(pause.v_TabGeneral)));
            b.AddItem(ControlId.Structural("pause:tab:audio"), GameNodes.Tab(pause.v_ButtonAudio, () => Shown(pause.v_TabAudio)));
            b.EndRow();
            if (Shown(pause.v_TabGeneral))
            {
                if (Shown(pause.v_CheckboxResolution1))
                {
                    b.StartRow("pause:window");
                    Choice(b, "pause:res1", pause.v_CheckboxResolution1);
                    Choice(b, "pause:res2", pause.v_CheckboxResolution2);
                    b.EndRow();
                }
                b.StartRow("pause:language");
                Language(b, "pause:english", pause.v_CheckboxEnglish);
                Language(b, "pause:german", pause.v_CheckboxGerman);
                b.EndRow();
                Toggle(b, "pause:background", pause.v_CheckboxRunInBackground);
            }
            else if (Shown(pause.v_TabAudio))
            {
                b.StartRow("pause:music");
                Toggle(b, "pause:mix", pause.v_CheckboxMusicMix, radio: true);
                Toggle(b, "pause:old", pause.v_CheckboxMusicOld, radio: true);
                Toggle(b, "pause:new", pause.v_CheckboxMusicNew, radio: true);
                Toggle(b, "pause:off", pause.v_CheckboxMusicOff, radio: true);
                b.EndRow();
                Toggle(b, "pause:sfx", pause.v_CheckboxSFX);
                // The mod's volumes, drawn under Sound Effects (Audio/VolumeRows).
                VolumeRows.Ensure(pause);
                foreach (var channel in Volume.Channels)
                {
                    var ch = channel;
                    b.AddItem(ControlId.Structural("pause:volume:" + ch), GameNodes.Slider(
                        () => Volume.Label(ch), () => Volume.ValueText(ch),
                        (sign, large) => { Volume.Adjust(ch, sign * (large ? 5 : 1)); },
                        () => GameNodes.Usable(VolumeRows.MinusButton(ch))));
                }
            }
            b.PopContext();
        }

        private static bool Shown(Component c) => c != null && c.gameObject.activeInHierarchy;

        // A radio choice whose checkbox is a Container (its sprite at 0) carrying the button itself.
        private static void Choice(GraphBuilder b, string id, Container box)
        {
            if (!Shown(box)) return;
            var button = box.GetComponent<tk2dButton>();
            var sprite = box.GetASprite(0) ?? box.GetComponent<tk2dSprite>();
            if (button == null) return;
            b.AddItem(ControlId.Structural(id), GameNodes.Radio(button, () => GameNodes.IsChecked(sprite)));
        }

        // A language choice: its button is the Container's (1), its caption the allowed or the
        // not-allowed label (2 or 3) the game shows.
        private static void Language(GraphBuilder b, string id, Container box)
        {
            if (!Shown(box)) return;
            var button = box.GetAButton(1) ?? box.GetComponent<tk2dButton>();
            var sprite = box.GetASprite(0) ?? box.GetComponent<tk2dSprite>();
            if (button == null) return;
            b.AddItem(ControlId.Structural(id), GameNodes.Radio(button, () => GameNodes.IsChecked(sprite), () => GameText.Under(box)));
        }

        // A checkbox drawn as the button's own sprite.
        private static void Toggle(GraphBuilder b, string id, tk2dSprite sprite, bool radio = false)
        {
            if (!Shown(sprite)) return;
            var button = sprite.GetComponent<tk2dButton>();
            if (button == null) return;
            b.AddItem(ControlId.Structural(id), radio
                ? GameNodes.Radio(button, () => GameNodes.IsChecked(sprite))
                : GameNodes.Checkbox(button, sprite));
        }

        private static tk2dButton FindButton(Transform root, string name)
        {
            foreach (var button in root.GetComponentsInChildren<tk2dButton>(false))
                if (button.gameObject.name == name) return button;
            return null;
        }
    }
}
