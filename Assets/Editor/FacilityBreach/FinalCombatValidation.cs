using System.IO;
using FacilityBreach.Combat;
using FacilityBreach.Core;
using UnityEditor;
using UnityEngine;

namespace FacilityBreach.Editor
{
    public static class FinalCombatValidation
    {
        [MenuItem("Facility Breach/Validate Final Combat Phase 4")]
        public static void ValidateAll()
        {
            int passed = 0, total = 0;
            void Assert(bool condition, string message)
            {
                total++;
                if (!condition) Debug.LogError("[FAIL] " + message);
                else passed++;
            }

            // 1. Protected folder check
            Assert(Directory.Exists("Assets/Perera"), "Protected Assets/Perera folder exists");

            // 2. Enemy Roster & Four-Agent Roster Test
            var roster = new EnemyRoster();
            var pg = roster.Register("PG-01", 60);
            var sh = roster.Register("SH-01", 80);
            var cd = roster.Register("CD-03", 90);
            var fl = roster.Register("FL-04", 70);

            Assert(pg.Health == 60 && sh.Health == 80 && cd.Health == 90 && fl.Health == 70, "Four agent capacities registered correctly");
            Assert(!roster.ChallengeComplete, "Challenge initially incomplete");

            // Damage all 4 to 0
            roster.Damage("PG-01", 60, 1.0);
            roster.Damage("SH-01", 80, 1.0);
            roster.Damage("CD-03", 90, 1.0);
            roster.Damage("FL-04", 70, 1.0);

            Assert(roster.ChallengeComplete, "Permanent defeat challenge unlocked when all 4 agents are inactive");

            // 3. Player Ammo & Combat Resources Test
            var res = new PlayerResources();
            Assert(res.Magazine == 50 && res.Reserve == 50, "Initial Ammo1gun magazine 50 and reserve 50");

            res.Shoot();
            Assert(res.Magazine == 49, "Shot consumed 1 round");

            res.Reload();
            Assert(res.Magazine == 50 && res.Reserve == 49, "Reload refilled magazine from reserve");

            // 4. Mission Flow Victory Scenario Test
            var mission = new MissionRun();
            mission.Start();
            Assert(mission.State == MissionState.Active && mission.Remaining == 300, "Mission starts at 05:00 countdown");

            mission.CollectCard();
            Assert(mission.HasCard, "Access card collected");

            mission.Connect(true);
            Assert(mission.State == MissionState.Deactivating, "Card connected -> Deactivating state");

            mission.Tick(5.1);
            Assert(mission.State == MissionState.Escape && mission.ExitUnlocked, "5-second shutdown complete -> Escape state & Exit unlocked");

            mission.Exit(true);
            Assert(mission.State == MissionState.Won, "Reached exit alive -> Victory Won state");

            // 5. Mission Flow Timeout Defeat Scenario Test
            var failMission = new MissionRun();
            failMission.Start();
            failMission.Tick(300.1);
            Assert(failMission.State == MissionState.Lost, "Countdown timeout -> Defeat Lost state");

            Debug.Log($"[Facility Breach Phase 4] Final Combat Validation Complete. {passed}/{total} checks passed.");
        }
    }
}
