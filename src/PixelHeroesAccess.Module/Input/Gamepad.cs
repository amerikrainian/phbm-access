using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Pad = UnityEngine.InputSystem.Gamepad;

namespace PixelHeroesAccess.Module.Input
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The gamepad as the mod's bindings see it: which controls are held this frame and which were held
    /// the frame before, across every connected pad. The game reads its pad through the Input System
    /// (<c>Gamepad.current</c>, in <see cref="InputGamepad.HandleControllerInput"/>); we read the same
    /// devices once per module tick, before the input dispatch, so the result is the same whether or not
    /// the game is let to hear the pad (focus mode skips the game's handler, <see cref="UI.FocusMode"/>).
    /// </summary>
    internal static class Gamepad
    {
        // A stick or trigger counts as a press past this deflection: well clear of drift.
        private const float Threshold = 0.5f;

        private static HashSet<ControllerInput> _held = new HashSet<ControllerInput>();
        private static HashSet<ControllerInput> _was = new HashSet<ControllerInput>();

        public static bool IsHeld(ControllerInput input) => _held.Contains(input);
        public static bool WasHeld(ControllerInput input) => _was.Contains(input);

        /// <summary>Whether any control is held (a rebind waits for the pad to be let go).</summary>
        public static bool AnyHeld => _held.Count > 0;

        /// <summary>The controls held this frame that were not the frame before.</summary>
        public static IEnumerable<ControllerInput> Pressed()
        {
            foreach (var input in _held)
                if (!_was.Contains(input)) yield return input;
        }

        /// <summary>The controls let go this frame.</summary>
        public static IEnumerable<ControllerInput> Released()
        {
            foreach (var input in _was)
                if (!_held.Contains(input)) yield return input;
        }

        /// <summary>Per module tick, before the input dispatch: this frame's state of every pad.</summary>
        public static void Poll()
        {
            var previous = _was;
            _was = _held;
            previous.Clear();
            _held = previous;
            var pads = Pad.all;
            for (int i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad == null || !pad.enabled) continue;
                Button(pad.buttonSouth, ControllerInput.A);
                Button(pad.buttonEast, ControllerInput.B);
                Button(pad.buttonWest, ControllerInput.X);
                Button(pad.buttonNorth, ControllerInput.Y);
                Button(pad.leftShoulder, ControllerInput.LeftShoulder);
                Button(pad.rightShoulder, ControllerInput.RightShoulder);
                Button(pad.leftStickButton, ControllerInput.LeftStickClick);
                Button(pad.rightStickButton, ControllerInput.RightStickClick);
                Button(pad.startButton, ControllerInput.Start);
                Button(pad.selectButton, ControllerInput.Back);
                Button(pad.dpad.up, ControllerInput.DpadUp);
                Button(pad.dpad.down, ControllerInput.DpadDown);
                Button(pad.dpad.left, ControllerInput.DpadLeft);
                Button(pad.dpad.right, ControllerInput.DpadRight);
                Axis(pad.leftStick.y.ReadValue(), ControllerInput.LeftStickUp, ControllerInput.LeftStickDown);
                Axis(pad.leftStick.x.ReadValue(), ControllerInput.LeftStickRight, ControllerInput.LeftStickLeft);
                Axis(pad.rightStick.y.ReadValue(), ControllerInput.RightStickUp, ControllerInput.RightStickDown);
                Axis(pad.rightStick.x.ReadValue(), ControllerInput.RightStickRight, ControllerInput.RightStickLeft);
                Axis(pad.leftTrigger.ReadValue(), ControllerInput.LeftTrigger, null);
                Axis(pad.rightTrigger.ReadValue(), ControllerInput.RightTrigger, null);
            }
        }

        private static void Button(ButtonControl button, ControllerInput input)
        {
            if (button != null && button.isPressed) _held.Add(input);
        }

        private static void Axis(float value, ControllerInput positive, ControllerInput? negative)
        {
            if (value >= Threshold) _held.Add(positive);
            else if (negative != null && value <= -Threshold) _held.Add(negative.Value);
        }

        /// <summary>Module teardown: nothing held, so a successor starts from a clean slate.</summary>
        public static void Reset()
        {
            _held.Clear();
            _was.Clear();
        }
    }
}
