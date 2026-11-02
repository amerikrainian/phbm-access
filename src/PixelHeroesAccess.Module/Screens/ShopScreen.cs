using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;
using Screen = PixelHeroesAccess.Core.Screens.Screen;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// A shop (<see cref="Shop.pActiveShop"/>: the blacksmith in town, the library's bookseller, the
    /// arena's two). The game's own shop is select-then-press: click an item (the stock, the backpack
    /// on one tab, the selected hero's equipment on the other), then Buy or Sell. Here an item is one
    /// press. The stock: each offer "summary, price", with "not enough gold" when the party cannot
    /// pay; Enter buys it. The backpack: each item with what it sells for (potions are not sold, as
    /// in the game); Enter sells it. The equipment: a row per hero of its five slots (as in the
    /// inventory), Enter sells what is in one.
    /// Then the shop's buttons (Inventory, Close, Help), the gold and the backpack count against its
    /// limit (the game lets a purchase exceed it; only leaving town is then refused). Every press
    /// goes through the game: the tab, the hero and the item selected as clicks select them, then the
    /// Buy/Sell button's own click, so its checks hold; the result is said a few frames on.
    /// </summary>
    public sealed class ShopScreen : Screen
    {
        public override string Key => "game.shop";
        public override int Layer => 8;
        public override bool KeepStateOnPop => true;

        private static Shop ShopOpen => GameScreens.OpenShop();

        public override bool IsActive()
            => ShopOpen != null && ScreenBase.pCurrentInstance != null && Popup.GetTopMostActivePopup() == null;

        public override void OnPush()
        {
            var shop = ShopOpen;
            if (shop != null) ScreenIntro.Say(GameText.Of(shop.v_ShopTitle));
        }

        public override void Build(GraphBuilder b)
        {
            var shop = ShopOpen;
            var screen = ScreenBase.pCurrentInstance;
            if (shop == null || screen == null) return;

            b.BeginStop("shop:stock");
            b.PushContext(GameText.Tr(ET.Shop_StockedItems), Strings.RoleList);
            if (shop.mItemsShopStock != null)
                foreach (var offer in shop.mItemsShopStock)
                {
                    if (offer == null) continue;
                    var item = offer;
                    b.AddItem(ControlId.Structural(("shop:stock", (object)item)), Quiet(HeroNodes.Item(() => item, null, () => Buy(item),
                        () => StockLabel(item), reckon: () => ShopOpen?.mSelectedHero)));
                }
            if (shop.mItemsShopStock == null || shop.mItemsShopStock.Count == 0)
                b.AddItem(ControlId.Structural("shop:stock:none"), GameNodes.Text(() => Strings.ListEmpty));
            b.PopContext();

            b.BeginStop("shop:backpack");
            b.PushContext(GameText.Tr(ET.GUI_SH_B_Backpack), Strings.RoleList);
            if (screen.pItemsBackpack != null)
                foreach (var carried in screen.pItemsBackpack)
                {
                    if (carried == null) continue;
                    var item = carried;
                    b.AddItem(ControlId.Structural(("shop:backpack", (object)item)), Quiet(HeroNodes.Item(() => item, null, () => SellFromBackpack(item),
                        () => SellLabel(item, null), reckon: () => ShopOpen?.mSelectedHero)));
                }
            if (screen.pItemsBackpack == null || screen.pItemsBackpack.Count == 0)
                b.AddItem(ControlId.Structural("shop:backpack:none"), GameNodes.Text(() => Strings.ListEmpty));
            b.PopContext();

            b.BeginStop("shop:equipment");
            b.PushContext(GameText.Tr(ET.GUI_SH_B_Equiptment), Strings.RoleList);
            // A row per hero of the five slots, as the inventory has them; one row key, so Up and Down
            // keep the slot. A sold item leaves its slot empty in place.
            if (screen.pHeroes != null)
                foreach (var member in screen.pHeroes)
                {
                    if (member == null) continue;
                    var hero = member;
                    b.PushContext(GameText.Line(hero.GetName()), null, positions: false);
                    b.StartRow("shop:equipment:row");
                    for (int s = 0; s < 5; s++)
                    {
                        int slot = s;
                        b.AddItem(ControlId.Structural(("shop:equipment", (object)hero, slot)), Quiet(HeroNodes.Item(() => Equipped(hero, slot), () => hero,
                            () => SellEquipped(hero, Equipped(hero, slot)),
                            () => Strings.SlotLine(HeroLines.SlotName(slot), SellLabel(Equipped(hero, slot), hero)))));
                    }
                    b.EndRow();
                    b.PopContext();
                }
            b.PopContext();

            b.BeginStop("shop:info");
            b.PushContext(null, null);
            // As every screen lists them (GameScreens.ButtonRank): the content, the way out, the system
            // button; then the lines.
            foreach (var button in new[] { shop.v_Button_Inventory, shop.v_ButtonClose, shop.v_Button_Help })
                if (GameNodes.IsShown(button)) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            b.AddItem(ControlId.Structural("shop:gold"), GameNodes.Text(TavernScreen.GoldLine));
            b.AddItem(ControlId.Structural("shop:backpack:count"), GameNodes.Text(BackpackLine));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ShopOpen?.EventButtonShopBack());
        }

        // An item bought or sold leaves its list; the result is said, so the landing after is quiet.
        private static NodeVtable Quiet(NodeVtable vt)
        {
            vt.QuietVanish = true;
            return vt;
        }

        /// <summary>"Backpack 5 of 20", past the limit marked as over.</summary>
        public static string BackpackLine()
        {
            var session = GameCore.pGameSession;
            if (session == null) return null;
            string line = Strings.BackpackCount(session.pItemsBackpack.Count.ToString(), GameCore.GetBackpackLimit().ToString());
            return GameCore.BackpackIsOk() ? line : line + ", " + Strings.BackpackOver;
        }

        private static Item Equipped(CharacterHero hero, int slot)
        {
            if (hero == null) return null;
            if (slot < 2) return hero.pItemsActive != null && slot < hero.pItemsActive.Length ? hero.pItemsActive[slot] : null;
            int p = slot - 2;
            return hero.pItemsPassive != null && p < hero.pItemsPassive.Length ? hero.pItemsPassive[p] : null;
        }

        private static string StockLabel(Item item)
        {
            var shop = ShopOpen;
            string label = ItemLines.Summary(item, shop?.mSelectedHero) + ", " + Strings.Cost(item.GetPrice().ToString());
            var session = GameCore.pGameSession;
            if (session != null && session.GetGold() < item.GetPrice()) label += ", " + Strings.ShopTooExpensive;
            return label;
        }

        private static string SellLabel(Item item, CharacterHero holder)
        {
            if (item == null) return Strings.SlotEmpty;
            var shop = ShopOpen;
            string summary = ItemLines.Summary(item, holder ?? shop?.mSelectedHero);
            if (item.GetItemType() == eItemTypes.Potion) return summary + ", " + Strings.ShopNotForSale;
            return summary + ", " + Strings.ShopSellsFor(item.GetPrice().ToString());
        }

        private static void Buy(Item item)
        {
            var shop = ShopOpen;
            if (shop == null || item == null) return;
            if (GameCore.pGameSession.GetGold() < item.GetPrice())
            {
                Speech.Say(Strings.ShopTooExpensive, interrupt: true);
                return;
            }
            string name = ItemLines.Name(item);
            shop.EventSelectItem(item);
            Press(shop, () => shop.mItemsShopStock != null && !shop.mItemsShopStock.Contains(item), Strings.ShopBought(name));
        }

        private static void SellFromBackpack(Item item)
        {
            var shop = ShopOpen;
            if (shop == null || item == null) return;
            if (item.GetItemType() == eItemTypes.Potion) { Speech.Say(Strings.ShopNotForSale, interrupt: true); return; }
            string name = ItemLines.Name(item);
            if (!shop.mDisplayBackpack) shop.EventButtonTabBackpack();
            shop.EventSelectItem(item);
            Press(shop, () => shop.mItemsShopStock != null && shop.mItemsShopStock.Contains(item), Strings.ShopSold(name));
        }

        private static void SellEquipped(CharacterHero hero, Item item)
        {
            var shop = ShopOpen;
            if (shop == null || hero == null || item == null) return;
            string name = ItemLines.Name(item);
            if (shop.mDisplayBackpack) shop.EventButtonTabEquipment();
            if (shop.mSelectedHero != hero) shop.EventSelectHero(hero);
            shop.EventSelectItem(item);
            Press(shop, () => shop.mItemsShopStock != null && shop.mItemsShopStock.Contains(item), Strings.ShopSold(name));
        }

        // The Buy/Sell button's own click, if the selection took; then the outcome and the gold.
        private static void Press(Shop shop, System.Func<bool> done, string success)
        {
            var button = shop.v_ButtonBuySell;
            if (shop.mSelectedItem == null || !GameNodes.Usable(button))
            {
                Speech.Say(Strings.ShopRefused, interrupt: true);
                return;
            }
            button.TriggerClick();
            Later.Frames(2, () =>
            {
                if (done()) Speech.Say(success + ", " + TavernScreen.GoldLine(), interrupt: true);
                else Speech.Say(Strings.ShopRefused, interrupt: true);
            }, "shop result");
        }
    }
}
