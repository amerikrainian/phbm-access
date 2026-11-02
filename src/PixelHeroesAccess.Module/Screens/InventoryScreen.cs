using System.Collections.Generic;
using PixelHeroesAccess.Core;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Screens;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.Nodes;
using PixelHeroesAccess.Module.UI;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The inventory (<see cref="Inventory"/>: between a dungeon's rooms, and from town or a shop),
    /// where the game equips by dragging an item onto a slot, the backpack, the trash or a hero.
    /// Here: the party, a row per hero (its state, then its five slots: two hands, body, head,
    /// trinket); the attributes to raise, for each hero with points to spend (a row of the five, each
    /// with its value and what a point adds; Enter spends one, as the game's plus buttons do); the
    /// backpack; the game's buttons (Enter Room or Town, Quest Log, Help, Pause), the gold and the
    /// backpack against its limit. Enter on an item opens what can be done with it: on a hero, move it
    /// to the other hand, put it in the backpack, throw it away; in the backpack, equip it on each
    /// living hero's slot that takes it (a row per hero, what the slot holds now as the detail, which
    /// goes to the backpack), drink a potion (a row of the heroes), throw it away. Enter on a hero offers
    /// to swap places with another (the game's order is the battle line). Every move is the game's
    /// own drop (<see cref="Inventory.PerformItemDrop"/> with the drop target set as a drag would set
    /// it), so the game's bookkeeping holds (health kept in proportion, the old item to the backpack's
    /// end). Throwing away asks first: the game does not.
    /// </summary>
    public sealed class InventoryScreen : SceneScreen<Inventory>
    {
        public override string Key => "game.inventory";

        public override void Build(GraphBuilder b)
        {
            var inv = Game;
            if (inv == null || inv.pHeroes == null) return;

            b.BeginStop("inv:party");
            b.PushContext(Strings.BattleParty, Strings.RoleList);
            for (int i = 0; i < inv.pHeroes.Length; i++)
            {
                var hero = inv.pHeroes[i];
                if (hero == null) continue;
                int index = i;
                b.PushContext(GameText.Line(hero.GetName()), null, positions: false);
                b.StartRow("inv:hero:" + index);
                b.AddItem(ControlId.Structural("inv:hero:" + index), HeroNodes.Hero(() => Hero(index), () => HeroState(Hero(index)), () => OpenHeroMenu(index)));
                for (int s = 0; s < 5; s++)
                {
                    int slot = s;
                    b.AddItem(ControlId.Structural("inv:slot:" + index + ":" + slot), HeroNodes.Item(
                        () => Slot(Hero(index), slot), () => Hero(index),
                        () => OpenEquippedMenu(index, slot),
                        () => Strings.SlotLine(HeroLines.SlotName(slot), Slot(Hero(index), slot) != null ? ItemLines.Summary(Slot(Hero(index), slot), Hero(index)) : Strings.SlotEmpty)));
                }
                b.EndRow();
                b.PopContext();
            }
            b.PopContext();

            bool anyPoints = false;
            foreach (var hero in inv.pHeroes) if (hero != null && hero.GetSkillPointsCurrent() > 0) anyPoints = true;
            if (anyPoints)
            {
                b.BeginStop("inv:levelup");
                b.PushContext(Strings.InventoryLevelUp, Strings.RoleList);
                for (int i = 0; i < inv.pHeroes.Length; i++)
                {
                    var hero = inv.pHeroes[i];
                    if (hero == null || hero.GetSkillPointsCurrent() <= 0) continue;
                    int index = i;
                    b.PushContext(GameText.Line(hero.GetName()) + ", " + Strings.HeroPoints(hero.GetSkillPointsCurrent().ToString()), null, positions: false);
                    b.StartRow("inv:points:" + index);
                    for (int a = 0; a < (int)eHeroAttributes.COUNT; a++)
                    {
                        var attribute = (eHeroAttributes)a;
                        b.AddItem(ControlId.Structural("inv:points:" + index + ":" + a), GameNodes.Button(
                            () => AttributeLabel(Hero(index), attribute), () => Raise(index, attribute),
                            () => Hero(index) != null && Hero(index).GetSkillPointsCurrent() > 0));
                    }
                    b.EndRow();
                    b.PopContext();
                }
                b.PopContext();
            }

            b.BeginStop("inv:backpack");
            b.PushContext(GameText.Tr(ET.GUI_SH_B_Backpack), Strings.RoleList);
            if (inv.pItemsBackpack != null)
                foreach (var carried in inv.pItemsBackpack)
                {
                    if (carried == null) continue;
                    var item = carried;
                    var vt = HeroNodes.Item(() => item, null, () => OpenBackpackMenu(item), reckon: () => Game?.mSelectedHero);
                    vt.QuietVanish = true;
                    b.AddItem(ControlId.Structural(("inv:backpack", (object)item)), vt);
                }
            if (inv.pItemsBackpack == null || inv.pItemsBackpack.Count == 0)
                b.AddItem(ControlId.Structural("inv:backpack:none"), GameNodes.Text(() => Strings.ListEmpty));
            b.PopContext();

            b.BeginStop("inv:menu");
            b.PushContext(null, null);
            foreach (var button in new[] { inv.v_ButtonRoomOverview, inv.v_ButtonTown, inv.v_ButtonShop, inv.v_ButtonQuestLog, inv.v_ButtonHelp, inv.v_ButtonPause })
                if (GameNodes.IsShown(button)) b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            b.AddItem(ControlId.Structural("inv:gold"), GameNodes.Text(TavernScreen.GoldLine));
            b.AddItem(ControlId.Structural("inv:backpack:count"), GameNodes.Text(ShopScreen.BackpackLine));
            b.PopContext();
        }

        private static CharacterHero Hero(int index)
        {
            var inv = Game;
            return inv != null && inv.pHeroes != null && index < inv.pHeroes.Length ? inv.pHeroes[index] : null;
        }

        /// <summary>A slot's item by the combined index: 0 and 1 the hands, 2 body, 3 head, 4 trinket.</summary>
        public static Item Slot(CharacterHero hero, int slot)
        {
            if (hero == null) return null;
            if (slot < 2) return hero.pItemsActive[slot];
            return hero.pItemsPassive[slot - 2];
        }

        private static string HeroState(CharacterHero hero)
        {
            if (hero == null) return null;
            var parts = new List<string>
            {
                GameText.Line(hero.GetNameRace() + " " + hero.GetNameClass()),
                Strings.ItemPair(GameText.Tr(ET.Inventory_Hero_Level), hero.GetLevel().ToString()),
                HeroLines.Health(hero),
            };
            if (hero.GetSkillPointsCurrent() > 0) parts.Add(Strings.HeroPoints(hero.GetSkillPointsCurrent().ToString()));
            return string.Join(", ", parts);
        }

        private static string AttributeLabel(CharacterHero hero, eHeroAttributes attribute)
        {
            if (hero == null) return null;
            string gain = hero.IsShapeshifter() ? "?" : hero.GetAttributeLevelUpValue(attribute).ToString();
            return Strings.InventoryAttribute(GameText.AttributeName(attribute), hero.GetAttribute(attribute).ToString(), gain);
        }

        // ---- the game's own moves ----

        // The drag's start: the hero and the item selected as the game's click selects them.
        private static bool Pick(Inventory inv, CharacterHero hero, Item item)
        {
            if (inv.mSelectedHero != hero) inv.SelectHero(hero);
            inv.SelectItem(item, false);
            return inv.mSelectedHero == hero && inv.mSelectedItem == item;
        }

        // The drag's end: the drop target set as hovering it sets it, then the game's drop.
        private static void Drop(Inventory inv, Inventory.eDragAndDropState target, int backpackSlot = 0)
        {
            inv.mDragAndDropState = target;
            inv.PerformItemDrop(backpackSlot);
        }

        private static readonly Inventory.eDragAndDropState[] SlotTargets =
        {
            Inventory.eDragAndDropState.eOverHandLeft,
            Inventory.eDragAndDropState.eOverHandRight,
            Inventory.eDragAndDropState.eOverBody,
            Inventory.eDragAndDropState.eOverHead,
            Inventory.eDragAndDropState.eOverTrinket,
        };

        private static void Equip(CharacterHero hero, Item item, int slot)
        {
            var inv = Game;
            if (inv == null || hero == null || item == null) return;
            if (!hero.IsAlive()) { Speech.Say(Strings.HeroDead, interrupt: true); return; }
            // The game drops onto equipment only while its panel shows the items (not the skills).
            if (hero.pBattleHandDisplayMode != BattleGUI.eHandModes.eModeItems) inv.SetHandMode(BattleGUI.eHandModes.eModeItems);
            if (!Pick(inv, hero, item)) return;
            inv.mDragAndDropItemIsFromBackpack = inv.pItemsBackpack.Contains(item);
            Drop(inv, SlotTargets[slot]);
            Speech.Say(Strings.InventoryEquipped(ItemLines.Name(item), GameText.Line(hero.GetName()), HeroLines.SlotName(slot)), interrupt: true);
        }

        private static void Unequip(CharacterHero hero, Item item)
        {
            var inv = Game;
            if (inv == null || hero == null || item == null || !Pick(inv, hero, item)) return;
            Drop(inv, Inventory.eDragAndDropState.eOverBackpack, inv.pItemsBackpack.Count);
            Speech.Say(Strings.InventoryUnequipped(ItemLines.Name(item)), interrupt: true);
        }

        private static void Trash(CharacterHero hero, Item item)
        {
            var inv = Game;
            if (inv == null || item == null) return;
            string name = ItemLines.Name(item);
            if (!Pick(inv, hero ?? inv.mSelectedHero, item)) return;
            Drop(inv, Inventory.eDragAndDropState.eOverTrash);
            if (inv.mSelectedItem == null) Speech.Say(Strings.InventoryTrashed(name), interrupt: true);
        }

        private static void Drink(Item potion, CharacterHero hero)
        {
            var inv = Game;
            if (inv == null || potion == null || hero == null) return;
            int before = hero.GetHPCurrent();
            inv.SelectItem(potion, false);
            inv.UsePotion(hero);
            if (hero.GetHPCurrent() > before)
                Speech.Say(GameText.Line(hero.GetName()) + ", " + HeroLines.Health(hero), interrupt: true);
            else Speech.Say(Strings.ShopRefused, interrupt: true);
        }

        private static void Raise(int index, eHeroAttributes attribute)
        {
            var inv = Game;
            var hero = Hero(index);
            if (inv == null || hero == null || hero.GetSkillPointsCurrent() <= 0) return;
            if (inv.mSelectedHero != hero) inv.SelectHero(hero);
            inv.LevelUpAttribute(attribute);
            Speech.Say(AttributeLabel(hero, attribute) + ", " + Strings.HeroPoints(hero.GetSkillPointsCurrent().ToString()), interrupt: true);
        }

        private static void Swap(int from, int to)
        {
            var inv = Game;
            var a = Hero(from);
            var b = Hero(to);
            if (inv == null || a == null || b == null || from == to) return;
            if (inv.mSelectedHero != a) inv.SelectHero(a);
            inv.DragDropHeroEventDrop(b.gameObject);
            Speech.Say(Strings.InventorySwapped(GameText.Line(a.GetName()), (to + 1).ToString()), interrupt: true);
        }

        // ---- the menus ----

        private static void OpenHeroMenu(int index)
        {
            var hero = Hero(index);
            if (hero == null) return;
            var options = new List<ChoiceOption>();
            for (int i = 0; i < 3; i++)
            {
                var other = Hero(i);
                if (i == index || other == null) continue;
                int to = i;
                options.Add(new ChoiceOption(Strings.InventorySwapWith(GameText.Line(other.GetName()), (to + 1).ToString()), () => Swap(index, to)));
            }
            ChoiceSubmenuScreen.Open(GameText.Line(hero.GetName()), options);
        }

        private static void OpenEquippedMenu(int index, int slot)
        {
            var hero = Hero(index);
            var item = Slot(hero, slot);
            if (hero == null || item == null) return;
            var options = new List<ChoiceOption>();
            if (slot < 2)
            {
                int other = 1 - slot;
                var there = Slot(hero, other);
                options.Add(new ChoiceOption(Strings.InventoryMoveTo(HeroLines.SlotName(other)), () => Equip(hero, item, other),
                    there != null ? ItemLines.Name(there) : Strings.SlotEmpty));
            }
            options.Add(new ChoiceOption(Strings.InventoryToBackpack, () => Unequip(hero, item)));
            options.Add(new ChoiceOption(Strings.InventoryThrowAway, () => ConfirmTrash(hero, item)));
            ChoiceSubmenuScreen.Open(ItemLines.Name(item), options);
        }

        private static void OpenBackpackMenu(Item item)
        {
            var inv = Game;
            if (inv == null || item == null) return;
            var options = new List<ChoiceOption>();
            if (item.GetItemType() == eItemTypes.Potion)
            {
                foreach (var h in inv.pHeroes)
                {
                    if (h == null) continue;
                    var hero = h;
                    bool usable = hero.IsAlive() && hero.GetHPCurrent() < hero.GetHPMax() && item.pItemPotion.GetCount() > 0;
                    options.Add(new ChoiceOption(GameText.Line(hero.GetName()), () => Drink(item, hero), HeroLines.Health(hero), usable,
                        row: GameText.Tr(ET.GUI_IN_B_UsePotion)));
                }
            }
            else if (item.pItemActive != null && !item.IsSkill())
            {
                foreach (var h in inv.pHeroes)
                {
                    if (h == null || !h.IsAlive()) continue;
                    var hero = h;
                    for (int s = 0; s < 2; s++)
                    {
                        int slot = s;
                        var there = Slot(hero, slot);
                        options.Add(new ChoiceOption(HeroLines.SlotName(slot), () => Equip(hero, item, slot),
                            there != null ? ItemLines.Name(there) : Strings.SlotEmpty, row: GameText.Line(hero.GetName())));
                    }
                }
            }
            else if (item.pItemPassive != null)
            {
                int slot = 2 + (int)item.pItemPassive.GetItemPassiveType();
                foreach (var h in inv.pHeroes)
                {
                    if (h == null || !h.IsAlive()) continue;
                    var hero = h;
                    var there = Slot(hero, slot);
                    options.Add(new ChoiceOption(GameText.Line(hero.GetName()), () => Equip(hero, item, slot),
                        there != null ? ItemLines.Name(there) : Strings.SlotEmpty, row: Strings.InventoryEquipSlot(HeroLines.SlotName(slot))));
                }
            }
            if (item.GetItemType() != eItemTypes.Potion)
                options.Add(new ChoiceOption(Strings.InventoryThrowAway, () => ConfirmTrash(null, item)));
            ChoiceSubmenuScreen.Open(ItemLines.Name(item), options);
        }

        // The game throws away on the drop alone; here it asks first.
        private static void ConfirmTrash(CharacterHero hero, Item item)
        {
            var options = new List<ChoiceOption>
            {
                new ChoiceOption(Strings.KeysNo, () => { }),
                new ChoiceOption(Strings.InventoryThrowAwayConfirm(ItemLines.Name(item)), () => Trash(hero, item)),
            };
            ChoiceSubmenuScreen.Open(Strings.InventoryThrowAwayAsk(ItemLines.Name(item)), options);
        }
    }
}

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The dungeon's room overview (<see cref="Inventory.v_Popup_RoomOverview"/>), which draws the rooms
    /// as a row of pictures, the cleared ones marked and the next one moving: said as words, the
    /// dungeon and "room 3 of 8, the boss in room 8" (eight rooms, the last the boss's:
    /// <see cref="eDungeonRooms"/>), then the popup's buttons (Enter Room, Back, Quest Log).
    /// </summary>
    public sealed class RoomOverviewScreen : PopupScreen
    {
        public override string Key => "inventory.rooms";

        private static Inventory Game => Interop.GameScreens.Current<Inventory>();

        protected override Popup Resolve() => Game != null ? Game.v_Popup_RoomOverview : null;

        public override void Build(Core.Graph.GraphBuilder b)
        {
            var inv = Game;
            if (inv == null) return;
            b.PushContext(null, null);
            b.AddItem(Core.Graph.ControlId.Structural("rooms:where"), UI.GameNodes.Text(Where));
            var buttons = Interop.GameScreens.LiveButtons(inv.v_Popup_RoomOverview.transform);
            // Enter Room first: the reason the popup is open.
            buttons.Sort((x, y) => (y.gameObject.name.Contains("Enter") ? 1 : 0) - (x.gameObject.name.Contains("Enter") ? 1 : 0));
            foreach (var button in buttons)
                b.AddItem(Core.Graph.ControlId.ForObject(button), UI.GameNodes.ForButton(button));
            if (buttons.Count > 0) b.SetStart(Core.Graph.ControlId.ForObject(buttons[0]));
            b.PopContext();
        }

        private static string Where()
        {
            var session = GameCore.pGameSession;
            var dungeon = GameCore.GetCurrentDungeon();
            string name = dungeon != null ? Interop.GameText.Line(dungeon.GetData().pName) : null;
            if (session == null || GameCore.GetGameState() != eGameStates.eDungeon) return name;
            int next = (int)session.pNextRoom + 1;
            int total = (int)eDungeonRooms.COUNT;
            return UI.GameNodes.Sentences(name, Core.Strings.Strings.RoomNext(next.ToString(), total.ToString()));
        }
    }
}
