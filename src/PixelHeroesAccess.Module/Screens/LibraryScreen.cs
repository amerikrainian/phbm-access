using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The library (<see cref="Library"/>). Its choice (<see cref="Library.eStates.eDecision"/>): Talk
    /// (the librarian, <see cref="TalkScreen"/>), Shop (the bookseller, <see cref="ShopScreen"/>), Leave,
    /// Pause, and the gold. The walks in and out list nothing.
    /// </summary>
    public sealed class LibraryScreen : SceneScreen<Library>
    {
        public override string Key => "game.library";

        public override void Build(GraphBuilder b)
        {
            var library = Game;
            if (library == null || library.mState != Library.eStates.eDecision) return;
            b.PushContext(null, null);
            foreach (var button in GameScreens.LiveButtons(library.transform))
                if (!GameScreens.IsFigure(button)) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            b.AddItem(ControlId.Structural("library:gold"), GameNodes.Text(TavernScreen.GoldLine));
            b.PopContext();
        }
    }
}
