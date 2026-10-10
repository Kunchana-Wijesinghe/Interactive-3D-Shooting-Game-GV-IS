# Read-only Sound Hunter integration audit

Independent preparation for Phase 1; no IS implementation is included in the
foundation PR. Assets/Perera remains protected.

The protected HearingGraph already implements explicit Dijkstra using a
deterministic SortedSet. HearingEvaluator supports explicit source/listener
nodes. SceneHearingGraph requires authored containing zones, validates finite
positions and rejects unmapped sounds. SoundMemory supports bounded storage,
expiry pruning, same-source/category refresh and revision-safe Forget. SoundRanker
prioritizes gunshot over thrown sounds, remaining budget, time, source and position.
These pure utility classes can be referenced directly without copying them.

The protected MonoBehaviour brain cannot be activated unchanged in Prototype:
it requires Perera.Health for both lifecycle and target vision, includes its own
direct 6-damage attack, and uses permanent death. Those conflict with the shared
foundation's healing/resource/lifecycle and 10-damage warning/reload contract.
The protected HunterVision also requires a Perera.Health target; adding a second
player Health merely to satisfy it would create conflicting health systems.

Recommended narrow adapter work in the exact IS branch:

- Reuse the protected graph/ranker/memory/evaluator as library code; do not enable
  its combat brain or test mover. New decision code lives in a distinct namespace.
- Author non-overlapping room/corridor/portal volumes using actual wall bounds.
  Use explicit-node hearing, never HearingGraph.NearestNode for scene mapping.
- Door links keep a finite open cost and higher closed attenuation. Rebuild or
  refresh graph costs only on effective door state changes; invalidate evaluators
  when graph instances change. Do not use a closed link as infinite attenuation
  unless specifically authored as acoustically sealed.
- Reject non-finite source position/time/budget before evaluation. Filter to
  gunshots and can impacts. Use runtime GameplayTime for memory/search timers.
- Configure ten-second memory expiry, bounded capacity, four-second search and
  all-round 16-unit sight. Perception returns position only on range/LOS success.
- Decision state issues IHunterMovement-compatible requests at bounded intervals.
  A new Unity NavMesh adapter is needed because the published one is test-only
  and Bandara's movement controller is absent. Add a separate navigation root;
  do not run FacilityNavMesh.SetupNavMesh, which removes existing components.
- Ask EnemyCombatAdapter.TryAttack only after successful visual perception.
  Implement IEnemyActivitySink to stop hearing, decisions and movement immediately,
  and clear memory/routes/last-seen information on revival.
- Separate robot-authorized door traversal from player-card permission. A robot
  may open a door; a card barrier must still prevent an unauthorized player.
  Verify every Machinery portal rather than only its bottom doorway.
- Check graph audibility around all walls, expired investigations before arrival,
  refresh revisions, dynamic barricade path failures and door attenuation changes.

Do not create/push the IS branch until the foundation is actually merged into
develop. This audit records reuse boundaries; it does not claim an integrated SH.
