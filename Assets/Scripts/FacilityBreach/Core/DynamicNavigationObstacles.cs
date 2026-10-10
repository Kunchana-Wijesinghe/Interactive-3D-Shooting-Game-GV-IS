using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FacilityBreach
{
    // New gameplay children follow authored pushable barricades; no authored collider is edited.
    public sealed class DynamicNavigationObstacles : MonoBehaviour
    {
        private readonly List<(Collider source, NavMeshObstacle obstacle)> followers = new List<(Collider, NavMeshObstacle)>();
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
