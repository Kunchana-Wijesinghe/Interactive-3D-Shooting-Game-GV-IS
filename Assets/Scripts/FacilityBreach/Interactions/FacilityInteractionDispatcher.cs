using System.Collections.Generic;
using FacilityBreach.Core;
using UnityEngine;

namespace FacilityBreach.Interactions
{
    public enum InteractionTargetKind
    {
        None,
        HeldBarricade,
        Station,
        Door,
        Barricade
    }

    public struct InteractionCandidate
    {
        public InteractionTargetKind Kind;
        public object Target;
        public float Distance;
        public float DotProduct;
        public bool HasDirectAim;
        public float Score;
        public string Prompt;

        public static readonly InteractionCandidate NoneCandidate = new InteractionCandidate
        {
            Kind = InteractionTargetKind.None,
            Target = null,
            Distance = float.PositiveInfinity,
            DotProduct = -1f,
            HasDirectAim = false,
            Score = float.PositiveInfinity,
            Prompt = ""
        };
    }

    /// <summary>
    /// Centralized interaction dispatcher for FacilityBreach.
    /// Safely arbitrates single E-key interactions between:
    /// 1. Held movable barricades (absolute release priority)
    /// 2. Pickups and ResourceStations (weapons, cards, ammo crates, health, devices)
    /// 3. Interactive doors (opening and closing with clearance checks)
    /// 4. Movable barricades (grab / release maneuver)
    /// Ensures exactly one interaction per key press without race conditions or input contention.
    /// </summary>
    public sealed class FacilityInteractionDispatcher : MonoBehaviour
    {
        [Header("Configuration")]
        public float stationMaxDistance = 2.5f;
        public float doorMaxDistance = 2.5f;
        public float barricadeMaxDistance = 3.0f;
        public float stationMinDot = 0.55f;
        public float doorMinDot = 0.35f;
        public float barricadeMinDot = 0.35f;
        public float directAimScoreBonus = 1.5f;

        private FacilityBreachRuntime runtime;
        private Camera playerCamera;
        private Transform playerTransform;

        private readonly List<ResourceStation> stations = new List<ResourceStation>();
        private readonly List<InteractiveDoor> doors = new List<InteractiveDoor>();
        private readonly List<MovableBarricade> barricades = new List<MovableBarricade>();

        public IReadOnlyList<ResourceStation> Stations => stations;
        public IReadOnlyList<InteractiveDoor> Doors => doors;
        public IReadOnlyList<MovableBarricade> Barricades => barricades;

        public void Initialize(FacilityBreachRuntime runtimeRef, Camera cam, Transform player)
        {
            runtime = runtimeRef;
            playerCamera = cam;
            playerTransform = player;
        }

        public void SetPlayerReferences(Camera cam, Transform player)
        {
            playerCamera = cam;
            playerTransform = player;
        }

        public void RegisterStation(ResourceStation s)
        {
            if (s != null && !stations.Contains(s)) stations.Add(s);
        }

        // A card-locked door stays shut until the player holds the card (it can always be closed again).
        private bool LockedByCard(InteractiveDoor d) => d.RequiresCard && !d.IsOpen && runtime != null && !runtime.Run.HasCard;
        public void RegisterDoor(InteractiveDoor d)
        {
            if (d != null && !doors.Contains(d)) doors.Add(d);
        }

        public void RegisterBarricade(MovableBarricade b)
        {
            if (b != null && !barricades.Contains(b)) barricades.Add(b);
        }

        public void UnregisterStation(ResourceStation s) => stations.Remove(s);
        public void UnregisterDoor(InteractiveDoor d) => doors.Remove(d);
        public void UnregisterBarricade(MovableBarricade b) => barricades.Remove(b);
        public void ClearAll()
        {
            stations.Clear();
            doors.Clear();
            barricades.Clear();
        }

        /// <summary>
        /// Returns any barricade currently held by the player.
        /// </summary>
        public MovableBarricade GetHeldBarricade()
        {
            if (playerTransform == null) return null;
            for (int i = 0; i < barricades.Count; i++)
            {
                var b = barricades[i];
                if (b != null && b.IsHeld && b.HoldingPlayer == playerTransform)
                    return b;
            }
            return null;
        }

