using Godot;
using System.Collections.Generic;

namespace PatientZero
{
    public enum EnemyType { Standard, Fast, Tanky, Boss }

    public enum ZoneName
    {
        NorthChokepoint, SouthPillar, EastFlank, WestFlank, CenterOpen, Balanced
    }

    public enum ThemeBucket { Coastal, Desert, Temperate, Mountain, Urban, Jungle }

    public struct Pillar
    {
        public Vector2 Pos;
        public float R;
        public string Name;
        public Pillar(float x, float y, float r, string name) { Pos = new Vector2(x, y); R = r; Name = name; }
    }

    public struct WaveComp { public int Standard; public int Fast; public int Tanky; }

    public struct ThemeData
    {
        public string Label;
        public Color Bg, Floor, Grid, PillarFill, PillarEdge, Accent, Fog;
        public Color EnemyStandard, EnemyFast, EnemyTanky;
    }

    public static class Config
    {
        public const float WorldW = 40f;
        public const float WorldH = 26f;

        public static readonly Pillar[] Pillars =
        {
            new Pillar(-9f, -6f, 1.9f, "north_west_pillar"),
            new Pillar(9f, -6f, 1.9f, "north_east_pillar"),
            new Pillar(-9f, 6f, 1.9f, "south_west_pillar"),
            new Pillar(9f, 6f, 1.9f, "south_east_pillar"),
        };

        public static readonly Dictionary<ZoneName, Vector2> ZoneCenters = new()
        {
            { ZoneName.NorthChokepoint, new Vector2(0, -10.5f) },
            { ZoneName.SouthPillar, new Vector2(0, 10.5f) },
            { ZoneName.EastFlank, new Vector2(16.5f, 0) },
            { ZoneName.WestFlank, new Vector2(-16.5f, 0) },
            { ZoneName.CenterOpen, new Vector2(0, 0) },
        };

        public static readonly Dictionary<ZoneName, Vector2[]> ZoneSpawns = new()
        {
            { ZoneName.NorthChokepoint, new[] { new Vector2(-6, -12), new Vector2(0, -12.4f), new Vector2(6, -12) } },
            { ZoneName.SouthPillar, new[] { new Vector2(-6, 12), new Vector2(0, 12.4f), new Vector2(6, 12) } },
            { ZoneName.EastFlank, new[] { new Vector2(18.5f, -8), new Vector2(18.5f, 0), new Vector2(18.5f, 8) } },
            { ZoneName.WestFlank, new[] { new Vector2(-18.5f, -8), new Vector2(-18.5f, 0), new Vector2(-18.5f, 8) } },
            { ZoneName.CenterOpen, new[] { new Vector2(0, 0), new Vector2(5, 2), new Vector2(-5, -2) } },
        };

        // Player
        public const float PlayerMaxHp = 100f;
        public const float PlayerSpeed = 5.6f;
        public const float PlayerRadius = 0.75f;
        public const float FireCooldown = 0.24f;
        public const float BulletDmg = 35f;
        public const float BulletSpeed = 19f;
        public const float BulletLife = 0.85f;
        public const float MeleeDmg = 55f;
        public const float MeleeRange = 2.2f;
        public const float MeleeCooldown = 0.7f;
        public const float PurgeDmg = 130f;
        public const float PurgeRadius = 5.6f;
        public const float PurgeCooldown = 15f;
        public const float InvulnTime = 0.4f;

        // Enemies: hp, speed, damage, radius, score — slower shamblers, tougher hides
        public static readonly Dictionary<EnemyType, (float hp, float speed, float dmg, float radius, int score)> Enemies = new()
        {
            { EnemyType.Standard, (170f, 2.05f, 10f, 0.85f, 10) },
            { EnemyType.Fast, (95f, 4.3f, 5f, 0.65f, 15) },
            { EnemyType.Tanky, (430f, 1.1f, 20f, 1.15f, 25) },
            { EnemyType.Boss, (2600f, 1.5f, 35f, 1.5f, 500) },
        };

        public const float EnemyAttackRange = 2.3f;
        public const float EnemyAttackCd = 1.15f;
        public const float SpawnStagger = 0.32f;
        public const float IntermissionMin = 1.1f;
        public const float TauntHold = 2.9f;
        public const float StationaryThreshold = 0.6f;

