using Godot;
using System.Collections.Generic;

namespace PatientZero
{
    /// <summary>
    /// Headless logic verification — run with:
    /// Godot --headless --path . --script res://tests/SmokeTest.cs
    /// </summary>
    public partial class SmokeTest : SceneTree
    {
        public override void _Initialize()
        {
            var failures = new List<string>();

            // 1. Local brain: a camping, ranged-only player must get flanked + tanky pressure
            var camper = new BehaviorSummary
            {
                WaveNumber = 3,
                TimeStationaryPct = 0.8f,
                TimeMovingPct = 0.2f,
                KillsRangedPct = 0.95f,
                KillsMeleePct = 0.05f,
                AvgEngagementDistance = 15f,
                AvgDistanceToCover = 2f,
                WaveClearTimeSeconds = 30f,
                PlayerHpRemainingPct = 0.6f,
                PlayerFavoredZone = "north_chokepoint",
            };
            var d = PatientZeroBrain.DecideLocal(camper, null, 4);
            if (d.Zone != ZoneName.NorthChokepoint) failures.Add("camp zone bias");
            if (d.Composition.Tanky < 3) failures.Add("anti-range tanky count");
            int total = d.Composition.Standard + d.Composition.Fast + d.Composition.Tanky;
            if (total < 3 || total > 30) failures.Add("composition rails");

            // 2. Safety rails clamp garbage
            var wild = new AIDecision { Composition = new WaveComp { Standard = 99, Fast = -5, Tanky = 99 }, Zone = ZoneName.Balanced, Taunt = new string('x', 500) };
            PatientZeroBrain.Clamp(wild);
            int wt = wild.Composition.Standard + wild.Composition.Fast + wild.Composition.Tanky;
            if (wt > 30) failures.Add("clamp total");
            if (wild.Taunt.Length > 110) failures.Add("clamp taunt");

            // 3. Behavior logger compiles a sane summary
            var logger = new BehaviorLogger();
            logger.Reset();
            for (int i = 0; i < 100; i++) logger.TrackFrame(0.016f, new Vector2(0, -10), Vector2.Zero);
            logger.TrackKill("ranged", 12f);
            logger.TrackKill("melee", 1.5f);
            var sum = logger.Compile(1, 0.8f, ThemeBucket.Temperate);
            if (sum.TimeStationaryPct < 0.99f) failures.Add("logger stationary");
            if (sum.KillsRangedPct != 0.5f) failures.Add("logger kill ratio");
            if (sum.PlayerFavoredZone != "north_chokepoint") failures.Add("logger favored zone");

            // 4. Autopsy (local path) classifies archetype
            var (report, key) = PatientZeroBrain.Autopsy(new List<BehaviorSummary> { camper }, new SpecimenProfile(), 3, 20, 500).Result;
            if (key != "camper") failures.Add("archetype classify");
            if (report.ClosingRemark.Length == 0) failures.Add("autopsy remark");

            // 5. Memory roundtrip
            var p = new SpecimenProfile { SpecimenNumber = 42 };
            p.RecordRun(new RunRecord { WavesSurvived = 3, Score = 500, Kills = 20, Archetype = "camper", CauseOfDeath = "test" },
                new List<BehaviorSummary> { camper });
            if (p.RunCount != 1 || p.BestWave != 3) failures.Add("memory record");

            // 6. Share text format
            var share = PatientZeroBrain.BuildShareText(p, 3, 20, 500, report);
            if (!share.Contains("AUTOPSY REPORT") || !share.Contains("042")) failures.Add("share text");

            if (failures.Count == 0)
            {
                GD.Print("SMOKE: PASS (6/6 checks)");
                Quit(0);
            }
            else
            {
                GD.PrintErr("SMOKE: FAIL — " + string.Join(", ", failures));
                Quit(1);
            }
        }
    }
}
