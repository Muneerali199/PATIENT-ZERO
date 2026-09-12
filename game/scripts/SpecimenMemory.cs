using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PatientZero
{
    public class RunRecord
    {
        [JsonPropertyName("waves_survived")] public int WavesSurvived { get; set; }
        [JsonPropertyName("score")] public int Score { get; set; }
        [JsonPropertyName("kills")] public int Kills { get; set; }
        [JsonPropertyName("archetype")] public string Archetype { get; set; } = "";
        [JsonPropertyName("cause_of_death")] public string CauseOfDeath { get; set; } = "";
    }

    public class SpecimenProfile
    {
        [JsonPropertyName("specimen_number")] public int SpecimenNumber { get; set; } = 1;
        [JsonPropertyName("run_count")] public int RunCount { get; set; }
        [JsonPropertyName("last_run")] public RunRecord? LastRun { get; set; }
        [JsonPropertyName("last_3_wave_summaries")] public List<BehaviorSummary> LastSummaries { get; set; } = new();
        [JsonPropertyName("best_wave")] public int BestWave { get; set; }
        [JsonPropertyName("best_score")] public int BestScore { get; set; }

        private const string SavePath = "user://specimen.json";

        public static SpecimenProfile Load()
        {
            try
            {
                if (FileAccess.FileExists(SavePath))
                {
                    using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                    var p = JsonSerializer.Deserialize<SpecimenProfile>(f.GetAsText());
                    if (p != null) return p;
                }
            }
            catch { /* corrupted save → fresh specimen */ }
            return new SpecimenProfile();
        }

        public void Save()
        {
            try
            {
                using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
                f.StoreString(JsonSerializer.Serialize(this));
            }
            catch { /* storage unavailable */ }
        }

        public void RecordRun(RunRecord run, List<BehaviorSummary> history)
        {
            RunCount++;
            LastRun = run;
            var merged = new List<BehaviorSummary>(LastSummaries);
            merged.AddRange(history);
            LastSummaries = merged.Count > 3 ? merged.GetRange(merged.Count - 3, 3) : merged;
            if (run.WavesSurvived > BestWave) BestWave = run.WavesSurvived;
            if (run.Score > BestScore) BestScore = run.Score;
            Save();
        }
    }
}
