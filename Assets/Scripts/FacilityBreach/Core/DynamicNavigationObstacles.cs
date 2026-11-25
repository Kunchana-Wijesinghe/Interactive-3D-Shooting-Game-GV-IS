using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach
{
    // Runtime proxies keep navigation in sync with authored walls and pushable barricades.
    public sealed class DynamicNavigationObstacles : MonoBehaviour
    {
        private readonly List<(Collider source, NavMeshObstacle obstacle)> followers = new List<(Collider, NavMeshObstacle)>();

        public static void CarveStaticWall(BoxCollider wall, Transform runtime)
        {
            if (wall.GetComponent<NavMeshObstacle>()) return;

            // Scene walls can move between bakes. Use their current collider shape in world space.
            var proxy = new GameObject("FB_NavWall_" + wall.name);
            proxy.transform.SetPositionAndRotation(wall.transform.TransformPoint(wall.center), wall.transform.rotation);
            var scale = wall.transform.lossyScale;
            var obstacle = proxy.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.Scale(wall.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            proxy.transform.SetParent(runtime, true);
        }
        private void Start()
        {
            foreach (var body in FindObjectsByType<Rigidbody>())
            {
                if (!body.name.StartsWith("Barricade")) continue;
                var source = body.GetComponentInChildren<Collider>(); if (!source) continue;
                var proxy = new GameObject("FB_NavObstacle_" + body.name); proxy.transform.SetParent(transform);
                var obstacle = proxy.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.carving = true; obstacle.carveOnlyStationary = false; followers.Add((source, obstacle));
            }
        }
        private void LateUpdate()
        {
            foreach (var pair in followers)
            {
                if (!pair.source) continue;
                pair.obstacle.transform.position = pair.source.bounds.center;
                pair.obstacle.size = pair.source.bounds.size;
            }
        }
    }
}
