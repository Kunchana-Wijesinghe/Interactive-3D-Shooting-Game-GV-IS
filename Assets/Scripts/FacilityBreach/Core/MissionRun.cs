using System;

namespace FacilityBreach.Core
{
    public enum MissionState { Menu, Active, Deactivating, Escape, Won, Lost }

    // Pure simulation: resolve events at their times within a step, timeout wins ties.
    public sealed class MissionRun
    {
        public MissionState State { get; private set; } = MissionState.Menu;
        public double Remaining { get; private set; } = 300;
        public double ShutdownRemaining { get; private set; } = 5;
        public double Elapsed { get; private set; }
        public bool HasCard { get; private set; }
        public bool Paused { get; set; }
        public string LossReason { get; private set; }
        public bool Running => State == MissionState.Active || State == MissionState.Deactivating || State == MissionState.Escape;
        public bool ExitUnlocked => State == MissionState.Escape || State == MissionState.Won;
        public void Start() { if (State == MissionState.Menu) State = MissionState.Active; }
        public bool CollectCard() { if (!Running || Paused || HasCard) return false; HasCard = true; return true; }
        public bool Connect(bool inRange)
        {
            if (State != MissionState.Active || Paused || !HasCard || !inRange) return false;
            State = MissionState.Deactivating; return true;
        }
        public void Tick(double dt)
        {
            if (!Running || Paused || double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0) return;
            if (State == MissionState.Escape) { Elapsed += dt; return; }
            double eventTime = State == MissionState.Deactivating ? Math.Min(Remaining, ShutdownRemaining) : Remaining;
            double advance = Math.Min(dt, eventTime);
            Elapsed += advance; Remaining = Math.Max(0, Remaining - advance);
            if (State == MissionState.Deactivating) ShutdownRemaining = Math.Max(0, ShutdownRemaining - advance);
            if (Remaining <= 0) { Lose("Time expired"); return; }
            if (State == MissionState.Deactivating && ShutdownRemaining <= 0)
            { State = MissionState.Escape; Elapsed += dt - advance; }
        }
        public void Lose(string reason) { if (!Running) return; State = MissionState.Lost; LossReason = reason; }
        public bool Exit(bool alive) { if (State != MissionState.Escape || Paused || !alive) return false; State = MissionState.Won; return true; }
    }
}
