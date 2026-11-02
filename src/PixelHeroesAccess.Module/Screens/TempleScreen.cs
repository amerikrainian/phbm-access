using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The temple (<see cref="Temple"/>). Its choice (<see cref="Temple.eStates.Decision"/>): Talk,
    /// Revive (offered while a hero is dead), Buy potions, Leave, and the gold. Revive
    /// (<see cref="Temple.eStates.ReviveDialog"/>): a control per dead hero, "Revive Ghada, 120 gold",
    /// disabled when the party cannot pay (the game's own price,
    /// <see cref="DataBalancing.GetRevivalCosts"/>), then Cancel. The potion shop
    /// (<see cref="Temple.eStates.PotionShop"/>): each potion "Small Healing Potion, carried 2, 30 gold",
    /// then Cancel; a purchase says how many the party now carries. The walks in and out, the revive's
    /// animation and the talk (<see cref="TalkScreen"/>) list nothing here.
    /// </summary>
    public sealed class TempleScreen : SceneScreen<Temple>
    {
        public override string Key => "game.temple";

        protected override tk2dButton PauseButton(Temple game) => game.v_ButtonPause;
        protected override bool CancelActs(Temple game) => game.mState == Temple.eStates.PotionShop || game.mState == Temple.eStates.ReviveDialog;

        public override void Build(GraphBuilder b)
        {
            var temple = Game;
            if (temple == null) return;
            b.PushContext(null, null);
            switch (temple.mState)
            {
                case Temple.eStates.Decision:
                    foreach (var button in GameScreens.LiveButtons(temple.v_Container_InterfaceDecision.transform))
                        b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
                    break;
                case Temple.eStates.ReviveDialog:
                    int shown = 0;
                    for (int i = 0; i < temple.pHeroes.Length; i++)
                    {
                        var hero = temple.pHeroes[i];
                        if (hero == null || hero.IsAlive()) continue;
                        int slot = shown++;
                        if (slot >= temple.v_ButtonsRevive.Length) break;
                        var button = temple.v_ButtonsRevive[slot];
                        int index = i;
                        var vt = GameNodes.Button(button, () => Strings.TempleRevive(GameText.Line(Game?.pHeroes[index].GetName()),
                            Strings.Cost(DataBalancing.GetRevivalCosts(Game?.pHeroes[index]).ToString())));
                        vt.SideLines = HeroNodes.Sides(() => Game?.pHeroes[index]);
                        b.AddItem(ControlId.ForObject(button), vt);
                    }
                    if (GameNodes.IsShown(temple.v_Revive_Cancel)) b.AddItem(ControlId.ForObject(temple.v_Revive_Cancel), GameNodes.Button(temple.v_Revive_Cancel));
                    break;
                case Temple.eStates.PotionShop:
                    Potion(b, temple.v_PotionShop_ButtonSmall, temple.v_PotionShop_NameSmall, ItemPotion.ePotionTypes.Small);
                    Potion(b, temple.v_PotionShop_ButtonBig, temple.v_PotionShop_NameBig, ItemPotion.ePotionTypes.Big);
                    if (GameNodes.IsShown(temple.v_PotionShop_Cancel)) b.AddItem(ControlId.ForObject(temple.v_PotionShop_Cancel), GameNodes.Button(temple.v_PotionShop_Cancel));
                    break;
                default:
                    b.PopContext();
                    return;
            }
            if (GameNodes.IsShown(temple.v_ButtonPause)) b.AddItem(ControlId.ForObject(temple.v_ButtonPause), GameNodes.Button(temple.v_ButtonPause));
            b.AddItem(ControlId.Structural("temple:gold"), GameNodes.Text(TavernScreen.GoldLine));
            b.PopContext();
        }

        private static void Potion(GraphBuilder b, tk2dButton button, tk2dTextMesh name, ItemPotion.ePotionTypes type)
        {
            if (!GameNodes.IsShown(button)) return;
            b.AddItem(ControlId.ForObject(button), GameNodes.Button(() => PotionLabel(name, type), () =>
            {
                if (!GameNodes.Usable(button)) return;
                int before = GameCore.pGameSession.GetPotionCount(type);
                button.TriggerClick();
                if (GameCore.pGameSession.GetPotionCount(type) > before) Purchase.Said();
                else Speech.Say(Strings.ShopTooExpensive, interrupt: true);
            }, () => GameNodes.Usable(button)));
        }

        // "Small Healing Potion, carried 2, 30 gold".
        private static string PotionLabel(tk2dTextMesh name, ItemPotion.ePotionTypes type)
        {
            var session = GameCore.pGameSession;
            return string.Join(", ", new[]
            {
                GameText.Of(name),
                Strings.TempleCarried(session.GetPotionCount(type).ToString()),
                Strings.Cost(DataBalancing.GetPotionCosts(type).ToString()),
            });
        }
    }
}
