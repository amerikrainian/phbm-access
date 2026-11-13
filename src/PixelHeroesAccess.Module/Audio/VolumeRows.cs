using System;
using PixelHeroesAccess.Core;
using HarmonyLib;
using UnityEngine;

namespace PixelHeroesAccess.Module.Audio
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The volume rows drawn into the pause popup's Audio tab, in its empty right column under Sound
    /// Effects, one row per volume at the music choices' spacing: the name, and under it a minus button,
    /// the percentage and a plus button (the name beside them would run off the popup, German more so). Each button is a copy of the Sound Effects checkbox with the game's handler
    /// taken off and the mod's on (<see cref="tk2dButton.ButtonPressedEvent"/>, which a click and
    /// <see cref="tk2dButton.TriggerClick"/> both raise), its gamepad neighbours cleared so the game's
    /// pad cursor finds it by position; the texts are copies of its caption. Under the tab, so the game
    /// shows and hides them with it. Built when the game fills its options
    /// (<see cref="PopupPause.UpdateOptions"/>) or the mod's screen reads them, torn down with the module.
    /// </summary>
    public static class VolumeRows
    {
        private const string RootName = "PixelHeroesAccess Volume";
        // The percentage's room between the two buttons, in screen units (320 x 240).
        private const float ValueWidth = 24f;
        // Within a row: the name above its middle, the buttons below it.
        private const float LabelRise = 10f;
        private const float ButtonDrop = 7f;

        private static GameObject _root;
        private static PopupPause _popup;
        private static readonly tk2dTextMesh[] _values = new tk2dTextMesh[3];
        private static readonly tk2dTextMesh[] _labels = new tk2dTextMesh[3];
        private static readonly tk2dButton[] _minus = new tk2dButton[3];
        private static bool _subscribed;

        /// <summary>The minus button of a volume's row, for the screen's enabled gate; null before the
        /// rows exist.</summary>
        public static tk2dButton MinusButton(VolumeChannel channel) => _root != null ? _minus[(int)channel] : null;

        /// <summary>Make sure the popup's Audio tab carries the rows, and show the current values.</summary>
        public static void Ensure(PopupPause popup)
        {
            if (popup == null || popup.v_TabAudio == null || popup.v_CheckboxSFX == null) return;
            if (_root == null || !ReferenceEquals(_popup, popup) || _root.transform.parent != popup.v_TabAudio)
                Build(popup);
            Refresh();
        }

        private static void Build(PopupPause popup)
        {
            Destroy();
            var tab = popup.v_TabAudio;
            // An earlier generation's rows, should its teardown not have run yet.
            for (int i = tab.childCount - 1; i >= 0; i--)
                if (tab.GetChild(i).name == RootName) UnityEngine.Object.Destroy(tab.GetChild(i).gameObject);

            var sfx = popup.v_CheckboxSFX;
            var caption = sfx.transform.Find("TextMesh");
            if (caption == null || popup.v_CheckboxMusicMix == null || popup.v_CheckboxMusicOld == null)
            {
                CoreLog.Warning("VolumeRows: the Audio tab is not shaped as expected; no volume rows");
                return;
            }
            var captionMesh = caption.GetComponent<tk2dTextMesh>();
            var buttonCaption = popup.v_ButtonAudio != null ? popup.v_ButtonAudio.transform.Find("TextMesh") : null;
            Color glyphColor = buttonCaption != null && buttonCaption.GetComponent<tk2dTextMesh>() != null
                ? buttonCaption.GetComponent<tk2dTextMesh>().color : Color.white;

            _root = new GameObject(RootName);
            _root.layer = sfx.gameObject.layer;
            _root.transform.SetParent(tab, false);
            _root.transform.localPosition = Vector3.zero;
            _popup = popup;

            Vector3 origin = sfx.transform.localPosition;
            float rowStep = popup.v_CheckboxMusicOld.transform.localPosition.y - popup.v_CheckboxMusicMix.transform.localPosition.y;
            float box = Extensions.GlobalExtensions.GetWidth(sfx);
            float captionZ = origin.z + caption.localPosition.z;

            foreach (var channel in Volume.Channels)
            {
                int i = (int)channel;
                float rowY = origin.y + rowStep * (i + 1);
                var minusAt = new Vector3(origin.x, Mathf.Round(rowY - ButtonDrop), origin.z);
                var plusAt = minusAt + new Vector3(Mathf.Round(box + ValueWidth), 0f, 0f);
                _minus[i] = StepButton(sfx, minusAt, "-", glyphColor, channel, -1);
                StepButton(sfx, plusAt, "+", glyphColor, channel, 1);
                _values[i] = Text(captionMesh, new Vector3((minusAt.x + plusAt.x) / 2f, minusAt.y, captionZ), TextAnchor.MiddleCenter);
                _labels[i] = Text(captionMesh, new Vector3(Mathf.Round(origin.x - box / 2f), Mathf.Round(rowY + LabelRise), captionZ), TextAnchor.MiddleLeft);
            }
            if (!_subscribed)
            {
                Volume.Changed += Refresh;
                _subscribed = true;
            }
        }

        // A copy of the Sound Effects checkbox as an empty box with a glyph on it, pressing which steps
        // the volume.
        private static tk2dButton StepButton(tk2dSprite template, Vector3 at, string glyph, Color glyphColor, VolumeChannel channel, int steps)
        {
            var copy = UnityEngine.Object.Instantiate(template.gameObject, _root.transform, false);
            copy.name = RootName + " " + channel + (steps < 0 ? " down" : " up");
            copy.transform.localPosition = at;
            var button = copy.GetComponent<tk2dButton>();
            button.targetObject = null;
            button.messageName = "";
            button.buttonDownSprite = "";
            button.buttonUpSprite = "";
            button.buttonPressedSprite = "";
            button.pContrInpUp = new tk2dButton[0];
            button.pContrInpDown = new tk2dButton[0];
            button.pContrInpLeft = new tk2dButton[0];
            button.pContrInpRight = new tk2dButton[0];
            button.ButtonPressedEvent += _ =>
            {
                try { Volume.Adjust(channel, steps); }
                catch (Exception e) { CoreLog.Warning("VolumeRows: step failed: " + e); }
            };
            ScreenBase.SetCheckbox(copy.GetComponent<tk2dSprite>(), false);
            var caption = copy.transform.Find("TextMesh");
            if (caption != null)
            {
                caption.localPosition = new Vector3(0f, 0f, caption.localPosition.z);
                var mesh = caption.GetComponent<tk2dTextMesh>();
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.color = glyphColor;
                mesh.SetText(glyph);
            }
            return button;
        }

        private static tk2dTextMesh Text(tk2dTextMesh template, Vector3 at, TextAnchor anchor)
        {
            var copy = UnityEngine.Object.Instantiate(template.gameObject, _root.transform, false);
            copy.transform.localPosition = at;
            var mesh = copy.GetComponent<tk2dTextMesh>();
            mesh.maxChars = 32;
            mesh.anchor = anchor;
            return mesh;
        }

        private static void Refresh()
        {
            if (_root == null) return;
            foreach (var channel in Volume.Channels)
            {
                int i = (int)channel;
                if (_values[i] != null) _values[i].SetText(Volume.ValueText(channel));
                if (_labels[i] != null) _labels[i].SetText(Volume.Label(channel));
            }
        }

        private static void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _popup = null;
            Array.Clear(_values, 0, _values.Length);
            Array.Clear(_labels, 0, _labels.Length);
            Array.Clear(_minus, 0, _minus.Length);
        }

        /// <summary>Module teardown: the rows go with the generation that drew them.</summary>
        public static void Shutdown()
        {
            Destroy();
            if (_subscribed) Volume.Changed -= Refresh;
            _subscribed = false;
        }

        [HarmonyPatch(typeof(PopupPause), nameof(PopupPause.UpdateOptions))]
        private static class OptionsShown
        {
            private static void Postfix(PopupPause __instance)
            {
                try { Ensure(__instance); }
                catch (Exception e) { CoreLog.Warning("VolumeRows: building the rows failed: " + e); }
            }
        }
    }
}
