using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The title menu's campaign picker (<see cref="TitleMenu.v_Popup_CampaignSelect"/>): one control
    /// per campaign, its button's caption ("Campaign 2 (Brutal)", "Story Campaign") with the
    /// campaign's description (the final quest's name, or the story's title) and, while locked, the
    /// game's lock line ("Finish Campaign 1 first!"); a locked campaign is disabled. The popup's prompt
    /// is said as it opens; Back is the popup's own cancel button.
    /// </summary>
    public sealed class CampaignSelectScreen : PopupScreen
    {
        public override string Key => "title.campaigns";

        private static TitleMenu Title => GameScreens.Current<TitleMenu>();

        protected override Popup Resolve() => Title != null ? Title.v_Popup_CampaignSelect : null;

        public override void OnPush()
        {
            var popup = Resolve();
            if (popup == null) return;
            // The prompt: the popup's own title line, "Choose a campaign for your adventure!".
            ScreenIntro.Say(GameText.Tr(ET.GUI_TM_T_TitleCampaign));
        }

        public override void Build(GraphBuilder b)
        {
            var title = Title;
            if (title == null || title.v_Campaigns == null) return;
            b.PushContext(null, null);
            foreach (var campaign in title.v_Campaigns)
            {
                if (campaign == null) continue;
                var row = campaign;
                var button = row.GetAButton(2);
                if (button == null || !button.gameObject.activeInHierarchy) continue;
                var vt = GameNodes.Button(button, () => GameNodes.Sentences(
                    GameText.Under(button),
                    GameText.Of(row.GetAText(1)),
                    GameText.Of(row.GetAText(0))));
                b.AddItem(ControlId.ForObject(button), vt);
            }
            var back = GameScreens.LiveButtons(title.v_Popup_CampaignSelect.transform).Find(x => x.gameObject.name == "Button_Cancel");
            if (back != null) b.AddItem(ControlId.ForObject(back), GameNodes.Button(back));
            b.PopContext();
        }
    }
}
