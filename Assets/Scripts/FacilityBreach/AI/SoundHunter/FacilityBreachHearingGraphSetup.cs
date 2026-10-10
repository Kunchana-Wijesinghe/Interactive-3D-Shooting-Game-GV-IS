using System.Collections.Generic;
using Perera.SoundHunter;
using UnityEngine;

namespace FacilityBreach.AI.SoundHunter
{
    public sealed class FacilityBreachHearingGraphSetup : MonoBehaviour
    {
        public HearingGraph Graph { get; private set; }
        private readonly Dictionary<string, int> nodeIndices = new Dictionary<string, int>();
        private readonly List<(int a, int b, InteractiveDoor door, float openCost, float closedCost)> doorLinks
            = new List<(int, int, InteractiveDoor, float, float)>();

        public void BuildGraph()
        {
            Graph = new HearingGraph();
            nodeIndices.Clear();
            doorLinks.Clear();

            // Room / Zone nodes (positions matching Prototype layout)
            AddNode("Entrance", new Vector3(0, 0, -20));
            AddNode("CorridorSouth", new Vector3(0, 0, -10));
            AddNode("StorageRoom", new Vector3(-12, 0, -14));
            AddNode("CentralJunction", new Vector3(0, 0, 0));
            AddNode("ToxicWasteArea", new Vector3(0, 0, 18));
            AddNode("MachineryLeft", new Vector3(-10, 0, 18));
            AddNode("MachineryBottom", new Vector3(0, 0, 12));
            AddNode("MachineryTop", new Vector3(0, 0, 24));
            AddNode("MachineryCore", new Vector3(10, 0, 18));
            AddNode("CorridorEast", new Vector3(15, 0, -10));
            AddNode("ExitArea", new Vector3(18, 0, -18));

            // Open corridor links
            AddOpenLink("Entrance", "CorridorSouth", 2.0f);
            AddOpenLink("CorridorSouth", "CentralJunction", 2.0f);
            AddOpenLink("CentralJunction", "ToxicWasteArea", 3.0f);
            AddOpenLink("ToxicWasteArea", "MachineryLeft", 2.0f);
            AddOpenLink("ToxicWasteArea", "MachineryBottom", 2.0f);
            AddOpenLink("ToxicWasteArea", "MachineryTop", 2.0f);
            AddOpenLink("MachineryLeft", "MachineryCore", 2.0f);
            AddOpenLink("MachineryBottom", "MachineryCore", 2.0f);
            AddOpenLink("MachineryTop", "MachineryCore", 2.0f);
            AddOpenLink("CentralJunction", "CorridorEast", 3.0f);
            AddOpenLink("CorridorEast", "ExitArea", 2.0f);

            // Door links (open = 1.0, closed = 15.0 attenuation)
            var doors = FindObjectsByType<InteractiveDoor>();
            foreach (var door in doors)
            {
                if (door.name.Contains("Room1") || door.name.Contains("Storage"))
                {
                    AddDoorLink("CorridorSouth", "StorageRoom", door, 1.0f, 15.0f);
                }
                else if (door.name.Contains("Exit") || door.name.Contains("CorridorEast"))
                {
                    AddDoorLink("CorridorEast", "ExitArea", door, 1.0f, 15.0f);
                }
            }
        }

        private void AddNode(string name, Vector3 pos)
        {
            int idx = Graph.AddNode(pos);
            nodeIndices[name] = idx;
        }

        private void AddOpenLink(string nameA, string nameB, float cost)
        {
            if (nodeIndices.TryGetValue(nameA, out int a) && nodeIndices.TryGetValue(nameB, out int b))
            {
                Graph.AddEdge(a, b, cost);
            }
        }

        private void AddDoorLink(string nameA, string nameB, InteractiveDoor door, float openCost, float closedCost)
        {
            if (nodeIndices.TryGetValue(nameA, out int a) && nodeIndices.TryGetValue(nameB, out int b))
            {
                float cost = door && door.IsOpen ? openCost : closedCost;
                Graph.AddEdge(a, b, cost);
                doorLinks.Add((a, b, door, openCost, closedCost));
            }
        }

        public int GetNearestNode(Vector3 position)
        {
            if (Graph == null || Graph.NodeCount == 0) BuildGraph();
            return Graph.NearestNode(position);
        }

        public void UpdateDoorAttenuation()
        {
            if (Graph == null) BuildGraph();
            // Rebuild graph to reflect updated door costs dynamically
            BuildGraph();
        }
    }
}
