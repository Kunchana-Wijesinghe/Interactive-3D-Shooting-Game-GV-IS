using UnityEngine;

namespace Perera.SoundHunter
{
    public enum MoveStatus
    {
        Idle,     // no active request (initial, stopped/interrupted)
        Moving,   // request accepted, travelling
        Arrived,  // reached the goal within the stopping distance
        Failed    // rejected (no route) or abandoned (stuck/timeout); see FailureReason
    }

    /// <summary>
    /// MINIMAL ADAPTER CONTRACT between the Sound Hunter decision logic and whatever moves the
    /// body. Bandara's shared movement controller has not been published in this repository, so
    /// this is a Perera-side placeholder: Bandara's controller can implement it (or be wrapped by a
    /// thin adapter) without touching the decision code. The brain only issues abstract commands
    /// and polls status; it never steers, path-finds or applies physics itself.
    ///
    /// Rules the brain relies on:
    ///  - RequestMove is called at a bounded rate (never per frame) and returns false on immediate
    ///    failure (Status becomes Failed).
    ///  - A new RequestMove replaces any previous one (interruption); Stop() cancels (Status Idle).
    ///  - Status Arrived / Failed stays until the next RequestMove or Stop.
    ///  - Hearing propagation is NOT movement routing: the goal is a world position only.
    /// </summary>
    public interface IHunterMovement
    {
        MoveStatus Status { get; }
        string FailureReason { get; }
        bool RequestMove(Vector3 goal, float stoppingDistance);
        void Stop();
    }
}
