using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;
using UnityEngine;
using Screen = PixelHeroesAccess.Core.Screens.Screen;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The fallback over anything no dedicated screen claims: the buttons a player could press right now
    /// (under the topmost popup, else the open shop, else the scene's screen:
    /// <see cref="GameScreens.InputRoot"/>), each read by its own texts, in reading order, pressed as the
    /// game's gamepad presses them. Over a popup its texts are listed too, in reading order among the
    /// buttons, and said when it opens, focus landing on its first button. Escape is the game's own
    /// cancel (<see cref="Cancel"/>). Lowest layer: any dedicated screen sits above it and wins.
    /// </summary>
    public sealed class GenericScreen : Screen
    {
        private Popup _announced;

        public override string Key => "game.generic";
        public override int Layer => -10;

        public override bool IsActive() => ScreenBase.pCurrentInstance != null && !GameScreens.ShopPending();

        public override void OnUpdate()
        {
            // A popup's texts are its message: said once as it comes up, ahead of the landing.
            var popup = GameScreens.TopPopup();
            if (popup == _announced) return;
            if (popup != null && !popup.IsActiveAndNotMoving()) return;
            _announced = popup;
            // The rename popup is typed into; TextEntry says it.
            if (popup == null || popup is PopupHeroRename || !Navigation.FocusActive()) return;
            string message = GameNodes.Sentences(PopupTexts(popup.transform));
            if (!string.IsNullOrWhiteSpace(message)) Speech.Say(message);
        }

        public override void OnPop() => _announced = null;

        public override void Build(GraphBuilder b)
        {
            var root = GameScreens.InputRoot();
            if (root == null) return;
            // A popup still sliding in has every button blocked: nothing to land on yet.
            var top = GameScreens.TopPopup();
            if (top != null && !top.IsActiveAndNotMoving()) return;
            var buttons = GameScreens.LiveButtons(root);
            buttons.RemoveAll(GameScreens.IsFigure);
            bool isPopup = Popup.GetTopMostActivePopup() == root;
            var items = new List<Component>(buttons);
            if (isPopup) items.AddRange(LooseTexts(root));
            if (items.Count == 0) return;
            // A popup's texts read with its content; its way out and system buttons come after.
            items.Sort((x, y) =>
            {
                int rank = GameScreens.ButtonRank(x as tk2dButton).CompareTo(GameScreens.ButtonRank(y as tk2dButton));
                return rank != 0 ? rank : GameScreens.ReadingOrder(x, y);
            });
            b.PushContext(null, null);
            ControlId first = null;
            foreach (var item in items)
            {
                var id = ControlId.ForObject(item);
                if (item is tk2dButton button)
                {
                    b.AddItem(id, GameNodes.ForButton(button));
                    if (first == null) first = id;
                }
                else
                {
                    var mesh = (tk2dTextMesh)item;
                    b.AddItem(id, GameNodes.Text(() => GameText.Of(mesh)));
                }
            }
            if (first != null) b.SetStart(first);
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var popup = GameScreens.TopPopup();
                if (popup != null) CancelPopup(popup);
                else Cancel();
            });
        }

        /// <summary>The game's own cancel, as the gamepad's B: the shop's back, the top popup's
        /// <see cref="Popup.EventRequestCancel"/>, the talk box, else the screen's own.</summary>
        internal static void Cancel()
        {
            var screen = ScreenBase.pCurrentInstance;
            if (screen != null) screen.BaseTriggerEventControllerInputButtonCancelDown();
        }

        /// <summary>Cancel over a popup: the game's cancel first; a popup that ignores it (the base
        /// <see cref="Popup"/> answers no, and many screens close only some of theirs) and is still up
        /// is closed as its own close button does (<see cref="Popup.EventClose"/>).</summary>
        internal static void CancelPopup(Popup popup)
        {
            Cancel();
            if (popup != null && popup.IsActiveAndNotMoving()) popup.EventClose();
        }

        /// <summary>The texts of a popup outside its buttons, in reading order, as lines.</summary>
        internal static List<string> PopupTexts(Transform root)
        {
            var meshes = LooseTexts(root);
            meshes.Sort(GameScreens.ReadingOrder);
            var lines = new List<string>();
            foreach (var mesh in meshes)
            {
                string t = GameText.Of((tk2dTextMesh)mesh);
                if (t != null) lines.Add(t);
            }
            return lines;
        }

        // The shown texts under a root that are no button's caption and no shadow.
        private static List<Component> LooseTexts(Transform root)
        {
            var list = new List<Component>();
            foreach (var mesh in root.GetComponentsInChildren<tk2dTextMesh>(false))
            {
                if (GameText.IsShadow(mesh) || GameText.Of(mesh) == null) continue;
                if (mesh.GetComponentInParent<tk2dButton>() != null) continue;
                list.Add(mesh);
            }
            return list;
        }
    }
}
