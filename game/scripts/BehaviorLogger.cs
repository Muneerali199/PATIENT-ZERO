using Godot;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PatientZero
{
    public class BehaviorSummary
    {
        [JsonPropertyName("wave_number")] public int WaveNumber { get; set; }
        [JsonPropertyName("time_stationary_pct")] public float TimeStationaryPct { get; set; }
        [JsonPropertyName("time_moving_pct")] public float TimeMovingPct { get; set; }
        [JsonPropertyName("kills_ranged_pct")] public float KillsRangedPct { get; set; }
        [JsonPropertyName("kills_melee_pct")] public float KillsMeleePct { get; set; }
        [JsonPropertyName("avg_engagement_distance")] public float AvgEngagementDistance { get; set; }
        [JsonPropertyName("avg_distance_to_cover")] public float AvgDistanceToCover { get; set; }
        [JsonPropertyName("wave_clear_time_seconds")] public float WaveClearTimeSeconds { get; set; }
        [JsonPropertyName("player_hp_remaining_pct")] public float PlayerHpRemainingPct { get; set; }
        [JsonPropertyName("location_seed")] public string LocationSeed { get; set; } = "";
        [JsonPropertyName("player_favored_zone")] public string PlayerFavoredZone { get; set; } = "balanced";
    }

    public class BehaviorLogger
    {
        private float _totalTime, _stationaryTime, _movingTime, _coverDistTotal;
        private int _frameCount;
        private readonly List<(string kind, float dist)> _kills = new();
        private readonly Dictionary<ZoneName, float> _zoneTime = new();
        private bool _active;

        public void Reset()
        {
            _totalTime = _stationaryTime = _movingTime = _coverDistTotal = 0;
            _frameCount = 0;
            _kills.Clear();
            _zoneTime.Clear();
            _active = true;
        }

        public void Stop() => _active = false;

        public void TrackFrame(float dt, Vector2 pos, Vector2 vel)
        {
            if (!_active) return;
            _totalTime += dt;
            if (vel.Length() < Config.StationaryThreshold) _stationaryTime += dt;
            else _movingTime += dt;

            float nearest = float.MaxValue;
            foreach (var p in Config.Pillars)
            {
                float d = pos.DistanceTo(p.Pos) - p.R;
                if (d < nearest) nearest = d;
            }
            _coverDistTotal += Mathf.Max(0, nearest);
            _frameCount++;

            var zone = ClassifyZone(pos);
            _zoneTime.TryGetValue(zone, out float t);
            _zoneTime[zone] = t + dt;
        }

        public void TrackKill(string kind, float dist)
        {
            if (!_active) return;
            _kills.Add((kind, dist));
        }

        private static ZoneName ClassifyZone(Vector2 pos)
        {
            ZoneName best = ZoneName.CenterOpen;
            float bestD = float.MaxValue;
            foreach (var kv in Config.ZoneCenters)
            {
                float d = pos.DistanceTo(kv.Value);
                if (d < bestD) { bestD = d; best = kv.Key; }
            }
            return best;
        }

        public ZoneName FavoredZone()
        {
            ZoneName best = ZoneName.CenterOpen;
            float bestT = -1;
            foreach (var kv in _zoneTime)
            {
                if (kv.Value > bestT) { bestT = kv.Value; best = kv.Key; }
            }
            return best;
        }

        public BehaviorSummary Compile(int waveNumber, float hpPct, ThemeBucket seed)
        {
            int total = _kills.Count;
            int ranged = 0;
            float engDist = 0;
            foreach (var k in _kills)
            {
                if (k.kind == "ranged") ranged++;
                engDist += k.dist;
            }
            return new BehaviorSummary
            {
                WaveNumber = waveNumber,
                TimeStationaryPct = _totalTime > 0 ? _stationaryTime / _totalTime : 0,
                TimeMovingPct = _totalTime > 0 ? _movingTime / _totalTime : 0,
                KillsRangedPct = total > 0 ? (float)ranged / total : 0.5f,
                KillsMeleePct = total > 0 ? (float)(total - ranged) / total : 0.5f,
                AvgEngagementDistance = total > 0 ? engDist / total : 0,
                AvgDistanceToCover = _frameCount > 0 ? Mathf.Min(_coverDistTotal / _frameCount, 99f) : 99f,
                WaveClearTimeSeconds = _totalTime,
                PlayerHpRemainingPct = hpPct,
                LocationSeed = waveNumber == 1 ? Config.ThemeToApi(seed) : "",
                PlayerFavoredZone = Config.ZoneToApi(FavoredZone()),
            };
        }
    }
}