        /// <summary>
        /// Contextual prompt for HUD display.
        /// </summary>
        public string GetContextualPrompt(ResourceStation legacyTarget = null)
        {
            var held = GetHeldBarricade();
            if (held != null) return "E: Release barricade";

            var best = FindBestCandidate(out _);
            if (best.Kind == InteractionTargetKind.Station && best.Target is ResourceStation st)
                return st.Prompt;
            if (best.Kind == InteractionTargetKind.Door && best.Target is InteractiveDoor dr)
                return LockedByCard(dr) ? "Access card required" : dr.IsOpen ? "E: Close door" : "E: Open door";
            if (best.Kind == InteractionTargetKind.Barricade)
                return "E: Grab barricade";

            return legacyTarget != null ? legacyTarget.Prompt : "";
        }

        /// <summary>
        /// Finds the single best interaction candidate across all categories.
        /// </summary>
        public InteractionCandidate FindBestCandidate(out string rejectionReason)
        {
            rejectionReason = "Aim at an interactable object within range";

            var held = GetHeldBarricade();
            if (held != null)
            {
                rejectionReason = "";
                return new InteractionCandidate
                {
                    Kind = InteractionTargetKind.HeldBarricade,
                    Target = held,
                    Distance = 0f,
                    DotProduct = 1f,
                    HasDirectAim = true,
                    Score = -100f,
                    Prompt = "E: Release barricade"
                };
            }

            Vector3 viewPos = playerCamera != null ? playerCamera.transform.position : (playerTransform != null ? playerTransform.position + Vector3.up * 1.6f : Vector3.zero);
            Vector3 viewFwd = playerCamera != null ? playerCamera.transform.forward : (playerTransform != null ? playerTransform.forward : Vector3.forward);
            Vector3 playerPos = playerTransform != null ? playerTransform.position : viewPos;

            // Direct aim hit check from camera view
            Collider directAimCollider = null;
            if (NearestHit(viewPos, viewFwd, Mathf.Max(barricadeMaxDistance, doorMaxDistance) + 0.5f, out var directHit))
            {
                directAimCollider = directHit.collider;
            }

            InteractionCandidate bestCandidate = InteractionCandidate.NoneCandidate;
            float bestScore = float.PositiveInfinity;
            string bestRejection = rejectionReason;

            // 1. Evaluate ResourceStations
            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                if (station == null || !station.CanOfferInteraction) continue;

                Vector3 targetPoint = station.InteractionPoint;
                Vector3 delta = targetPoint - viewPos;
                float distance = delta.magnitude;

                if (distance > stationMaxDistance) continue;
                Vector3 dir = delta.normalized;
                float dot = Vector3.Dot(viewFwd, dir);
                if (dot < stationMinDot) continue;

                if (NearestHit(viewPos, dir, distance + 0.03f, out var hit) && !hit.transform.IsChildOf(station.transform))
                {
                    bestRejection = "Blocked by " + hit.collider.name;
                    continue;
                }

                bool directAim = directAimCollider != null && directAimCollider.transform.IsChildOf(station.transform);
                float score = CalculateScore(distance, dot, directAim);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = new InteractionCandidate
                    {
                        Kind = InteractionTargetKind.Station,
                        Target = station,
                        Distance = distance,
                        DotProduct = dot,
                        HasDirectAim = directAim,
                        Score = score,
                        Prompt = station.Prompt
                    };
                }
            }

            // 2. Evaluate InteractiveDoors
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (door == null) continue;

                Bounds bounds = GetDoorBounds(door);
                Vector3 doorPoint = bounds.center;
                float distance = Vector3.Distance(viewPos, doorPoint);
                bool nearby = door.PlayerNearby;

                if (!nearby && distance > doorMaxDistance) continue;

                Vector3 dir = (doorPoint - viewPos).normalized;
                float dot = Vector3.Dot(viewFwd, dir);
                float minDot = nearby ? 0.20f : doorMinDot;
                if (dot < minDot) continue;

                if (NearestHit(viewPos, dir, distance + 0.05f, out var hit) && !hit.transform.IsChildOf(door.transform))
                {
                    bestRejection = "Blocked by " + hit.collider.name;
                    continue;
                }