        public static WaveComp Wave1Bias(ThemeBucket b) => b switch
        {
            ThemeBucket.Coastal => new WaveComp { Standard = 2, Fast = 3, Tanky = 1 },
            ThemeBucket.Desert => new WaveComp { Standard = 2, Fast = 1, Tanky = 3 },
            ThemeBucket.Mountain => new WaveComp { Standard = 3, Fast = 1, Tanky = 2 },
            ThemeBucket.Urban => new WaveComp { Standard = 1, Fast = 4, Tanky = 1 },
            _ => new WaveComp { Standard = 2, Fast = 2, Tanky = 2 },
        };

        public static readonly Dictionary<ThemeBucket, ThemeData> Themes = new()
        {
            { ThemeBucket.Temperate, new ThemeData {
                Label = "TEMPERATE / ROTTED STRAIN",
                Bg = C("#070b07"), Floor = C("#111a12"), Grid = C("#1a281c"),
                PillarFill = C("#26332a"), PillarEdge = C("#3a4f3e"), Accent = C("#7da05a"),
                EnemyStandard = C("#6f9b4a"), EnemyFast = C("#9bc46f"), EnemyTanky = C("#42582f"),
                Fog = new Color(0.078f, 0.133f, 0.086f, 0.16f) } },
            { ThemeBucket.Coastal, new ThemeData {
                Label = "COASTAL / DROWNED STRAIN",
                Bg = C("#050d14"), Floor = C("#0e1c26"), Grid = C("#152836"),
                PillarFill = C("#1d3441"), PillarEdge = C("#2d5163"), Accent = C("#6ec6e9"),
                EnemyStandard = C("#4a8dab"), EnemyFast = C("#7fb8cf"), EnemyTanky = C("#2d566b"),
                Fog = new Color(0.063f, 0.157f, 0.227f, 0.2f) } },
            { ThemeBucket.Desert, new ThemeData {
                Label = "DESERT / SCORCHED STRAIN",
                Bg = C("#140c04"), Floor = C("#221708"), Grid = C("#33230d"),
                PillarFill = C("#4a3218"), PillarEdge = C("#6d4b22"), Accent = C("#e8a33d"),
                EnemyStandard = C("#d97b29"), EnemyFast = C("#eba253"), EnemyTanky = C("#8f4c14"),
                Fog = new Color(0.259f, 0.165f, 0.047f, 0.18f) } },
            { ThemeBucket.Mountain, new ThemeData {
                Label = "MOUNTAIN / FROZEN STRAIN",
                Bg = C("#0a0e15"), Floor = C("#161d29"), Grid = C("#212b3b"),
                PillarFill = C("#2a3140"), PillarEdge = C("#46536b"), Accent = C("#bcd4e6"),
                EnemyStandard = C("#a8cfe0"), EnemyFast = C("#d3e8f2"), EnemyTanky = C("#6b8299"),
                Fog = new Color(0.706f, 0.804f, 0.886f, 0.1f) } },
            { ThemeBucket.Jungle, new ThemeData {
                Label = "JUNGLE / WILD STRAIN",
                Bg = C("#04100a"), Floor = C("#1d3319"), Grid = C("#16301a"),
                PillarFill = C("#2a3d24"), PillarEdge = C("#9fff5f"), Accent = C("#c4ff4d"),
                EnemyStandard = C("#4a7c3a"), EnemyFast = C("#7cb35a"), EnemyTanky = C("#2d4a26"),
                Fog = new Color(0.1f, 0.2f, 0.1f, 0.22f) } },
            { ThemeBucket.Urban, new ThemeData {
                Label = "URBAN / INFECTED STRAIN",
                Bg = C("#0a0a10"), Floor = C("#15151f"), Grid = C("#20202e"),
                PillarFill = C("#26263a"), PillarEdge = C("#3d3d5c"), Accent = C("#d63cf0"),
                EnemyStandard = C("#c04ae0"), EnemyFast = C("#e079f0"), EnemyTanky = C("#7c2b96"),
                Fog = new Color(0.196f, 0.078f, 0.259f, 0.2f) } },
        };

        public static Color EnemyTint(ThemeData t, EnemyType type) => type switch
        {
            EnemyType.Fast => t.EnemyFast,
            EnemyType.Tanky => t.EnemyTanky,
            _ => t.EnemyStandard,
        };

