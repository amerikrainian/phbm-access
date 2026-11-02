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
    /// Any talk (<see cref="Talkbox.pActiveTalkbox"/>: an NPC in town, the temple, the library, the
    /// campaign's opening, the casino's and the barkeeper's fixed lines, an overland event). Each page is
    /// said as it comes up, the speaker first when it changes (with the quest and its dungeon while
    /// an NPC offers or holds one). The page is the first control: Enter goes on to the next page at
    /// once (the game's first click only finishes the typewriter, its second turns the page), and on a
    /// last page that halts (a quest on offer) it does nothing. Then the choices the scene shows beside
    /// the talk: an event's options (<see cref="Talkbox.v_ButtonsOptions"/>), the town's Accept and
    /// Refuse, and the scene's other live buttons. Escape is the game's cancel: the talk box skips to
    /// its end (<see cref="Talkbox.EventCancel"/>), which over a quest offer is a refusal.
    /// </summary>
    public sealed class TalkScreen : Screen
    {
        private object _pageArray;
        private int _pageIndex = -1;
        private Talkbox.eOverlandEventStates _pageState;
        private string _speaker;
        private bool _haltHandled;
        private ControlId _firstChoice;
        private tk2dButton _firstChoiceButton;

        public override string Key => "game.talk";
        public override int Layer => 5;

        private static Talkbox Box => Talkbox.pActiveTalkbox;

        public override bool IsActive()
        {
            var box = Box;
            return box != null && box.IsTalking() && ScreenBase.pCurrentInstance != null && Popup.GetTopMostActivePopup() == null;
        }

        // The screen says each page itself (speaker first): the landing on the page is not spoken.
        public override void OnPush() => Navigation.QuietNextLanding();

        public override void OnPop()
        {
            _pageArray = null;
            _pageIndex = -1;
            _speaker = null;
            _haltHandled = false;
        }

        public override void OnUpdate()
        {
            var box = Box;
            if (box == null || !box.IsTalking()) return;
            // A last page that waits for a choice (a quest on offer): focus to the first choice, queued
            // after the page so the page is heard whole.
            if (box.IsEndedAndHalted())
            {
                if (!_haltHandled && _firstChoice != null)
                {
                    _haltHandled = true;
                    // Silently, then queued: a move's readout interrupts, and the page may still be read.
                    Navigation.FocusNode(_firstChoice, announce: false);
                    if (Navigation.FocusActive()) Speech.Say(GameNodes.LabelOf(_firstChoiceButton));
                }
            }
            else _haltHandled = false;
            // The speaker's name is set the frame the talk starts; wait for it.
            if (string.IsNullOrWhiteSpace(GameText.Of(box.v_TalkingName)) && box.mOverlandEventState != Talkbox.eOverlandEventStates.Options) return;
            if (ReferenceEquals(box.mTextArray, _pageArray) && box.mCurrentTalkTextIndex == _pageIndex && box.mOverlandEventState == _pageState) return;
            _pageArray = box.mTextArray;
            _pageIndex = box.mCurrentTalkTextIndex;
            _pageState = box.mOverlandEventState;
            if (!Navigation.FocusActive()) return;
            string speaker = Speaker(box);
            var parts = new List<string>();
            if (speaker != _speaker && !string.IsNullOrWhiteSpace(speaker)) parts.Add(speaker);
            _speaker = speaker;
            parts.Add(Page(box));
            string line = GameNodes.Sentences(parts);
            if (!string.IsNullOrWhiteSpace(line)) Speech.Say(line);
        }

        public override void Build(GraphBuilder b)
        {
            var box = Box;
            if (box == null) return;
            b.PushContext(null, null);
            bool options = box.mTalkMode == Talkbox.eTalkModes.eOverlandEvent && box.mOverlandEventState == Talkbox.eOverlandEventStates.Options;
            if (!options)
            {
                var page = GameNodes.Button(() => Page(Box), Advance);
                page.ControlType = ControlTypes.Text;
                page.Details = () => GameNodes.Lines(Speaker(Box));
                b.AddItem(ControlId.Structural("talk:page"), page);
            }
            else if (box.mOverlandEvent != null)
            {
                b.AddItem(ControlId.Structural("talk:event"), GameNodes.Text(() => GameText.Line(Box?.mOverlandEvent?.pIngameName)));
            }
            if (box.v_ButtonsOptions != null)
                foreach (var option in box.v_ButtonsOptions)
                {
                    if (!GameNodes.IsShown(option)) continue;
                    var vt = GameNodes.Button(option);
                    vt.QuietVanish = true;
                    b.AddItem(ControlId.ForObject(option), vt);
                }
            // The scene's own buttons beside the talk (Accept and Refuse in town), not the talk box's
            // click-anywhere selector, nor the figures standing in the scene.
            var root = GameScreens.InputRoot();
            _firstChoice = null;
            foreach (var button in GameScreens.LiveButtons(root))
            {
                if (button == box.v_Selector || button.transform.IsChildOf(box.transform)) continue;
                if (GameScreens.IsFigure(button)) continue;
                var id = ControlId.ForObject(button);
                if (_firstChoice == null) { _firstChoice = id; _firstChoiceButton = button; }
                var vt = GameNodes.ForButton(button);
                // A choice goes once taken; the page it leads to is said by the screen itself.
                vt.QuietVanish = true;
                b.AddItem(id, vt);
            }
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => GenericScreen.Cancel());
        }

        // One press, one page: the typewriter finished and the page turned, as two clicks would.
        private static void Advance()
        {
            var box = Box;
            if (box == null || box.IsEndedAndHalted()) return;
            if (!box.v_TalkingText.IsFinished()) box.v_TalkingText.MakeFull();
            box.EventTalkAdvance();
        }

        // The page as the game will show it whole (the mesh holds only what has typed out so far).
        private static string Page(Talkbox box)
        {
            if (box == null || box.mTextArray == null) return null;
            int i = box.mCurrentTalkTextIndex;
            if (i < 0 || i >= box.mTextArray.Count) return null;
            return GameText.Line(box.v_TalkingText != null && box.v_TalkingText.mText != null ? box.v_TalkingText.mText : box.mTextArray[i]);
        }

        // "Name", with the quest and its dungeon while the NPC offers or holds one.
        private static string Speaker(Talkbox box)
        {
            if (box == null) return null;
            return GameNodes.Sentences(GameText.Of(box.v_TalkingName), GameText.Of(box.v_TalkingQuestName), GameText.Of(box.v_TalkingDungeonName));
        }
    }
}