                bool directAim = directAimCollider != null && directAimCollider.transform.IsChildOf(door.transform);
                float score = CalculateScore(distance, dot, directAim);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = new InteractionCandidate
                    {
                        Kind = InteractionTargetKind.Door,
                        Target = door,
                        Distance = distance,
                        DotProduct = dot,
                        HasDirectAim = directAim,
                        Score = score,
                        Prompt = LockedByCard(door) ? "Access card required" : door.IsOpen ? "E: Close door" : "E: Open door"
                    };
                }
            }

            // 3. Evaluate MovableBarricades
            for (int i = 0; i < barricades.Count; i++)
            {
                var barricade = barricades[i];
                if (barricade == null) continue;

                float distToPlayer = barricade.GetDistanceToPlayer(playerPos);
                if (distToPlayer > barricade.interactionDistance) continue;

                Collider col = barricade.GetComponent<Collider>();
                Vector3 targetPoint = col != null ? col.bounds.center : barricade.transform.position;
                Vector3 delta = targetPoint - viewPos;
                float distance = delta.magnitude;
                Vector3 dir = delta.normalized;
                float dot = Vector3.Dot(viewFwd, dir);
                if (dot < barricadeMinDot) continue;

                if (NearestHit(viewPos, dir, distance + 0.05f, out var hit) && !hit.transform.IsChildOf(barricade.transform))
                {
                    bestRejection = "Blocked by " + hit.collider.name;
                    continue;
                }

                bool directAim = directAimCollider != null && directAimCollider.transform.IsChildOf(barricade.transform);
                float score = CalculateScore(distToPlayer, dot, directAim);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = new InteractionCandidate
                    {
                        Kind = InteractionTargetKind.Barricade,
                        Target = barricade,
                        Distance = distToPlayer,
                        DotProduct = dot,
                        HasDirectAim = directAim,
                        Score = score,
                        Prompt = "E: Grab barricade"
                    };
                }
            }

            if (bestCandidate.Kind != InteractionTargetKind.None)
            {
                rejectionReason = "";
            }
            else
            {
                rejectionReason = bestRejection;
            }

            return bestCandidate;
        }

        /// <summary>
        /// Executes the single winning interaction.
        /// </summary>
        public bool ExecuteInteraction(out string rejection, out InteractionTargetKind executedKind, out object executedTarget)
        {
            executedKind = InteractionTargetKind.None;
            executedTarget = null;

            if (runtime != null && !runtime.AcceptsGameplay)
            {
                rejection = "Mission inactive, paused or player eliminated";
                return false;
            }

            // Priority 1: Held barricade exclusive release
            var held = GetHeldBarricade();
            if (held != null)
            {
                held.Release();
                rejection = "";
                executedKind = InteractionTargetKind.HeldBarricade;
                executedTarget = held;
                return true;
            }

            // Priority 2: Arbitrated candidate execution
            var candidate = FindBestCandidate(out rejection);
            if (candidate.Kind == InteractionTargetKind.None)
            {
                return false;
            }

            switch (candidate.Kind)
            {
                case InteractionTargetKind.Station:
                    if (candidate.Target is ResourceStation station && runtime != null)
                    {
                        bool used = station.Use(runtime.Run, runtime.Resources);
                        rejection = used ? "" : station.RejectionReason;
                        executedKind = InteractionTargetKind.Station;
                        executedTarget = station;
                        return used;
                    }
                    break;

                case InteractionTargetKind.Door:
                    if (candidate.Target is InteractiveDoor door)
                    {
                        if (LockedByCard(door)) { rejection = "Access card required"; executedKind = InteractionTargetKind.Door; executedTarget = door; return false; }
                        bool toggled = door.RequestOpen(!door.IsOpen);
                        rejection = toggled ? "" : "Door clearance obstructed by character or obstacle";
                        executedKind = InteractionTargetKind.Door;
                        executedTarget = door;
                        return toggled;
                    }
                    break;

                case InteractionTargetKind.Barricade:
                    if (candidate.Target is MovableBarricade barricade)
                    {
                        bool grabbed = barricade.Interact(playerTransform);
                        rejection = grabbed ? "" : "Barricade interaction cooldown or out of range";
                        executedKind = InteractionTargetKind.Barricade;
                        executedTarget = barricade;
                        return grabbed;
                    }
                    break;
            }

            return false;
        }

        public float CalculateScore(float distance, float dot, bool directAim)
        {
            return distance + 2.0f * (1.0f - dot) - (directAim ? directAimScoreBonus : 0f);
        }

        private Bounds GetDoorBounds(InteractiveDoor door)
        {
            Collider[] cols = door.GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return new Bounds(door.transform.position, Vector3.one);

            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++)
            {
                if (!cols[i].isTrigger) b.Encapsulate(cols[i].bounds);
            }
            return b;
        }

        private bool NearestHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            var hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (playerTransform != null && hit.collider.transform.IsChildOf(playerTransform)) continue;
                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }
    }
}
