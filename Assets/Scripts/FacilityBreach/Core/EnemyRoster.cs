using System;
using System.Collections.Generic;
using System.Linq;

namespace FacilityBreach.Core
{
    public enum EnemyLife { Active, TemporarilyInactive, AwaitingSafeSpawn, Permanent }
    public sealed class EnemyRecord
    {
        public string Identity { get; }
        public int Maximum { get; }
        public int Health { get; internal set; }
        public double ReviveAt { get; internal set; }
        public EnemyLife Life { get; internal set; }
        public EnemyRecord(string identity, int health) { Identity = identity; Maximum = health; Health = health; }
    }
    // Logical expiry happens before damage, even when physical revival must be postponed.
    public sealed class EnemyRoster
    {
        private readonly Dictionary<string, EnemyRecord> records = new Dictionary<string, EnemyRecord>();
        private readonly string[] required = { "PG-01", "SH-01", "CD-03", "FL-04" };
        public bool ChallengeComplete { get; private set; }
        public EnemyRecord Register(string identity, int health)
        {
            if (records.ContainsKey(identity) || health <= 0) throw new ArgumentException("Duplicate/invalid enemy registration");
            var record = new EnemyRecord(identity, health); records.Add(identity, record); return record;
        }
        public void Tick(double now)
        {
            foreach (var record in records.Values)
                if (record.Life == EnemyLife.TemporarilyInactive && now >= record.ReviveAt) record.Life = EnemyLife.AwaitingSafeSpawn;
        }
        public bool Damage(string identity, int amount, double now)
        {
            Tick(now);
            if (amount <= 0 || !records.TryGetValue(identity, out var victim) || victim.Life != EnemyLife.Active) return false;
            victim.Health = Math.Max(0, victim.Health - amount);
            if (victim.Health == 0) { victim.Life = EnemyLife.TemporarilyInactive; victim.ReviveAt = now + 30; }
            if (required.All(id => records.TryGetValue(id, out var r) && r.Life == EnemyLife.TemporarilyInactive))
            {
                ChallengeComplete = true;
                foreach (var record in records.Values) { record.Life = EnemyLife.Permanent; record.ReviveAt = 0; }
            }
            return true;
        }
        public bool Revive(EnemyRecord record, bool safe)
        {
            if (!safe || record.Life != EnemyLife.AwaitingSafeSpawn) return false;
            record.Health = record.Maximum; record.Life = EnemyLife.Active; record.ReviveAt = 0; return true;
        }
    }
}
