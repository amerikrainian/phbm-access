using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The credits (<see cref="Credits"/>), which the game scrolls by itself in a loop: the title, the
    /// credits a line each, the copyright, then the screen's buttons; Escape goes back to the title
    /// menu (the game's own cancel).
    /// </summary>
    public sealed class CreditsScreen : SceneScreen<Credits>
    {
        public override string Key => "game.credits";

        public override void Build(GraphBuilder b)
        {
            var credits = Game;
            if (credits == null) return;
            b.PushContext(null, null);
            var lines = new List<string>();
            string title = GameText.Of(credits.v_TextMesh_Title);
            if (title != null) lines.Add(title);
            if (credits.v_TextMesh_Credits != null) lines.AddRange(GameText.Lines(credits.v_TextMesh_Credits.text));
            string copyright = GameText.Of(credits.v_TextMesh_Copyright);
            if (copyright != null) lines.Add(copyright);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                b.AddItem(ControlId.Structural("credits:" + i), GameNodes.Text(() => line));
            }
            foreach (var button in GameScreens.LiveButtons(credits.transform))
                if (!GameScreens.IsFigure(button)) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            b.PopContext();
        }
    }
}
