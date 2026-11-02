using System.Collections.Generic;
using PixelHeroesAccess.Core.Graph;
using PixelHeroesAccess.Core.Strings;
using PixelHeroesAccess.Core.UI;
using PixelHeroesAccess.Module.Interop;
using PixelHeroesAccess.Module.UI;
using UnityEngine;

namespace PixelHeroesAccess.Module.Screens
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>
    /// The town (<see cref="Town"/>), whose places the game reaches by walking the party along one
    /// street (clicking ahead, or holding the pad), a context button appearing beside whatever stands
    /// near. Here the places are a list in street order, left to right: the four buildings (Tavern,
    /// Casino, Temple, Library, by the game's own names), the blacksmith, the "!!!" guys, and the
    /// townspeople (by the middle of the stretch each wanders, so the order holds still), each with
    /// what their quest stands at ("offers a quest" and its name, "quest in
    /// progress", "quest complete"). Enter goes there: the party is placed at the door or beside the
    /// person and the game's own path takes over (a building's door opens and the scene changes, the
    /// blacksmith's shop opens, a person starts talking), so nothing waits on a walk. After them, in
    /// the same stop as a region of its own, the game's interface (Leave Town, Inventory, Quest Log, Pause), the gold and the quest the
    /// party is on. Talks, the shop, the reward screen and the popups are their own screens; while the
    /// party walks into a building nothing is listed.
    /// </summary>
    public sealed class TownScreen : SceneScreen<Town>
    {
        public override string Key => "game.town";

        protected override tk2dButton PauseButton(Town game) => game.v_Button_Pause;
        protected override bool CancelActs(Town game) => game.v_Lootscreen != null && game.v_Lootscreen.IsActive();

        public override void Build(GraphBuilder b)
        {
            var town = Game;
            if (town == null || town.mState != Town.eStates.eFree) return;

            // One stop: the street, then the interface, each a region (Alt+Up/Down between them).
            b.BeginStop("town");
            b.SetRegion("town:places");
            b.PushContext(Strings.TownPlaces, Strings.RoleList);
            var places = new List<Place>();
            for (int k = 0; k < (int)Town.eBuildings.COUNT; k++)
            {
                int building = k;
                places.Add(new Place(town.GetDoorPos(building), "town:building:" + building,
                    GameNodes.Button(() => BuildingName((Town.eBuildings)building), () => EnterBuilding(building))));
            }
            places.Add(new Place(Town.mPOS_BLACKSMITH, "town:blacksmith",
                GameNodes.Button(() => GameText.Tr(ET.Shop_Blacksmith), () => Game?.EventButtonContextBlacksmith())));
            places.Add(new Place(Town.mPOS_EXCLAMATION_GUYS, "town:exclamation",
                GameNodes.Button(() => GameText.Tr(ET.Town_ExclamationGuysName), () => Game?.EventButtonContextExclamationGuys())));
            if (town.mNPCs != null)
            {
                foreach (var person in town.mNPCs)
                {
                    if (person == null || !person.gameObject.activeInHierarchy) continue;
                    var npc = person;
                    // People wander within a range; its middle keeps the list's order still.
                    float home = npc.mPositionXMax > npc.mPositionXMin ? (npc.mPositionXMin + npc.mPositionXMax) / 2f : npc.transform.localPosition.x;
                    places.Add(new Place(home, null,
                        GameNodes.Button(() => NpcLabel(npc), () => TalkTo(npc)), npc));
                }
            }
            places.Sort((x, y) => x.X.CompareTo(y.X));
            foreach (var place in places)
                b.AddItem(place.Npc != null ? ControlId.ForObject(place.Npc) : ControlId.Structural(place.Id), place.Node);
            b.PopContext();

            b.SetRegion("town:menu");
            b.PushContext(null, null);
            if (town.v_Container_InterfaceFree != null)
                foreach (var button in GameScreens.LiveButtons(town.v_Container_InterfaceFree.transform))
                    b.AddItem(ControlId.ForObject(button), GameNodes.ForButton(button));
            if (GameNodes.IsShown(town.v_Button_Pause) && (town.v_Container_InterfaceFree == null || !town.v_Button_Pause.transform.IsChildOf(town.v_Container_InterfaceFree.transform)))
                b.AddItem(ControlId.ForObject(town.v_Button_Pause), GameNodes.Button(town.v_Button_Pause));
            b.AddItem(ControlId.Structural("town:gold"), GameNodes.Text(TavernScreen.GoldLine));
            b.AddItem(ControlId.Structural("town:quest"), GameNodes.Text(QuestLine));
            b.PopContext();
        }

        private sealed class Place
        {
            public readonly float X;
            public readonly string Id;
            public readonly NodeVtable Node;
            public readonly NPC Npc;

            public Place(float x, string id, NodeVtable node, NPC npc = null)
            {
                X = x;
                Id = id;
                Node = node;
                Npc = npc;
            }
        }

        private static string BuildingName(Town.eBuildings building)
        {
            switch (building)
            {
                case Town.eBuildings.eTavern: return GameText.Tr(ET.Town_Building_Tavern);
                case Town.eBuildings.eCasino: return GameText.Tr(ET.Town_Building_Casino);
                case Town.eBuildings.eTemple: return GameText.Tr(ET.Town_Building_Temple);
                default: return GameText.Tr(ET.Town_Building_Library);
            }
        }

        // The game's own entry: the target building set, the party at its door, the walk finishing
        // at once, the door opening (EventButtonContextBuilding with the walk already done).
        private static void EnterBuilding(int building)
        {
            var town = Game;
            if (town == null || town.mState == Town.eStates.eLocked || !town.CancelTalking()) return;
            float door = town.GetDoorPos(building);
            Town.pPositionCurrent = door;
            town.mTargetBuilding = (Town.eBuildings)building;
            town.SetNewDestination(door);
        }

        // Beside the person, then the game's own talk (TalkToNPCFromContext without the context row).
        private static void TalkTo(NPC npc)
        {
            var town = Game;
            if (town == null || npc == null || town.mState == Town.eStates.eLocked || !town.CancelTalking()) return;
            Town.pPositionCurrent = Mathf.Clamp(npc.transform.localPosition.x, 32f, 720f);
            town.StartTalking(npc);
        }

        /// <summary>"Name, offers a quest: The Lost Ring".</summary>
        public static string NpcLabel(NPC npc)
        {
            string name = GameText.Line(npc.GetName());
            switch (npc.GetQuestState())
            {
                case NPC.eQuestStates.Proposing: return name + ", " + Strings.NpcOffersQuest(GameText.Line(npc.GetQuestName()));
                case NPC.eQuestStates.Active: return name + ", " + Strings.NpcQuestActive(GameText.Line(npc.GetQuestName()));
                case NPC.eQuestStates.JustFinished: return name + ", " + Strings.NpcQuestDone;
                default: return name;
            }
        }

        /// <summary>The quest the party is on and its dungeon, or that there is none.</summary>
        public static string QuestLine()
        {
            var session = GameCore.pGameSession;
            var npc = session != null ? session.pNPCCurrentlyActiveQuest : null;
            if (npc == null) return Strings.QuestNone;
            var quest = npc.GetCurrentQuest();
            string dungeon = DataDungeons.GetDungeonNameFromID(npc.GetQuestDungeon());
            return Strings.QuestCurrent(GameText.Line(quest != null ? quest.pQuestName : ""), GameText.Line(dungeon));
        }
    }
}
