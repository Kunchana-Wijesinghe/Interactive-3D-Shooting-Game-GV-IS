using UnityEngine;

namespace FacilityBreach.AI.Patrol
{
    /// <summary>
    /// Factory providing topological navigation graphs for the facility layout.
    /// Distinguishes physically verified connections from proposed unverified routes.
    /// Coordinates are derived directly from FacilityGenerator.cs and FacilityDoors.cs.
    /// </summary>
    public static class FacilityPatrolGraphFactory
    {
        // Verified Security Zone Node IDs
        public const int Node_PG_Spawn = 12;
        public const int Node_PG_Route_1 = 13;
        public const int Node_PG_Route_2 = 14;
        public const int Node_PG_Route_3 = 15;
        public const int Node_Door_Room3_Left = 16;
        public const int Node_Room3_Center = 17;
        public const int Node_Exit_Approach = 18;
        public const int Node_Door_Room2_Top = 10;
        public const int Node_Corridor_Junction = 5;

        /// <summary>
        /// Creates a clean, 100% verified gameplay graph for the upper security zone
        /// where Patrol Guard (PG-01) patrols, investigates, and responds to barricades.
        /// All coordinates are verified clear of procedural walls and cover objects.
        /// </summary>
        public static PatrolAStarGraph CreateSecurityZoneGraph()
        {
            var graph = new PatrolAStarGraph();

            // Core Patrol Route (Verified in FacilityBreachRuntime.cs and EnemyRuleValidation.cs)
            graph.AddNode(Node_PG_Spawn, new Vector3(5f, 0f, 18f), "PG_Spawn_Post", isVerified: true);
            graph.AddNode(Node_PG_Route_1, new Vector3(5f, 0f, 22f), "PG_Route_North", isVerified: true);
            graph.AddNode(Node_PG_Route_2, new Vector3(7f, 0f, 22f), "PG_Route_East", isVerified: true);
            graph.AddNode(Node_PG_Route_3, new Vector3(7f, 0f, 18f), "PG_Route_South", isVerified: true);

            // Doorway and Corridor Connections (Verified against FacilityDoors.cs and FacilityGenerator.cs)
            graph.AddNode(Node_Door_Room3_Left, new Vector3(10f, 0f, 18.75f), "Door_Room3_Left", isVerified: true);
            graph.AddNode(Node_Room3_Center, new Vector3(14f, 0f, 18f), "Room3_Machinery_Center", isVerified: true);
            graph.AddNode(Node_Exit_Approach, new Vector3(7f, 0f, 25f), "Exit_Approach_BarricadeArea", isVerified: true);
            graph.AddNode(Node_Door_Room2_Top, new Vector3(1f, 0f, 10f), "Door_Room2_Top", isVerified: true);
            // Junction at X=5, Z=12 passes safely East of Middle_Offset_Block (X=3, Z in [14, 18])
            graph.AddNode(Node_Corridor_Junction, new Vector3(5f, 0f, 12f), "Upper_Corridor_Junction", isVerified: true);

            // --- Verified Edges ---
            // 1. Primary Patrol Loop: 12 -> 13 -> 14 -> 15 -> 12
            graph.AddBidirectionalEdge(Node_PG_Spawn, Node_PG_Route_1, isVerified: true);     // dist = 4.0m
            graph.AddBidirectionalEdge(Node_PG_Route_1, Node_PG_Route_2, isVerified: true);   // dist = 2.0m
            graph.AddBidirectionalEdge(Node_PG_Route_2, Node_PG_Route_3, isVerified: true);   // dist = 4.0m
            graph.AddBidirectionalEdge(Node_PG_Route_3, Node_PG_Spawn, isVerified: true);     // dist = 2.0m

            // 2. North exit route connection
            graph.AddBidirectionalEdge(Node_PG_Route_2, Node_Exit_Approach, isVerified: true); // dist = 3.0m

            // 3. Room 3 Machinery access
            graph.AddBidirectionalEdge(Node_PG_Route_3, Node_Door_Room3_Left, isVerified: true); // dist = 3.09m
            graph.AddBidirectionalEdge(Node_Door_Room3_Left, Node_Room3_Center, isVerified: true); // dist = 4.07m

            // 4. Room 3 alternate exit bypass (bypasses Barricade_ExitArea at Node 18 via Room 3)
            graph.AddBidirectionalEdge(Node_Room3_Center, Node_Exit_Approach, isVerified: true); // dist = 9.89m

            // 5. South access toward Room 2
            graph.AddBidirectionalEdge(Node_PG_Spawn, Node_Corridor_Junction, isVerified: true);  // dist = 6.0m
            graph.AddBidirectionalEdge(Node_Corridor_Junction, Node_Door_Room2_Top, isVerified: true); // dist = 4.47m

            return graph;
        }

        /// <summary>
        /// Creates the full facility prototype graph.
        /// Unverified nodes and connections across lower facility corridors are flagged accordingly.
        /// </summary>
        public static PatrolAStarGraph CreateFullFacilityGraph(bool includeUnverified = false)
        {
            var graph = CreateSecurityZoneGraph();
            if (!includeUnverified) return graph;

            // Lower facility nodes (Proposed / Pending full dynamic NavMesh validation in PlayMode)
            graph.AddNode(0, new Vector3(-12.5f, 0f, -26f), "Entrance_Corridor", isVerified: false);
            graph.AddNode(1, new Vector3(-13f, 0f, -17.5f), "Room1_South_Portal", isVerified: false);
            graph.AddNode(2, new Vector3(-13f, 0f, -11.75f), "Room1_Interior", isVerified: false);
            graph.AddNode(3, new Vector3(-8f, 0f, -11.75f), "Door_Room1_Right", isVerified: false);
            graph.AddNode(4, new Vector3(-13f, 0f, -6.5f), "Door_Room1_Top", isVerified: false);
            graph.AddNode(6, new Vector3(1f, 0f, -2f), "Door_Room2_Bottom", isVerified: false);
            graph.AddNode(7, new Vector3(1f, 0f, 4f), "Room2_Center", isVerified: false);
            graph.AddNode(8, new Vector3(-7f, 0f, 4f), "Door_Room2_Left", isVerified: false);
            graph.AddNode(9, new Vector3(9f, 0f, 4f), "Door_Room2_Right", isVerified: false);
            graph.AddNode(11, new Vector3(-11f, 0f, 14f), "West_Bypass_Corridor", isVerified: false);
            graph.AddNode(19, new Vector3(12.5f, 0f, 28f), "Main_Exit_Vestibule", isVerified: false);

            // Unverified candidate edges
            graph.AddBidirectionalEdge(0, 1, isVerified: false);
            graph.AddBidirectionalEdge(1, 2, isVerified: false);
            graph.AddBidirectionalEdge(2, 3, isVerified: false);
            graph.AddBidirectionalEdge(2, 4, isVerified: false);
            graph.AddBidirectionalEdge(4, 8, isVerified: false);
            graph.AddBidirectionalEdge(6, 7, isVerified: false);
            graph.AddBidirectionalEdge(7, 8, isVerified: false);
            graph.AddBidirectionalEdge(7, 9, isVerified: false);
            graph.AddBidirectionalEdge(7, Node_Door_Room2_Top, isVerified: false);
            graph.AddBidirectionalEdge(8, 11, isVerified: false);
            graph.AddBidirectionalEdge(11, Node_PG_Spawn, isVerified: false);
            graph.AddBidirectionalEdge(Node_Exit_Approach, 19, isVerified: false);

            return graph;
        }
    }
}

