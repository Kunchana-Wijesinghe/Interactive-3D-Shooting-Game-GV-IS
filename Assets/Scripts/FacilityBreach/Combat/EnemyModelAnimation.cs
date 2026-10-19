using FacilityBreach.AI.Roles;
using FacilityBreach.AI.SoundHunter;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FacilityBreach.Combat
{
    /// <summary>Visual-only playback of the team's embedded FBX clips. Never moves the combat root.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyModelAnimation : MonoBehaviour
    {
        private EnemyCombatAdapter combat;
        private SoundHunterNavMeshMover mover;
        private FacilityBreachRuntime runtime;
        private RoleDecisionBrain role;
        private FacilityBreachSoundHunterBrain hunter;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] players;
        private AnimationClip[] clips;
        private float shotTime;
        private readonly float[] clipTimes = new float[5];
        private static readonly string[] StateNames = { "Idle", "Move", "Search", "Attack", "Defeat" };
        private int state = -1, shots;
        public bool HasClips => graph.IsValid();
        public string AnimationState => state < 0 ? "Unavailable" : StateNames[state];

        private void Start()
        {
            combat = GetComponent<EnemyCombatAdapter>(); mover = GetComponent<SoundHunterNavMeshMover>();
            runtime = FindAnyObjectByType<FacilityBreachRuntime>(); role = GetComponent<RoleDecisionBrain>(); hunter = GetComponent<FacilityBreachSoundHunterBrain>();
            var library = UnityEngine.Resources.Load<EnemyAnimationLibrary>("EnemyAnimationLibrary");
            var set = library ? library.Find(combat.Identity) : null;
            var wrapper = transform.Find("ModelVisual");
            var visual = wrapper ? wrapper.Find("ModelVisual") : null;
            if (set == null || !visual || !set.Idle || !set.Move || !set.Search || !set.Attack || !set.Defeat)
            { Debug.LogWarning("GV animation clips unavailable for " + combat.Identity + "; using existing pose presentation.", this); return; }
            var animator = visual.GetComponent<Animator>(); if (!animator) animator = visual.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create(combat.Identity + " GV presentation"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 5);
            clips = new[] { set.Idle, set.Move, set.Search, set.Attack, set.Defeat }; players = new AnimationClipPlayable[5];
            for (int i = 0; i < 5; i++)
            {
                players[i] = AnimationClipPlayable.Create(graph, clips[i]); players[i].SetSpeed(0);
                players[i].SetApplyFootIK(false); graph.Connect(players[i], 0, mixer, i);
            }
            mixer.SetInputWeight(0, 1);
            var output = AnimationPlayableOutput.Create(graph, "Robot", animator); output.SetSourcePlayable(mixer); graph.Play();
        }
        private void LateUpdate()
        {
            if (!HasClips || !runtime || !runtime.AcceptsGameplay) return;
            if (combat.ResolvedShots != shots) { shots = combat.ResolvedShots; shotTime = .25f; }
            shotTime = Mathf.Max(0, shotTime - Time.deltaTime);
            bool searching = (role && role.State == RoleState.Search) || (hunter && hunter.CurrentState == SoundHunterState.Search);
            int next = combat.IsDead ? 4 : combat.Warning || shotTime > 0 ? 3 : mover && mover.Velocity.sqrMagnitude > .025f ? 1 : searching ? 2 : 0;
            if (state != next) { state = next; clipTimes[state] = 0; }
            float blend = 1 - Mathf.Exp(-12 * Time.deltaTime);
            for (int i = 0; i < 5; i++)
            {
                mixer.SetInputWeight(i, Mathf.Lerp(mixer.GetInputWeight(i), i == state ? 1 : 0, blend));
                clipTimes[i] += Time.deltaTime;
                float time = i == 4 ? Mathf.Min(clipTimes[i], clips[i].length) : clipTimes[i] % clips[i].length;
                players[i].SetTime(time);
            }
            graph.Evaluate(0);
        }
        private void OnDisable() { if (graph.IsValid()) graph.Stop(); }
        private void OnEnable() { if (graph.IsValid()) graph.Play(); }
        private void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
