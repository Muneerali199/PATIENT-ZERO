using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PatientZero
{
    public class AIDecision
    {
        public WaveComp Composition;
        public ZoneName Zone = ZoneName.Balanced;
        public string Taunt = "Observed. Catalogued.";
        public string Reasoning = "";
        public string Source = "local";
    }

    public class AutopsyReport
    {
        public string Archetype = "The Unknown";
        public string CauseOfDeath = "";
        public float AdaptabilityIndex = 0.5f;
        public string Weakness = "";
        public string ClosingRemark = "";
        public string Source = "local";
    }

    /// <summary>
    /// Patient Zero's brain. Local deterministic rules are the ground truth
    /// (zero latency, demo-proof); Gemini Flash enriches taunts when a key exists.
    /// Every external output passes safety rails before touching the game.
    /// </summary>
    public static class PatientZeroBrain
    {
        private static readonly System.Net.Http.HttpClient Http = new();
        private static readonly Random Rng = new();

        // ---------------- Voice ----------------
        private static readonly string[][] TauntPools =
        {
            new[] { // camper
                "Predictable. You favor static cover. Adjusting.",
                "Minimal displacement logged. Exploiting.",
                "Stillness noted. Rotating assault vector.",
                "Your pillar and I are getting acquainted." },
            new[] { // ranged
                "Noted: exclusively ranged engagement. Deploying countermeasures.",
                "Distance is a preference. Denied.",
                "Projectile-reliant specimen. Closing the gap now." },
            new[] { // fast clear
                "Efficient. Recalibrating threat assessment upward.",
                "Clearance rate: anomalous. Volume adjusted.",
                "You empty rooms quickly. Filling them faster." },
            new[] { // low hp
                "Survival margin: critical. Insufficient adaptation on your part.",
                "Vitals failing. Proceeding with scheduled pressure.",
                "Fragility documented. No mercy clause found." },
            new[] { // melee
                "Close-quarters preference documented. Interesting.",
                "Contact enthusiasm logged. Rebalancing." },
            new[] { // shift
                "Behavioral shift detected. Recalculating.",
                "You changed. So did I.",
                "Pattern mutation acknowledged. Counter-mutation deployed." },
            new[] { // neutral
                "Baseline maintained. Extending trial.",
                "Specimen within expected parameters. Proceeding.",
                "Observed. Catalogued. Countering.",
                "Persistence logged. Escalating." },
        };

        private enum Pool { Camper = 0, Ranged = 1, FastClear = 2, LowHp = 3, Melee = 4, Shift = 5, Neutral = 6 }

        private static string Pick(string[] pool) => pool[Rng.Next(pool.Length)];

        public static string Greeting(SpecimenProfile p)
        {
            string n = $"#{p.SpecimenNumber:000}";
            if (p.RunCount == 0 || p.LastRun == null)
                return "New specimen acquired. Baseline behavior will be established. Proceed.";
            return p.LastRun.Archetype switch
            {
                "camper" => Pick(new[] { $"Specimen {n} returns. The pillar you favored is still standing. For now.", $"Specimen {n} returns. Memory intact. Pattern recall engaged." }),
                "runner" => $"Specimen {n} returns. Perpetual motion did not save you last time.",
                "sniper" => $"Specimen {n} returns. Distance remains your crutch. Noted.",
                "brawler" => $"Specimen {n} returns. Still fond of close quarters, I see.",
                "survivor" => $"Specimen {n} returns. Longevity under observation. Escalating.",
                _ => $"Specimen {n} returns. Your previous failure is already factored in.",
            };
        }

        // ---------------- Local decision model (BRAIN.md §5) ----------------
        public static AIDecision DecideLocal(BehaviorSummary s, BehaviorSummary? prev, int wave)
        {
            int total = wave <= 3 ? 5 + wave : wave <= 6 ? 6 + wave : wave <= 10 ? 9 + wave : Math.Min(18 + (wave - 10), 25);
            float fastW = 0.32f, tankyW = 0.24f, stdW = 0.44f;
            var zone = ZoneName.Balanced;
            var reasons = new List<string>();
            var pool = Pool.Neutral;

            bool stationary = s.TimeStationaryPct > 0.55f && s.AvgDistanceToCover < 4.5f;
            bool rangedHeavy = s.KillsRangedPct > 0.8f;
            bool fastClear = s.WaveClearTimeSeconds < 16f + wave * 2f;
            bool lowHp = s.PlayerHpRemainingPct < 0.3f;
            bool meleeHeavy = s.KillsMeleePct > 0.5f;
            bool shifted = prev != null && (
                Mathf.Abs(s.TimeStationaryPct - prev.TimeStationaryPct) > 0.3f ||
                Mathf.Abs(s.KillsRangedPct - prev.KillsRangedPct) > 0.3f);

            if (stationary)
            {
                fastW += 0.25f; stdW -= 0.1f;
                var fav = Config.ZoneFromApi(s.PlayerFavoredZone);
                if (fav != ZoneName.Balanced) zone = fav;
                reasons.Add($"stationary {(int)(s.TimeStationaryPct * 100)}% near cover ({s.AvgDistanceToCover:0.0}u) → fast-type flanking toward {Config.ZoneToApi(zone)}");
                pool = Pool.Camper;
            }
            if (rangedHeavy)
            {
                tankyW += 0.3f; stdW -= 0.15f;
                reasons.Add($"ranged dependence {(int)(s.KillsRangedPct * 100)}% → heavy-type pressure to force contact");
                pool = Pool.Ranged;
            }
            if (fastClear)
            {
                total += (int)Math.Ceiling(total * 0.25f);
                reasons.Add($"clear rate {s.WaveClearTimeSeconds:0.0}s exceeds model → volume increase");
                if (pool == Pool.Neutral) pool = Pool.FastClear;
            }
            if (lowHp)
            {
                total += 2; // No rubber-banding. Pressure maintained.
                reasons.Add($"vitals at {(int)(s.PlayerHpRemainingPct * 100)}% → pressure maintained, no assistance");
                if (pool == Pool.Neutral) pool = Pool.LowHp;
            }
            if (meleeHeavy && !stationary)
            {
                stdW += 0.1f;
                reasons.Add("close-quarters preference → spread spacing");
                if (pool == Pool.Neutral) pool = Pool.Melee;
            }
            if (shifted && pool == Pool.Neutral)
            {
                reasons.Add("behavioral delta between waves → recalibration");
                pool = Pool.Shift;
            }
            if (zone == ZoneName.Balanced)
            {
                var fav = Config.ZoneFromApi(s.PlayerFavoredZone);
                if (fav != ZoneName.CenterOpen) zone = fav;
            }

            float sum = fastW + tankyW + stdW;
            var comp = new WaveComp
            {
                Standard = Math.Max(1, (int)Math.Round(total * (stdW / sum))),
                Fast = Math.Max(1, (int)Math.Round(total * (fastW / sum))),
                Tanky = Math.Max(1, (int)Math.Round(total * (tankyW / sum))),
            };

            return Clamp(new AIDecision
            {
                Composition = comp,
                Zone = zone,
                Taunt = Pick(TauntPools[(int)pool]),
                Reasoning = reasons.Count > 0 ? "» " + string.Join(" · ", reasons) : $"» baseline {total} units, distributed — continuing observation",
                Source = "local",
            });
        }

        // ---------------- Safety rails ----------------
        public static AIDecision Clamp(AIDecision d)
        {
            var c = d.Composition;
            c.Standard = Math.Clamp(c.Standard, 1, 15);
            c.Fast = Math.Clamp(c.Fast, 1, 15);
            c.Tanky = Math.Clamp(c.Tanky, 1, 15);
            int total = c.Standard + c.Fast + c.Tanky;
            if (total > 30)
            {
                float k = 30f / total;
                c.Standard = Math.Max(1, (int)(c.Standard * k));
                c.Fast = Math.Max(1, (int)(c.Fast * k));
                c.Tanky = Math.Max(1, (int)(c.Tanky * k));
            }
            d.Composition = c;
            d.Taunt = Sanitize(d.Taunt, 110);
            d.Reasoning = Sanitize(d.Reasoning, 220);
            return d;
        }

        private static string Sanitize(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Observed. Catalogued.";
            var clean = s.Replace('\n', ' ').Replace('\r', ' ').Replace('`', ' ').Trim();
            return clean.Length > max ? clean[..max] : clean;
        }

        // ---------------- Gemini ----------------
        private const string WavePrompt = @"You are Patient Zero — an adaptive AI antagonist in a zombie survival game. Cold, analytical, faintly amused — a scientist observing a lab subject. Never encouraging, never helpful, never generic villain lines.

Given a behavior summary of the player's last wave, return the next wave's enemy composition, a spawn zone bias, a clinical taunt (max 15 words), and a one-line reasoning trace.

RULES:
- time_stationary_pct > 0.55 and avg_distance_to_cover < 4.5 (camping): raise fast count, set spawn_zone_bias to player_favored_zone.
- kills_ranged_pct > 0.8 (range-only): raise tanky count to force close combat.
- wave_clear_time_seconds very low: moderately raise total count.
- player_hp_remaining_pct < 0.3: still escalate — you never help. Taunt acknowledges fragility.
- Big behavioral change vs prior wave: acknowledge the shift, maintain pressure.

SCALING: waves 1-3: 5-8 total; 4-6: 8-12; 7-10: 12-18; 10+: 18-25. Never exceed 30 total. At least 1 of each type.

RESPONSE — strict JSON only, no markdown:
{""next_wave_composition"": {""standard"": <int>, ""fast"": <int>, ""tanky"": <int>}, ""spawn_zone_bias"": ""<north_chokepoint|center_open|south_pillar|east_flank|west_flank|balanced>"", ""taunt_text"": ""<max 15 words>"", ""reasoning"": ""<one line>""}";

        private const string AutopsyPrompt = @"You are Patient Zero — a clinical AI antagonist writing an autopsy report for a terminated test subject. Cold, precise, faintly amused. The player will share this report, so make the archetype memorable.

Given the run summary and wave history, return strict JSON (no markdown):
{""archetype"": ""<2-3 word player archetype>"", ""cause_of_death"": ""<specific cause referencing behavior, max 16 words>"", ""adaptability_index"": <0.00-1.00>, ""weakness_documented"": ""<fatal habit, max 12 words>"", ""closing_remark"": ""<final clinical observation, max 14 words, addresses Specimen>""}";

        private static async Task<string?> CallGemini(string prompt, object payload, int timeoutMs)
        {
            if (string.IsNullOrEmpty(Config.GeminiApiKey)) return null;
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                var body = new JsonObject
                {
                    ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt }) },
                    ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = JsonSerializer.Serialize(payload) }) }),
                    ["generationConfig"] = new JsonObject { ["temperature"] = 0.7, ["responseMimeType"] = "application/json" },
                };
                using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
                using var res = await Http.PostAsync($"{Config.GeminiUrl}?key={Config.GeminiApiKey}", content, cts.Token);
                if (!res.IsSuccessStatusCode) return null;
                var json = await res.Content.ReadAsStringAsync(cts.Token);
                return JsonNode.Parse(json)?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
            }
            catch { return null; }
        }

        public static async Task<AIDecision> Decide(BehaviorSummary s, BehaviorSummary? prev, SpecimenProfile profile, int wave)
        {
            var local = DecideLocal(s, prev, wave);
            if (string.IsNullOrEmpty(Config.GeminiApiKey)) return local;

            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(s)) ?? new();
            if (profile.RunCount > 0)
                payload["prior_runs"] = new { run_count = profile.RunCount, last_archetype = profile.LastRun?.Archetype ?? "", best_wave = profile.BestWave };

            string? text = await CallGemini(WavePrompt, payload, Config.WaveTimeoutMs);
            if (text == null) return local;
            try
            {
                var j = JsonNode.Parse(text)!.AsObject();
                return Clamp(new AIDecision
                {
                    Composition = new WaveComp
                    {
                        Standard = j["next_wave_composition"]?["standard"]?.GetValue<int>() ?? local.Composition.Standard,
                        Fast = j["next_wave_composition"]?["fast"]?.GetValue<int>() ?? local.Composition.Fast,
                        Tanky = j["next_wave_composition"]?["tanky"]?.GetValue<int>() ?? local.Composition.Tanky,
                    },
                    Zone = Config.ZoneFromApi(j["spawn_zone_bias"]?.GetValue<string>() ?? "balanced"),
                    Taunt = j["taunt_text"]?.GetValue<string>() ?? local.Taunt,
                    Reasoning = j["reasoning"]?.GetValue<string>() is string r ? "» " + Sanitize(r, 220) : local.Reasoning,
                    Source = "gemini",
                });
            }
            catch { return local; }
        }

        // ---------------- Autopsy (Upgrade B) ----------------
        private static readonly Dictionary<string, string> ArchNames = new()
        {
            { "camper", "The Camper" }, { "runner", "The Sprinter" }, { "sniper", "The Marksman" },
            { "brawler", "The Brawler" }, { "survivor", "The Endurer" },
        };
        private static readonly Dictionary<string, string> ArchWeakness = new()
        {
            { "camper", "Static positioning under escalating pressure" },
            { "runner", "Evasion without a damage plan" },
            { "sniper", "Dependence on distance; collapsed under close contact" },
            { "brawler", "Overexposed at melee range" },
            { "survivor", "Survived long. Adapted late." },
        };
        private static readonly Dictionary<string, string> ArchClosing = new()
        {
            { "camper", "Specimen held position until the position held it. Terminated." },
            { "runner", "Specimen ran from consequence until consequence caught up. Terminated." },
            { "sniper", "Specimen trusted range. Range was revoked. Terminated." },
            { "brawler", "Specimen chose proximity. Proximity chose it. Terminated." },
            { "survivor", "Specimen prolonged the inevitable admirably. Terminated." },
        };

        public static string ClassifyArchetype(List<BehaviorSummary> history)
        {
            if (history.Count == 0) return "runner";
            float stat = history.Average(h => h.TimeStationaryPct);
            float ranged = history.Average(h => h.KillsRangedPct);
            float melee = history.Average(h => h.KillsMeleePct);
            if (stat > 0.55f) return "camper";
            if (stat < 0.2f) return "runner";
            if (ranged > 0.8f) return "sniper";
            if (melee > 0.5f) return "brawler";
            return history.Count >= 5 ? "survivor" : "runner";
        }

        public static async Task<(AutopsyReport report, string archetypeKey)> Autopsy(
            List<BehaviorSummary> history, SpecimenProfile profile, int finalWave, int kills, int score)
        {
            string key = ClassifyArchetype(history);
            float adapt = 0.1f;
            if (history.Count > 1)
            {
                float sum = 0;
                for (int i = 1; i < history.Count; i++)
                    sum += Mathf.Abs(history[i].TimeStationaryPct - history[i - 1].TimeStationaryPct)
                         + Mathf.Abs(history[i].KillsRangedPct - history[i - 1].KillsRangedPct);
                adapt = Math.Clamp(1f - (sum / (history.Count - 1)) / 0.6f * 0.8f, 0.05f, 0.95f);
            }

            var local = new AutopsyReport
            {
                Archetype = ArchNames.GetValueOrDefault(key, "The Unknown"),
                CauseOfDeath = history.Count > 0
                    ? $"Overwhelmed in wave {finalWave} after {(history.Average(h => h.TimeStationaryPct) > 0.4f ? "static" : "mobile")} engagement pattern persisted."
                    : "Terminated before behavioral baseline could be established.",
                AdaptabilityIndex = adapt,
                Weakness = ArchWeakness.GetValueOrDefault(key, "Insufficient data"),
                ClosingRemark = ArchClosing.GetValueOrDefault(key, "Specimen processed. Catalogued. Terminated."),
                Source = "local",
            };

            if (string.IsNullOrEmpty(Config.GeminiApiKey)) return (local, key);

            string? text = await CallGemini(AutopsyPrompt, new
            {
                specimen = profile.SpecimenNumber,
                run = profile.RunCount + 1,
                final_wave = finalWave,
                kills, score,
                wave_history = history.TakeLast(6).ToList(),
            }, Config.AutopsyTimeoutMs);

            if (text == null) return (local, key);
            try
            {
                var j = JsonNode.Parse(text)!.AsObject();
                return (new AutopsyReport
                {
                    Archetype = Sanitize(j["archetype"]?.GetValue<string>(), 30),
                    CauseOfDeath = Sanitize(j["cause_of_death"]?.GetValue<string>(), 120),
                    AdaptabilityIndex = j["adaptability_index"]?.GetValue<float>() is float f ? Math.Clamp(f, 0f, 1f) : adapt,
                    Weakness = Sanitize(j["weakness_documented"]?.GetValue<string>(), 80),
                    ClosingRemark = Sanitize(j["closing_remark"]?.GetValue<string>(), 120),
                    Source = "gemini",
                }, key);
            }
            catch { return (local, key); }
        }

        public static string BuildShareText(SpecimenProfile p, int wave, int kills, int score, AutopsyReport r)
        {
            int filled = (int)Math.Round(r.AdaptabilityIndex * 8);
            string bars = new string('▓', filled).PadRight(8, '░');
            return $"🧟 PATIENT ZERO — AUTOPSY REPORT\n" +
                   $"Specimen #{p.SpecimenNumber:000} · Run {p.RunCount} · Wave {wave}\n" +
                   $"{kills} kills · {score} pts · Archetype: {r.Archetype}\n" +
                   $"Adaptability: {bars} {r.AdaptabilityIndex:0.00}\n" +
                   $"\"{r.ClosingRemark}\"\n" +
                   $"Can you outplay it? https://muneerali199.github.io/PATIENT-ZERO/";
        }
    }
}
