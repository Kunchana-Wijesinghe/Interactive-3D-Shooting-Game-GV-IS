using UnityEngine;
using UnityEditor;

public class DoorTester : EditorWindow
{
    [MenuItem("Facility Breach/Door Testing/Open All Doors")]
    public static void OpenAllDoors()
    {
        InteractiveDoor[] doors =
            Object.FindObjectsByType<InteractiveDoor>(
                FindObjectsSortMode.None
            );

        foreach (InteractiveDoor door in doors)
        {
            Undo.RecordObject(
                door.transform,
                "Open Facility Door"
            );

            door.transform.localRotation =
                Quaternion.Euler(0f, 90f, 0f);
        }

        SceneView.RepaintAll();

        Debug.Log(
            doors.Length + " doors opened for testing."
        );
    }

    [MenuItem("Facility Breach/Door Testing/Close All Doors")]
    public static void CloseAllDoors()
    {
        InteractiveDoor[] doors =
            Object.FindObjectsByType<InteractiveDoor>(
                FindObjectsSortMode.None
            );

        foreach (InteractiveDoor door in doors)
        {
            Undo.RecordObject(
                door.transform,
                "Close Facility Door"
            );

            door.transform.localRotation =
                Quaternion.identity;
        }

        SceneView.RepaintAll();

        Debug.Log(
            doors.Length + " doors closed."
        );
    }
}