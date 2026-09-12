using System.Collections.Generic;

namespace PatientZero
{
    /// <summary>
    /// Grounded narrative system — every line is tied to the research paper's story:
    /// the player is SUBJECT #1, Patient Zero is an adversarial AI director that
    /// observes → models → counters → observes again. Waves 1-3 are cold-start
    /// observation, wave 4+ is full counter-play, every 5th wave opens a GATE.
    /// "The game engine decides. The model explains." (paper §2.4)
    /// </summary>
    public static class Story
    {
        // ---- Strain families (paper §9: location-seeded outbreak generation) ----
        public static string StrainName(ThemeBucket b) => b switch
        {
            ThemeBucket.Coastal => "DROWNED STRAIN",
            ThemeBucket.Desert => "SCORCHED STRAIN",
            ThemeBucket.Jungle => "BLOOMED STRAIN",
            ThemeBucket.Mountain => "RIMEBOUND STRAIN",
            ThemeBucket.Temperate => "ASHEN STRAIN",
            _ => "WILDCARD STRAIN",
        };

        // ---- Cold-start observation arc (paper §10) ----
        public static string[] IntroLines(ThemeBucket bucket) => new[]
        {
            "TRANSMISSION RECEIVED.",
            $"OUTBREAK CLASSIFIED: {StrainName(bucket)}.",
            "You are SUBJECT #1 of the exposure protocol.",
            "I am PATIENT ZERO. I do not assist. I do not balance. I STUDY.",
            "Every move you make becomes a measurement. Every habit becomes a weapon — mine.",
            "Observe. Model. Counter. Observe again.",
            "The trial begins now. Survive as long as your habits allow.",
        };

        // ---- Per-wave grounded dialogue (paper §7: Facts → Narrative) ----
        public static string WaveBeat(int wave, string archetype, ThemeBucket bucket)
        {
            // GATE waves — every 5th (paper §3.1)
            if (wave % 5 == 0)
                return Pick(new[]
                {
                    $"GATE {wave / 5} OPENING. You have been weighed, Subject. This is what I built from YOUR habits.",
                    $"GATE {wave / 5}. Behind it stands everything your behavior taught me. Meet my thesis.",
                    $"A gate opens. Not to reward you — to TEST what I learned from you.",
                });

            // Cold start — waves 1-3 (paper §10: weak behavioral inference)
            if (wave == 1)
                return "Wave one: neutral composition. A baseline must be established before it can be broken.";
            if (wave == 2)
                return "Telemetry compiling. Movement entropy, weapon preference, positional concentration — all of it is already yours.";
            if (wave == 3)
                return "Inference strengthening. Your archetype is forming. I do hope you surprise me.";

            // Full adversarial adaptation — wave 4+ (paper §10)
            if (wave == 4)
                return "Baseline established. Counter-selection engaged. Henceforth, every wave is an argument against your strategy.";

            return archetype switch
            {
                "camper" => Pick(new[]
                {
                    $"Positional concentration: {Pct()} of the wave in one cell. I have started naming that wall after you.",
                    "Corner seven again. Flankers have your address. Relocate or be catalogued.",
                    "Static cover is not safety. It is a data point. Exploiting.",
                }),
                "sniper" => Pick(new[]
                {
                    "Ranged dependence documented. Platebacks inbound — your bullets will need a hobby.",
                    "Distance is a preference. Preferences get countered. Closing the gap.",
                    "Exclusively ranged engagement. Deploying armor that does not care about your range.",
                }),
                "brawler" => Pick(new[]
                {
                    "Contact enthusiasm logged. Spitters deployed — approach has a price now.",
                    "Close-quarters preference documented. The space between us is about to get hostile.",
                }),
                "runner" => Pick(new[]
                {
                    "Movement entropy: high. Repetition found anyway. Trappers placed along your loop.",
                    "You run beautifully. I stopped chasing. I started PREDICTING.",
                }),
                _ => Pick(new[]
                {
                    "Observed. Catalogued. Countering.",
                    "Your strategy is a hypothesis. This wave is its refutation.",
                    $"Behavioral delta within tolerance. {StrainName(bucket)} adjusting.",
                    "I do not raise difficulty. I remove your certainties, one wave at a time.",
                }),
            };
        }

        // ---- Intermission telemetry remark (grounded, paper-style) ----
        public static string TelemetryRemark(BehaviorSummary s)
        {
            var lines = new List<string>
            {
                $"Clear time {s.WaveClearTimeSeconds:0.0}s · stationary {(int)(s.TimeStationaryPct * 100)}% · ranged {(int)(s.KillsRangedPct * 100)}%.",
            };
            if (s.TimeStationaryPct > 0.55f) lines.Add("A favored position is a confession.");
            else if (s.KillsRangedPct > 0.8f) lines.Add("You never closed the distance. Noted.");
            else if (s.KillsMeleePct > 0.5f) lines.Add("You LIKE being close. Filed under: exploitable.");
            else lines.Add("Adequate diversity. Escalating anyway.");
            return string.Join(" ", lines);
        }

        // ---- Greeting (paper framing: player as subject) ----
        public static string SubjectGreeting(int specimenNumber, int runCount)
        {
            if (runCount == 0)
                return "New specimen acquired. Baseline behavior will be established. Proceed.";
            return Pick(new[]
            {
                $"Specimen #{specimenNumber:000} returns. Memory intact. Your previous failure is already factored in.",
                $"Subject #{specimenNumber:000}, resuming trial. I kept your file open.",
            });
        }

        // ---- Death/autopsy framing (paper: autopsy report) ----
        public static string[] AutopsyFrame() => new[]
        {
            "SUBJECT TERMINATED. ARCHIVING BEHAVIORAL FILE.",
            "Your habits outlived you. They are already in the next wave.",
            "Cause of death: your own strategy, weaponized.",
        };

        private static readonly System.Random Rng = new();
        private static string Pick(string[] pool) => pool[Rng.Next(pool.Length)];
        private static string Pct() => $"{55 + Rng.Next(35)}%";
    }
}