        // ---------- weapons ----------
        public class WeaponDef
        {
            public string Name = "";
            public string Sound = "shoot";
            public int Mag;
            public float Damage;
            public float FireCd;
            public float ReloadTime;
            public float Speed;
            public Color BulletColor;
            public int Pellets = 1;
            public float Spread;
            public string Model = "";
            // player-space chest-rig mount (meters, player faces +Z, right = +X)
            public Vector3 MountPos = new(0.20f, 1.22f, 0.28f);
            public Vector3 MountRot = new(0f, 0f, 0f);
            public float MountScale = 1.0f;
            public float ForegripZ = 0.2f; // where the left hand grips the rail
            public float VmScale = 0.8f; // first-person viewmodel scale
            public Vector3 VmRot = new(0f, 0f, 0f); // extra viewmodel rotation (camera space, holder already flips Y 180)
        }

        public static readonly WeaponDef[] Weapons =
        {
            new WeaponDef { Name = "PULSE PISTOL", Sound = "pistol", Mag = 10, Damage = 55, FireCd = 0.30f, ReloadTime = 1.1f, Speed = 17f, BulletColor = new Color(1f, 0.7f, 0.3f), Pellets = 1, Model = "res://assets/models/pistol.glb",
                MountPos = new Vector3(0.21f, 1.30f, 0.26f), MountRot = new Vector3(0f, 0f, 0f), MountScale = 1.0f, ForegripZ = 0.13f, VmScale = 0.8f, VmRot = new Vector3(0f, 0f, 0f) },
            new WeaponDef { Name = "AKM REAPER", Sound = "akm_fire", Mag = 30, Damage = 32, FireCd = 0.13f, ReloadTime = 1.6f, Speed = 20f, BulletColor = new Color(1f, 0.55f, 0.15f), Pellets = 1, Model = "res://assets/models/akm.glb",
                MountPos = new Vector3(0.17f, 1.32f, 0.30f), MountRot = new Vector3(0f, 0f, 0f), MountScale = 1.0f, ForegripZ = 0.26f, VmScale = 0.95f, VmRot = new Vector3(0f, 0f, 0f) },
            new WeaponDef { Name = "VOID SCATTERGUN", Sound = "shotgun", Mag = 6, Damage = 18, FireCd = 0.72f, ReloadTime = 2.0f, Speed = 15f, BulletColor = new Color(0.4f, 0.85f, 1f), Pellets = 5, Spread = 0.13f, Model = "res://assets/models/scattergun.glb",
                MountPos = new Vector3(0.18f, 1.32f, 0.26f), MountRot = new Vector3(0f, 0f, 0f), MountScale = 1.0f, ForegripZ = 0.34f, VmScale = 0.85f, VmRot = new Vector3(0f, 0f, 0f) },
        };

        // AI — gemini-2.0-flash is RETIRED (404s); 3.5-flash-lite verified fast (~2s) on our key
        public const string GeminiModel = "gemini-3.5-flash-lite";
        public const string GeminiUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/" + GeminiModel + ":generateContent";
        public const int WaveTimeoutMs = 2500;
        public const int AutopsyTimeoutMs = 3200;
        public static string GeminiApiKey = ""; // supply at runtime with --key=...; local AI fallback remains available

        public static string ZoneToApi(ZoneName z) => z switch
        {
            ZoneName.NorthChokepoint => "north_chokepoint",
            ZoneName.SouthPillar => "south_pillar",
            ZoneName.EastFlank => "east_flank",
            ZoneName.WestFlank => "west_flank",
            ZoneName.CenterOpen => "center_open",
            _ => "balanced",
        };

        public static ZoneName ZoneFromApi(string s) => (s ?? "").Trim().ToLowerInvariant() switch
        {
            "north_chokepoint" => ZoneName.NorthChokepoint,
            "south_pillar" => ZoneName.SouthPillar,
            "east_flank" => ZoneName.EastFlank,
            "west_flank" => ZoneName.WestFlank,
            "center_open" => ZoneName.CenterOpen,
            _ => ZoneName.Balanced,
        };

        public static string ThemeToApi(ThemeBucket b) => b.ToString().ToLowerInvariant();

        private static Color C(string hex) => new Color(hex);
    }
}
