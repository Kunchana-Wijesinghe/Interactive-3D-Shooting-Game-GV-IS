using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;
using UnityEngine.AI;

public class FacilityNavMesh : EditorWindow
{
    [MenuItem("Facility Breach/Setup NavMesh")]
    public static void SetupNavMesh()
    {
        GameObject facility = GameObject.Find("Generated_Facility");

        if (facility == null)
        {
            Debug.LogError("Generated_Facility not found.");
            return;
        }

        // Remove old NavMesh setup if it exists
        NavMeshSurface oldSurface =
            facility.GetComponent<NavMeshSurface>();

        if (oldSurface != null)
            DestroyImmediate(oldSurface);

        // Add NavMesh Surface to facility
        NavMeshSurface surface =
            facility.AddComponent<NavMeshSurface>();

        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // Build navigation mesh
        surface.BuildNavMesh();

        EditorUtility.SetDirty(facility);

        Debug.Log("Facility NavMesh created successfully.");
    }
}