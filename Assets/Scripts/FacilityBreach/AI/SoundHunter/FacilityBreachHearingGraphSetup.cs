using System.Collections.Generic;
using Perera.SoundHunter;
using UnityEngine;
namespace FacilityBreach.AI.SoundHunter
{
    // Acoustic cells have explicit containing volumes and physically validated portal links.
    // Membership is never selected by nearest distance across a wall.
    public sealed class FacilityBreachHearingGraphSetup : MonoBehaviour
    {
        public HearingGraph Graph { get; private set; }
        private readonly Dictionary<Vector2Int,int> cells = new Dictionary<Vector2Int,int>();
        private Vector3 minimum;
        private int doorRevision;
        private float nextCheck;
        private const float CellSize = 1;
        public void BuildGraph()
        {
            Graph = new HearingGraph(); cells.Clear();
            var floor = GameObject.Find("Facility_Floor");
            if (!floor || !floor.TryGetComponent<Collider>(out var collider)) return;
            Bounds bounds=collider.bounds; minimum=bounds.min;
            int width=Mathf.CeilToInt(bounds.size.x),depth=Mathf.CeilToInt(bounds.size.z);
            for(int x=0;x<width;x++) for(int z=0;z<depth;z++)
            {
                Vector3 center=Center(new Vector2Int(x,z));
                bool occupied=false;
                foreach(var hit in Physics.OverlapBox(center, new Vector3(.08f,.1f,.08f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                    if (!Ignore(hit) && !hit.GetComponentInParent<InteractiveDoor>()) { occupied=true;break; }
                if (!occupied) cells[new Vector2Int(x,z)]=Graph.AddNode(center);
            }
            foreach(var pair in cells)
                foreach(var step in new[]{Vector2Int.right,Vector2Int.up})
                    if(cells.TryGetValue(pair.Key+step,out int other))
                    {
                        Vector3 origin=Center(pair.Key),delta=Center(pair.Key+step)-origin;
                        float cost=CellSize; bool blocked=false;
                        foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                        {
                            if(Ignore(hit.collider)) continue;
                            var door=hit.collider.GetComponentInParent<InteractiveDoor>();
                            if(door) cost+=door.IsOpen?1:15;
                            else {blocked=true;break;}
                        }
                        if(!blocked) Graph.AddEdge(pair.Value,other,cost);
                    }
            doorRevision=DoorRevision();
            GameplayDiagnostics.Trace($"hearing graph ready nodes={Graph.NodeCount} doorRevision={doorRevision}",this);
        }
        private Vector3 Center(Vector2Int key)=>new Vector3(minimum.x+(key.x+.5f)*CellSize,1.2f,minimum.z+(key.y+.5f)*CellSize);
        private bool Ignore(Collider collider)=>collider.GetComponentInParent<FacilityBreach.Combat.EnemyCombatAdapter>() ||
            collider.GetComponentInParent<FacilityBreach.Interactions.ResourceStation>() || collider.GetComponentInParent<FacilityBreach.Player.TemporaryPlayerAdapter>() || collider.name.StartsWith("Card required");
        public bool TryMapPoint(Vector3 position,out int node)
        {
            node=-1;
            if(!float.IsFinite(position.x)||!float.IsFinite(position.y)||!float.IsFinite(position.z)||Graph==null||position.y<-.5f||position.y>4) return false;
            var key=new Vector2Int(Mathf.FloorToInt((position.x-minimum.x)/CellSize),Mathf.FloorToInt((position.z-minimum.z)/CellSize));
            if(!cells.TryGetValue(key,out node))return false;
            Vector3 origin=new Vector3(position.x,1.2f,position.z),delta=Center(key)-origin;
            foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!Ignore(hit.collider)&&!hit.collider.GetComponentInParent<InteractiveDoor>()){node=-1;return false;}
            return true;
        }
        public int GetNearestNode(Vector3 position)=>TryMapPoint(position,out int node)?node:-1;
        private int DoorRevision()
        { int hash=17;foreach(var door in FindObjectsByType<InteractiveDoor>())hash=unchecked(hash*31+(door.IsOpen?1:0));return hash; }
        public void UpdateDoorAttenuation(){if(Graph==null||DoorRevision()!=doorRevision)BuildGraph();}
        private void Update(){if(Time.time<nextCheck)return;nextCheck=Time.time+.5f;UpdateDoorAttenuation();}
    }
}
