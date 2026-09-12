using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PatientZero
{
    /// <summary>
    /// PATIENT ZERO: PROTOCOL — market build (Godot 4, C#).
    /// Single composition root: simulation runs on the XZ plane in world units,
    /// 3D nodes are visual mirrors. AI brain / logger / memory are engine-agnostic.
    /// Dev flags: --theme=desert --key=GEMINI_KEY --screenshot
    /// </summary>
    public partial class GameRoot3D : Node3D
    {
        private enum Phase { Menu, Playing, Intermission, Over }

        // ---------- state ----------
        private Phase _phase = Phase.Menu;
        private float _time;
        private int _wave, _score, _kills;
        private SpecimenProfile _profile = new();
        private readonly BehaviorLogger _logger = new();
        private readonly List<BehaviorSummary> _history = new();
        private ThemeData _theme;
        private ThemeBucket _bucket = ThemeBucket.Temperate;
        private string _seedLabel = "";
        private bool _screenshotMode;
        private float _shotAt = -1f;
        private bool _forceBoss;
        private bool _menuShotMode;

        // ---------- sim entities ----------
        private readonly Player _player = new();
        private readonly List<Enemy> _enemies = new();
        private readonly List<Projectile> _projectiles = new();
        private readonly List<Particle> _particles = new();
        private readonly List<(float at, EnemyType type, Vector2 pos)> _spawnQueue = new();
        private int _nextEnemyId = 1;

        // ---------- enemy water bolts ----------
        private readonly List<EnemyBolt> _bolts = new();
        private readonly Dictionary<EnemyBolt, Node3D> _boltNodes = new();

        private void CastWaterBolt(Enemy e)
        {
            var p = _player;
            var dir = (p.Pos - e.Pos).Normalized();
            SpawnBoltWithVisual(e.Pos + dir * 1.2f, dir * 11f);
            // boss enraged: triple water spread under 50% integrity
            if (e.Type == EnemyType.Boss && e.Hp < e.MaxHp * 0.5f)
            {
                foreach (float spread in new[] { -0.3f, 0.3f })
                {
                    var d2 = dir.Rotated(spread);
                    SpawnBoltWithVisual(e.Pos + d2 * 1.2f, d2 * 11f);
                }
            }
            PlaySfx("waterbolt");
        }

        private void SpawnBoltWithVisual(Vector2 pos, Vector2 vel)
        {
            var bolt = new EnemyBolt { Pos = pos, Vel = vel };
            _bolts.Add(bolt);

            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.25f, 0.85f, 1f),
                EmissionEnabled = true,
                Emission = new Color(0.25f, 0.85f, 1f),
                EmissionEnergyMultiplier = 4f,
            };
            var node = new Node3D();
            var ball = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.24f, Height = 0.48f }, MaterialOverride = mat };
            node.AddChild(ball);
            var light = new OmniLight3D { LightColor = new Color(0.3f, 0.85f, 1f), LightEnergy = 1.6f, OmniRange = 5f };
            node.AddChild(light);
            var trail = new CpuParticles3D
            {
                Emitting = true,
                Amount = 24,
                Lifetime = 0.45f,
                LocalCoords = false,
                Direction = new Vector3(0, 0, 0),
                Spread = 12f,
                InitialVelocityMin = 0.2f,
                InitialVelocityMax = 0.8f,
                Gravity = new Vector3(0, 0.5f, 0),
                ScaleAmountMin = 0.05f,
                ScaleAmountMax = 0.16f,
                Color = new Color(0.4f, 0.9f, 1f, 0.85f),
                Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f },
            };
            node.AddChild(trail);
            node.Position = new Vector3(bolt.Pos.X, 1.1f, bolt.Pos.Y);
            _fxRoot.AddChild(node);
            _boltNodes[bolt] = node;
        }

        private void KillBolt(EnemyBolt b, bool splash = true)
        {
            if (b.Dead) return;
            b.Dead = true;
            if (splash)
            {
                SpawnBurst(new Vector3(b.Pos.X, 0.7f, b.Pos.Y), new Color(0.3f, 0.85f, 1f), 16, 5f);
                PlaySfx("splash");
            }
            if (_boltNodes.TryGetValue(b, out var node))
            {
                _boltNodes.Remove(b);
                node.QueueFree();
            }
        }

        // ---------- AI ----------
        private Task<AIDecision>? _pendingDecision;
        private AIDecision? _decision;
        private float _intermissionT;
        private Task<(AutopsyReport report, string archetypeKey)>? _pendingAutopsy;
        private AutopsyReport? _lastReport;

        // ---------- 3D visuals ----------
        private Camera3D _cam = null!;
        private DirectionalLight3D _sun = null!;
        private WorldEnvironment _worldEnv = null!;
        private Node3D _enemyRoot = null!;
        private Node3D _fxRoot = null!;
        private Node3D _playerNode = null!;
        private Node3D? _rifle;
        private AnimationPlayer? _playerAnim;
        private RigData? _playerRig;
        private OmniLight3D _muzzleLight = null!;
        private MeshInstance3D _purgeRing = null!;
        private float _purgeRingT = -1f;
        private float _shake;

        // ---------- camera modes ----------
        private enum CamMode { Top, Tpp, Fpp }
        private CamMode _camMode = CamMode.Top;
        private float _yaw, _pitch = -0.04f;
        private Vector3 _camPos = new(0, 25f, 15.5f);
        private Vector3 _camRot = new(-1.01f, 0, 0);
        private float _camBlend = 1f;
        private bool _mouseLookActive;
        private readonly Dictionary<int, Node3D> _enemyNodes = new();
        private readonly Dictionary<int, AnimationPlayer?> _enemyAnims = new();
        private readonly Dictionary<int, RigData?> _enemyRigs = new();
        private readonly List<(Node3D node, Projectile sim)> _bulletNodes = new();

        private static readonly Dictionary<EnemyType, string> ModelPaths = new()
        {
            { EnemyType.Standard, "res://assets/custom/patient_zero_grunt.glb" },
            { EnemyType.Fast, "res://assets/custom/patient_zero_grunt.glb" },
            { EnemyType.Tanky, "res://assets/custom/patient_zero_grunt.glb" },
            { EnemyType.Boss, "res://assets/custom/patient_zero_rigged.glb" },
        };
        private static readonly Dictionary<EnemyType, float> ModelScale = new()
        {
            { EnemyType.Standard, 1.0f },
            { EnemyType.Fast, 1.0f },
            { EnemyType.Tanky, 1.0f },
            { EnemyType.Boss, 1.0f },
        };
        private readonly Dictionary<EnemyType, PackedScene?> _modelCache = new();
        private PackedScene? _pillarScene;

        // ---------- UI ----------
        private CanvasLayer _ui = null!;
        private Label _waveLabel = null!, _scoreLabel = null!, _brainBadge = null!, _themeTag = null!;
        private Label _tauntLabel = null!, _reasonLabel = null!;
        private Label _waveBanner = null!;
        private Control _crosshair = null!;
        private Button _camBtn = null!;
        private Button _fireBtn = null!;
        private bool _fireHeld;

        // ---------- weapons ----------
        private int _weaponIdx = 1;
        private readonly int[] _ammo = { 10, 30, 6 };
        private bool _reloading;
        private readonly List<Node3D?> _weaponNodes = new();
        private readonly List<Button> _weaponBtns = new();
        private Label _ammoLabel = null!;
        private bool _autoFire;
        private Tween? _kickTween;

        // ---------- blood + flame fx ----------
        private readonly Dictionary<int, (List<StandardMaterial3D> mats, List<Color> orig)> _bloodMats = new();
        private bool _weaponBoneSpace;
        private readonly List<(Control root, ColorRect fill)> _hpBars = new();
        private Control _bossBar = null!;
        private ColorRect _bossFill = null!;
        private Label _hpNum = null!;
        private readonly List<MeshInstance3D> _decals = new();
        private static readonly Color BloodRed = new(0.55f, 0.05f, 0.08f);

        private void BloodDecal(Vector2 pos, float scale = 1f)
        {
            var m = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.32f, 0.03f, 0.05f, 0.8f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            var d = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.3f * scale, BottomRadius = 0.36f * scale, Height = 0.012f },
                MaterialOverride = m,
                Position = new Vector3(pos.X, 0.015f, pos.Y),
            };
            _fxRoot.AddChild(d);
            _decals.Add(d);
            if (_decals.Count > 24) { var old = _decals[0]; _decals.RemoveAt(0); if (IsInstanceValid(old)) old.QueueFree(); }
            var tw = CreateTween();
            tw.TweenInterval(4f);
            tw.TweenProperty(m, "albedo_color:a", 0f, 2.5f);
            tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(d)) { _decals.Remove(d); d.QueueFree(); } }));
        }

        private void CollectBloodMats(int id, Node node)
        {
            var mats = new List<StandardMaterial3D>();
            var origs = new List<Color>();
            void Walk(Node n)
            {
                if (n is MeshInstance3D mi && mi.Mesh != null)
                {
                    int sc = mi.Mesh.GetSurfaceCount();
                    for (int i = 0; i < sc; i++)
                    {
                        var m = mi.GetActiveMaterial(i) as StandardMaterial3D;
                        if (m != null)
                        {
                            var dup = (StandardMaterial3D)m.Duplicate();
                            mi.SetSurfaceOverrideMaterial(i, dup);
                            mats.Add(dup);
                            origs.Add(dup.AlbedoColor);
                        }
                    }
                }
                foreach (var c in n.GetChildren()) Walk(c);
            }
            Walk(node);
            if (mats.Count > 0) _bloodMats[id] = (mats, origs);
        }

        // ---------- UI polish + virtual joysticks ----------
        private TextureRect _joyBaseL = null!, _joyKnobL = null!, _joyBaseR = null!, _joyKnobR = null!;
        private TextureRect? _menuRing;
        private Label? _titleGlow;
        private ColorRect? _menuAccent;
        private ImageTexture? _ringTex, _discTex;

        private static ImageTexture MakeRingTex(int size, float innerR, float outerR, Color c)
        {
            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            var center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = (new Vector2(x, y) - center).Length() / (size / 2f);
                if (d >= innerR && d <= outerR)
                {
                    float edge = Mathf.Min((d - innerR) / 0.05f, (outerR - d) / 0.05f);
                    img.SetPixel(x, y, new Color(c.R, c.G, c.B, c.A * Mathf.Clamp(edge, 0f, 1f)));
                }
            }
            return ImageTexture.CreateFromImage(img);
        }

        private static ImageTexture MakeDiscTex(int size, Color c)
        {
            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            var center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = (new Vector2(x, y) - center).Length() / (size / 2f);
                if (d <= 1f) img.SetPixel(x, y, new Color(c.R, c.G, c.B, c.A * Mathf.Clamp((1f - d) / 0.12f + 0.4f, 0f, 1f)));
            }
            return ImageTexture.CreateFromImage(img);
        }

        private static ImageTexture MakeVignetteTex(int w = 256, int h = 144)
        {
            var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x / (float)w - 0.5f) * 2f, dy = (y / (float)h - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp((d - 0.62f) * 1.1f, 0f, 0.6f);
                img.SetPixel(x, y, new Color(0, 0, 0, a));
            }
            return ImageTexture.CreateFromImage(img);
        }

        private void StyleBtn(Button b, float alpha = 0.62f)
        {
            var s = new StyleBoxFlat { BgColor = new Color(0.012f, 0.04f, 0.024f, alpha) };
            s.SetBorderWidthAll(1);
            s.BorderColor = new Color(0.18f, 1f, 0.53f, 0.4f);
            s.CornerRadiusTopLeft = s.CornerRadiusTopRight = s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 6;
            b.AddThemeStyleboxOverride("normal", s);
            b.AddThemeStyleboxOverride("hover", s);
            b.AddThemeStyleboxOverride("pressed", s);
            b.AddThemeColorOverride("font_color", TermGreen);
        }
        private Control _tauntPanel = null!;
        private ColorRect _hpFill = null!;
        private Button _purgeBtn = null!;
        private Control _startPanel = null!, _overPanel = null!;
        private Label _startSpec = null!, _startGreet = null!, _startSeed = null!, _startBest = null!;
        private Label _rRun = null!, _rSpec = null!, _rWave = null!, _rKills = null!,
                      _rArchetype = null!, _rCause = null!, _rWeak = null!, _rRemark = null!, _rSource = null!;
        private ColorRect _adaptFill = null!;

        // taunt typewriter
        private string _tauntFull = "";
        private int _tauntShown;
        private float _tauntTick, _tauntHideAt;
        private float _bannerT = -1f;

        // ---------- audio ----------
        private AudioStreamPlayer _bgm = null!;
        private AudioStreamPlayer _bgmBoss = null!;
        private bool _bossActive;
        private bool _bossMusicOn;
        private Tween? _musicTween;
        private readonly List<AudioStreamPlayer> _sfxPool = new();
        private int _sfxIdx;
        private readonly Dictionary<string, AudioStream> _sfx = new();

        private void InitAudio()
        {
            var bgm = GD.Load<AudioStreamWav>("res://assets/audio/bgm_dark_aria.wav");
            if (bgm != null)
            {
                bgm.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                bgm.LoopBegin = 0;
                bgm.LoopEnd = (int)(bgm.GetLength() * bgm.MixRate);
                _bgm = new AudioStreamPlayer { Stream = bgm, VolumeDb = -9f };
                AddChild(_bgm);
            }
            var boss = GD.Load<AudioStreamWav>("res://assets/audio/bgm_boss.wav");
            if (boss != null)
            {
                boss.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                _bgmBoss = new AudioStreamPlayer { Stream = boss, VolumeDb = -80f };
                AddChild(_bgmBoss);
            }
            foreach (var n in new[] { "shoot", "hit", "melee", "purge", "hurt", "wave", "over", "taunt", "boss", "waterbolt", "splash", "pistol", "shotgun", "reload", "switch" })
            {
                var s = GD.Load<AudioStream>($"res://assets/audio/{n}.wav");
                if (s != null) _sfx[n] = s;
            }
            for (int i = 0; i < 10; i++)
            {
                var p = new AudioStreamPlayer { VolumeDb = -4f };
                AddChild(p);
                _sfxPool.Add(p);
            }
        }

        public void PlaySfx(string name)
        {
            if (!_sfx.TryGetValue(name, out var s)) return;
            var p = _sfxPool[_sfxIdx++ % _sfxPool.Count];
            p.Stream = s;
            p.Play();
        }

        // input
        private readonly Dictionary<long, (Vector2 origin, Vector2 cur, bool aim)> _touches = new();
        private bool _mouseDown;
        private Vector2 _mousePos;

        private static readonly Color TermGreen = new("#9ef0b3");
        private static readonly Color TermDim = new("#4e8a63");
        private static readonly Color AlertRed = new("#ff4d5e");
        private const float CamShakeDecay = 6f;

        // ==================================================================
        // READY
        // ==================================================================
        public override void _Ready()
        {
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--theme=") &&
                    Enum.TryParse(arg.Substring(8), true, out ThemeBucket b))
                    _bucket = b;
                if (arg.StartsWith("--key="))
                    Config.GeminiApiKey = arg.Substring(6).Trim();
                if (arg == "--screenshot")
                    _screenshotMode = true;
                if (arg == "--menushot")
                    _menuShotMode = true;
                if (arg == "--forceboss")
                    _forceBoss = true;
                if (arg.StartsWith("--cam="))
                    _camMode = arg.Substring(6).ToLowerInvariant() switch
                    {
                        "tpp" => CamMode.Tpp,
                        "fpp" => CamMode.Fpp,
                        _ => CamMode.Top,
                    };
            }
            _theme = Config.Themes[_bucket];
            _seedLabel = $"SEED: {_theme.Label}";

            _autoFire = _screenshotMode;
            _profile = SpecimenProfile.Load();
            BuildWorld();
            BuildArena();
            BuildPlayer();
            BuildUi();
            InitAudio();
            ShowStart();
            if (_screenshotMode)
            {
                StartRun();
                _shotAt = 4.0f;
            }
            else if (_menuShotMode)
            {
                _shotAt = 1.6f;
            }
        }

        // ==================================================================
        // WORLD / ARENA
        // ==================================================================
        private void BuildWorld()
        {
            var env = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = _theme.Bg,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = _theme.Floor.Lightened(0.45f),
                AmbientLightEnergy = 1.05f,
                TonemapMode = Godot.Environment.ToneMapper.Aces,
                GlowEnabled = true,
                GlowIntensity = 0.6f,
                GlowBloom = 0.1f,
                FogEnabled = true,
                FogLightColor = _theme.Fog with { A = 1f },
                FogDensity = 0.018f,
                SsaoEnabled = true,
                SsaoRadius = 1.6f,
            };
            _worldEnv = new WorldEnvironment { Environment = env };
            AddChild(_worldEnv);

            _sun = new DirectionalLight3D
            {
                RotationDegrees = new Vector3(-52f, -32f, 0f),
                LightColor = _theme.Accent.Lightened(0.55f),
                LightEnergy = 1.3f,
                ShadowEnabled = true,
                DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
                DirectionalShadowMaxDistance = 70f,
            };
            AddChild(_sun);

            _cam = new Camera3D
            {
                Position = new Vector3(0f, 25f, 15.5f),
                RotationDegrees = new Vector3(-58f, 0f, 0f),
                Projection = Camera3D.ProjectionType.Perspective,
                Fov = 48f,
                Current = true,
            };
            AddChild(_cam);

            _enemyRoot = new Node3D { Name = "Enemies" };
            _fxRoot = new Node3D { Name = "FX" };
            AddChild(_enemyRoot);
            AddChild(_fxRoot);

            _muzzleLight = new OmniLight3D
            {
                LightColor = TermGreen,
                LightEnergy = 0f,
                OmniRange = 6f,
                Position = new Vector3(0, 1.4f, 0),
            };
            AddChild(_muzzleLight);

            // purge ring (hidden until used)
            var ringMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(0.62f, 0.94f, 0.7f, 0.75f),
                EmissionEnabled = true,
                Emission = TermGreen,
                EmissionEnergyMultiplier = 2.4f,
            };
            _purgeRing = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.92f, OuterRadius = 1f, Rings = 48, RingSegments = 8 },
                MaterialOverride = ringMat,
                Visible = false,
                Position = new Vector3(0, 0.15f, 0),
                RotationDegrees = new Vector3(90f, 0f, 0f),
            };
            AddChild(_purgeRing);
        }

        private static StandardMaterial3D Mat(Color c, float rough = 0.92f, float metal = 0.02f) =>
            new() { AlbedoColor = c, Roughness = rough, Metallic = metal };

        private static StandardMaterial3D PbrMat(Color tint, string texBase)
        {
            var m = Mat(tint, 0.95f, 0.02f);
            string baseDir = "res://assets/textures/";
            if (ResourceLoader.Exists($"{baseDir}{texBase}_diff_1k.jpg"))
                m.AlbedoTexture = GD.Load<Texture2D>($"{baseDir}{texBase}_diff_1k.jpg");
            if (ResourceLoader.Exists($"{baseDir}{texBase}_nor_gl_1k.jpg"))
            {
                m.NormalEnabled = true;
                m.NormalTexture = GD.Load<Texture2D>($"{baseDir}{texBase}_nor_gl_1k.jpg");
                m.NormalScale = 0.8f;
            }
            if (ResourceLoader.Exists($"{baseDir}{texBase}_rough_1k.jpg"))
            {
                m.RoughnessTexture = GD.Load<Texture2D>($"{baseDir}{texBase}_rough_1k.jpg");
                m.Roughness = 1.0f;
            }
            m.Uv1Scale = new Vector3(10f, 7f, 1f);
            return m;
        }

        private void BuildArena()
        {
            // floor — PolyHaven CC0 PBR concrete, tinted per theme
            var floor = new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(Config.WorldW + 6, Config.WorldH + 6) },
                MaterialOverride = PbrMat(_theme.Floor.Lightened(0.55f), "brushed_concrete"),
            };
            AddChild(floor);

            // grid lines via thin emissive strips (cheap, moody)
            var gridMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = _theme.Grid,
                EmissionEnabled = true,
                Emission = _theme.Grid,
                EmissionEnergyMultiplier = 0.7f,
            };
            for (float gx = -Config.WorldW / 2; gx <= Config.WorldW / 2; gx += 4f)
            {
                var line = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.04f, 0.02f, Config.WorldH) },
                    MaterialOverride = gridMat,
                    Position = new Vector3(gx, 0.012f, 0),
                };
                AddChild(line);
            }
            for (float gz = -Config.WorldH / 2; gz <= Config.WorldH / 2; gz += 4f)
            {
                var line = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(Config.WorldW, 0.02f, 0.04f) },
                    MaterialOverride = gridMat,
                    Position = new Vector3(0, 0.012f, gz),
                };
                AddChild(line);
            }

            // perimeter walls — weathered concrete PBR
            var wallMat = PbrMat(_theme.PillarFill.Lightened(0.2f), "chipped_concrete");
            (Vector3 pos, Vector3 size)[] walls =
            {
                (new Vector3(0, 1.5f, -Config.WorldH / 2 - 0.5f), new Vector3(Config.WorldW + 2, 3, 1)),
                (new Vector3(0, 1.5f, Config.WorldH / 2 + 0.5f), new Vector3(Config.WorldW + 2, 3, 1)),
                (new Vector3(-Config.WorldW / 2 - 0.5f, 1.5f, 0), new Vector3(1, 3, Config.WorldH)),
                (new Vector3(Config.WorldW / 2 + 0.5f, 1.5f, 0), new Vector3(1, 3, Config.WorldH)),
            };
            foreach (var (pos, size) in walls)
            {
                var w = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = wallMat, Position = pos };
                AddChild(w);
            }
            // glowing arena edge strips
            var edgeMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = _theme.Accent,
                EmissionEnergyMultiplier = 1.6f,
                AlbedoColor = _theme.Accent,
            };
            foreach (var (pos, size) in walls)
            {
                var strip = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(size.X == 1 ? 0.12f : size.X, 0.14f, size.Z == 0 ? size.Z : (size.Z == 1 ? 0.12f : size.Z)) },
                    MaterialOverride = edgeMat,
                    Position = new Vector3(pos.X, 3.05f, pos.Z),
                };
                AddChild(strip);
            }

            // pillars — KayKit dungeon pillar if imported, else box fallback
            _pillarScene = LoadScene("res://assets/models/pillar.glb");
            foreach (var p in Config.Pillars)
            {
                Node3D node;
                if (_pillarScene != null)
                {
                    node = _pillarScene.Instantiate<Node3D>();
                    node.Scale = Vector3.One * (p.R / 2.1f);
                }
                else
                {
                    node = new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = p.R * 0.8f, BottomRadius = p.R, Height = 4f },
                        MaterialOverride = Mat(_theme.PillarFill),
                    };
                }
                node.Position = new Vector3(p.Pos.X, 0f, p.Pos.Y);
                AddChild(node);
                // accent ring at pillar base
                var ring = new MeshInstance3D
                {
                    Mesh = new TorusMesh { InnerRadius = p.R + 0.12f, OuterRadius = p.R + 0.26f, Rings = 32, RingSegments = 6 },
                    MaterialOverride = edgeMat,
                    Position = new Vector3(p.Pos.X, 0.06f, p.Pos.Y),
                    RotationDegrees = new Vector3(90f, 0f, 0f),
                };
                AddChild(ring);
            }
        }

        private static PackedScene? LoadScene(string path)
        {
            try { return ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null; }
            catch { return null; }
        }

        private static void SetJoy(TextureRect baseR, TextureRect knob, (Vector2 o, Vector2 c, bool active) s, float k)
        {
            baseR.Visible = s.active; knob.Visible = s.active;
            if (!s.active) return;
            baseR.Position = s.o * k - baseR.Size / 2;
            var d = (s.c - s.o) * k;
            if (d.Length() > 46f) d = d.Normalized() * 46f;
            knob.Position = s.o * k - knob.Size / 2 + d * 0.55f;
        }

        private void UpdateEnemyBars()
        {
            float k = 1280f / GetViewport().GetVisibleRect().Size.X;
            int bi = 0;
            Enemy? boss = null;
            if (_phase == Phase.Playing || _phase == Phase.Intermission)
            {
                foreach (var e in _enemies)
                {
                    if (e.Type == EnemyType.Boss) { if (!e.Dead) boss = e; continue; }
                    if (bi >= _hpBars.Count) break;
                    if (e.Dead || e.SpawnT > 0 || e.Hp >= e.MaxHp) continue;
                    var head = new Vector3(e.Pos.X, 2.3f, e.Pos.Y);
                    if (_cam.IsPositionBehind(head)) continue;
                    var sp = _cam.UnprojectPosition(head) * k;
                    var bar = _hpBars[bi];
                    bar.root.Visible = true;
                    bar.root.Position = sp - new Vector2(24, 0);
                    float pct = Mathf.Clamp(e.Hp / e.MaxHp, 0f, 1f);
                    bar.fill.Size = new Vector2(46 * pct, 5);
                    bar.fill.Color = pct > 0.4f ? TermGreen : AlertRed;
                    bi++;
                }
            }
            for (; bi < _hpBars.Count; bi++) _hpBars[bi].root.Visible = false;

            if (boss != null && _bossBar != null)
            {
                _bossBar.Visible = true;
                _bossFill.Size = new Vector2(360f * Mathf.Clamp(boss.Hp / boss.MaxHp, 0f, 1f), 10);
            }
            else if (_bossBar != null) _bossBar.Visible = false;
        }

        // ---- weapon system ----
        private void SwitchWeapon(int i)
        {
            if (i == _weaponIdx || _reloading || i < 0 || i >= Config.Weapons.Length) return;
            var oldN = _weaponNodes.Count > _weaponIdx ? _weaponNodes[_weaponIdx] : null;
            var newN = _weaponNodes.Count > i ? _weaponNodes[i] : null;
            _weaponIdx = i;
            PlaySfx("switch");
            SpawnBurst(new Vector3(_player.Pos.X, 1.2f, _player.Pos.Y), Config.Weapons[i].BulletColor, 14, 4f);
            if (oldN != null)
            {
                var t = CreateTween();
                t.TweenProperty(oldN, "rotation_degrees", new Vector3(-85, 40, 30), 0.16f);
                t.Parallel().TweenProperty(oldN, "scale", Vector3.One * 0.01f, 0.16f);
                t.TweenCallback(Callable.From(() => { if (IsInstanceValid(oldN)) { oldN.Visible = false; oldN.RotationDegrees = Vector3.Zero; oldN.Scale = Vector3.One; } }));
            }
            if (newN != null)
            {
                newN.Visible = true;
                newN.Scale = Vector3.One * 0.01f;
                newN.RotationDegrees = new Vector3(-90, -540, 30);
                var t2 = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                t2.TweenProperty(newN, "scale", Vector3.One, 0.38f);
                t2.Parallel().TweenProperty(newN, "rotation_degrees", Vector3.Zero, 0.38f);
            }
            UpdateWeaponHud();
        }

        private void StartReload()
        {
            if (_reloading) return;
            var w = Config.Weapons[_weaponIdx];
            if (_ammo[_weaponIdx] >= w.Mag) return;
            _reloading = true;
            PlaySfx("reload");
            var n = _weaponNodes.Count > _weaponIdx ? _weaponNodes[_weaponIdx] : null;
            if (n != null)
            {
                var t = CreateTween();
                t.TweenProperty(n, "rotation_degrees", new Vector3(-42, 0, 14), 0.22f);
                t.TweenProperty(n, "rotation_degrees", Vector3.Zero, 0.32f).SetDelay(Mathf.Max(0.05f, w.ReloadTime - 0.32f));
            }
            UpdateWeaponHud();
            GetTree().CreateTimer(w.ReloadTime).Timeout += () =>
            {
                _ammo[_weaponIdx] = w.Mag;
                _reloading = false;
                UpdateWeaponHud();
            };
        }

        private void UpdateWeaponHud()
        {
            for (int i = 0; i < _weaponBtns.Count; i++)
                _weaponBtns[i].Modulate = i == _weaponIdx ? new Color(1f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f);
            var w = Config.Weapons[_weaponIdx];
            _ammoLabel.Text = _reloading ? "RELOADING…" : $"{_ammo[_weaponIdx]} / {w.Mag}";
        }

        // ---- Custom character override: drop YOUR .glb into res://assets/custom/ ----
        private static readonly string[] CustomNames =
            { "player", "zombie_standard", "zombie_runner", "zombie_brute" };

        private static PackedScene? LoadCharacter(string builtinPath, string customName)
        {
            // rigged variant wins > plain custom > builtin
            foreach (var path in new[]
            {
                $"res://assets/custom/{customName}_rigged.glb",
                $"res://assets/custom/{customName}.glb",
            })
            {
                if (ResourceLoader.Exists(path))
                {
                    var s = LoadScene(path);
                    if (s != null) return s;
                }
            }
            return LoadScene(builtinPath);
        }

        // ---- runtime skeleton puppetry (walk cycles for auto-rigged models) ----
        private class RigData
        {
            public Skeleton3D Skel = null!;
            public int ThighL = -1, ThighR = -1, ShinL = -1, ShinR = -1, ArmL = -1, ArmR = -1, Spine = -1;
        }

        private static RigData? FindRig(Node root)
        {
            var skel = root.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
            if (skel == null) return null;
            var r = new RigData { Skel = skel };
            r.ThighL = skel.FindBone("thigh_l"); r.ThighR = skel.FindBone("thigh_r");
            r.ShinL = skel.FindBone("shin_l"); r.ShinR = skel.FindBone("shin_r");
            r.ArmL = skel.FindBone("upperarm_l"); r.ArmR = skel.FindBone("upperarm_r");
            r.Spine = skel.FindBone("spine");
            return r.ThighL >= 0 ? r : null;
        }

        private static void PoseWalk(RigData r, float phase, float amount)
        {
            float swing = Mathf.Sin(phase) * amount;
            if (r.ThighL >= 0) r.Skel.SetBonePoseRotation(r.ThighL, new Quaternion(Vector3.Right, swing));
            if (r.ThighR >= 0) r.Skel.SetBonePoseRotation(r.ThighR, new Quaternion(Vector3.Right, -swing));
            if (r.ShinL >= 0) r.Skel.SetBonePoseRotation(r.ShinL, new Quaternion(Vector3.Right, Mathf.Max(0, -swing) * 0.9f));
            if (r.ShinR >= 0) r.Skel.SetBonePoseRotation(r.ShinR, new Quaternion(Vector3.Right, Mathf.Max(0, swing) * 0.9f));
            if (r.ArmL >= 0) r.Skel.SetBonePoseRotation(r.ArmL, new Quaternion(Vector3.Right, -swing * 0.55f));
            if (r.ArmR >= 0) r.Skel.SetBonePoseRotation(r.ArmR, new Quaternion(Vector3.Right, swing * 0.55f));
            if (r.Spine >= 0) r.Skel.SetBonePoseRotation(r.Spine, new Quaternion(Vector3.Forward, Mathf.Sin(phase * 2f) * 0.06f * amount));
        }

        private static Aabb ComputeModelAabb(Node node, Transform3D accum, ref bool first, ref Aabb box)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var a = accum * mi.GetAabb();
                box = first ? a : box.Merge(a);
                first = false;
            }
            foreach (var c in node.GetChildren())
                if (c is Node3D n3) ComputeModelAabb(n3, accum * n3.Transform, ref first, ref box);
            return box;
        }

        /// <summary>Centers any GLB and scales its longest axis to targetLen (for weapons).</summary>
        private static Node3D NormalizeLength(Node3D model, float targetLen)
        {
            var box = new Aabb();
            bool first = true;
            ComputeModelAabb(model, Transform3D.Identity, ref first, ref box);
            if (first) return model;
            float maxDim = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
            if (maxDim < 0.0001f) return model;
            model.Position = new Vector3(
                -(box.Position.X + box.Size.X * 0.5f),
                -(box.Position.Y + box.Size.Y * 0.5f),
                -(box.Position.Z + box.Size.Z * 0.5f));
            var outer = new Node3D { Name = model.Name + "_wroot" };
            var inner = new Node3D { Name = model.Name + "_wnorm" };
            inner.AddChild(model);
            inner.Scale = Vector3.One * (targetLen / maxDim);
            outer.AddChild(inner);
            return outer;
        }
        private static Node3D NormalizeModel(Node3D model, float targetHeight)
        {
            var box = new Aabb();
            bool first = true;
            ComputeModelAabb(model, Transform3D.Identity, ref first, ref box);
            if (first || box.Size.Y < 0.0001f) return model;
            float s = targetHeight / box.Size.Y;
            model.Position = new Vector3(
                -(box.Position.X + box.Size.X * 0.5f),
                -box.Position.Y,
                -(box.Position.Z + box.Size.Z * 0.5f));
            var inner = new Node3D { Name = model.Name + "_norm" };
            inner.AddChild(model);
            inner.Scale = Vector3.One * s;
            var outer = new Node3D { Name = model.Name + "_root" };
            outer.AddChild(inner);
            return outer;
        }

        // ==================================================================
        // PLAYER
        // ==================================================================
        private void BuildPlayer()
        {
            var scene = LoadCharacter("res://assets/models/soldier.glb", "player");
            if (scene != null)
            {
                _playerNode = NormalizeModel(scene.Instantiate<Node3D>(), 1.8f);
                _playerAnim = FindAnim(_playerNode);
                _playerRig = FindRig(_playerNode);

                // weapon slots — mounted on the hand BONE so the grip stays in his fist
                Node3D weaponParent = _playerNode;
                var playerSkel = _playerNode.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
                if (playerSkel != null && playerSkel.FindBone("forearm_r") >= 0)
                {
                    var ba = new BoneAttachment3D { BoneName = "forearm_r" };
                    playerSkel.AddChild(ba);
                    weaponParent = ba;
                }
                _weaponBoneSpace = weaponParent != _playerNode;
                var customWeapon = LoadScene("res://assets/custom/weapon.glb");
                for (int i = 0; i < Config.Weapons.Length; i++)
                {
                    Node3D? wn = null;
                    if (customWeapon != null)
                        wn = NormalizeLength(customWeapon.Instantiate<Node3D>(), 0.95f);
                    else
                    {
                        var ws = LoadScene(Config.Weapons[i].Model);
                        if (ws != null) wn = ws.Instantiate<Node3D>();
                    }
                    if (wn != null)
                    {
                        if (_weaponBoneSpace)
                        {
                            wn.Position = new Vector3(0.02f, -0.16f, 0.05f);
                            wn.RotationDegrees = new Vector3(-90f, 0f, 0f);
                            wn.Scale = Vector3.One * 0.45f;
                        }
                        else
                        {
                            wn.Position = new Vector3(0.28f, 1.0f, 0.22f);
                        }
                        wn.Visible = i == _weaponIdx;
                        weaponParent.AddChild(wn);
                    }
                    _weaponNodes.Add(wn);
                }
                _rifle = _weaponNodes.Count > _weaponIdx ? _weaponNodes[_weaponIdx] : null;
            }
            else
            {
                _playerNode = new Node3D();
                var body = new MeshInstance3D
                {
                    Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.5f },
                    MaterialOverride = Mat(new Color("#e8fff0"), 0.6f),
                    Position = new Vector3(0, 0.75f, 0),
                };
                _playerNode.AddChild(body);
            }
            AddChild(_playerNode);
        }

        private static AnimationPlayer? FindAnim(Node root)
        {
            var ap = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
            return ap;
        }

        private static void PlayAnim(AnimationPlayer? ap, string want, float speed = 1f)
        {
            if (ap == null) return;
            string[] names = ap.GetAnimationList();
            string? found = names.FirstOrDefault(n => n.Contains(want, StringComparison.OrdinalIgnoreCase));
            if (found == null) return;
            var anim = ap.GetAnimation(found);
            bool loop = want.Contains("walk", StringComparison.OrdinalIgnoreCase)
                     || want.Contains("idle", StringComparison.OrdinalIgnoreCase)
                     || want.Contains("run", StringComparison.OrdinalIgnoreCase);
            if (anim != null) anim.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
            if (ap.CurrentAnimation.ToString() != found || !ap.IsPlaying()) ap.Play(found);
            ap.SpeedScale = speed;
        }

        // ==================================================================
        // UI (all code-built Controls on a CanvasLayer)
        // ==================================================================
        private Label MkLabel(Control parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color,
            HorizontalAlignment align = HorizontalAlignment.Left)
        {
            var l = new Label
            {
                Text = text,
                Position = pos,
                Size = size,
                HorizontalAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            l.AddThemeFontSizeOverride("font_size", fontSize);
            l.AddThemeColorOverride("font_color", color);
            parent.AddChild(l);
            return l;
        }

        private static StyleBoxFlat PanelStyle(Color bg, Color border)
        {
            var s = new StyleBoxFlat { BgColor = bg, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12 };
            s.SetBorderWidthAll(1);
            s.BorderColor = border;
            s.CornerRadiusTopLeft = s.CornerRadiusTopRight = s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 4;
            return s;
        }

        private void BuildUi()
        {
            _ui = new CanvasLayer();
            AddChild(_ui);

            // HP
            var hpBg = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), Position = new Vector2(24, 44), Size = new Vector2(300, 16) };
            _ui.AddChild(hpBg);
            _hpFill = new ColorRect { Color = TermGreen, Position = new Vector2(26, 46), Size = new Vector2(296, 12) };
            _ui.AddChild(_hpFill);
            MkLabel(UIR(), "SPECIMEN VITALS", new Vector2(24, 20), new Vector2(300, 20), 13, TermDim);

            // Wave / theme / score / badge
            _waveLabel = MkLabel(UIR(), "WAVE 01", new Vector2(490, 18), new Vector2(300, 40), 34, TermGreen, HorizontalAlignment.Center);
            _themeTag = MkLabel(UIR(), _theme.Label, new Vector2(440, 58), new Vector2(400, 20), 13, TermDim, HorizontalAlignment.Center);
            _scoreLabel = MkLabel(UIR(), "0", new Vector2(1030, 20), new Vector2(226, 34), 28, TermGreen, HorizontalAlignment.Right);
            _brainBadge = MkLabel(UIR(), "PZ-CORE // LOCAL", new Vector2(1030, 54), new Vector2(226, 20), 13, TermDim, HorizontalAlignment.Right);

            // Wave banner (center flash)
            _waveBanner = MkLabel(UIR(), "", new Vector2(340, 300), new Vector2(600, 90), 64, TermGreen, HorizontalAlignment.Center);
            _waveBanner.Visible = false;

            // Taunt panel
            _tauntPanel = new PanelContainer
            {
                Position = new Vector2(320, 560),
                Size = new Vector2(640, 110),
                Visible = false,
            };
            _tauntPanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.012f, 0.04f, 0.024f, 0.88f), new Color(0.18f, 1f, 0.53f, 0.4f)));
            var tv = new VBoxContainer();
            var who = new Label { Text = "▚ PATIENT ZERO // TRANSMISSION" };
            who.AddThemeFontSizeOverride("font_size", 11);
            who.AddThemeColorOverride("font_color", AlertRed);
            tv.AddChild(who);
            _tauntLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _tauntLabel.AddThemeFontSizeOverride("font_size", 19);
            _tauntLabel.AddThemeColorOverride("font_color", TermGreen);
            tv.AddChild(_tauntLabel);
            _reasonLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _reasonLabel.AddThemeFontSizeOverride("font_size", 12);
            _reasonLabel.AddThemeColorOverride("font_color", TermDim);
            tv.AddChild(_reasonLabel);
            _tauntPanel.AddChild(tv);
            _ui.AddChild(_tauntPanel);

            // Purge button
            _purgeBtn = new Button { Text = "PURGE [SPACE]", Position = new Vector2(1090, 596), Size = new Vector2(166, 66) };
            
            _purgeBtn.AddThemeFontSizeOverride("font_size", 16);
            _purgeBtn.Pressed += () => { if (_phase == Phase.Playing) TryPurge(); };
            StyleBtn(_purgeBtn);
            _ui.AddChild(_purgeBtn);

            // FIRE button (hold to shoot — touch-friendly)
            _fireBtn = new Button { Text = "🔥 FIRE", Position = new Vector2(1066, 476), Size = new Vector2(190, 100) };
            _fireBtn.AddThemeFontSizeOverride("font_size", 22);
            _fireBtn.ButtonDown += () => _fireHeld = true;
            _fireBtn.ButtonUp += () => _fireHeld = false;
            StyleBtn(_fireBtn, 0.7f);
            _ui.AddChild(_fireBtn);

            // weapon switcher (top-center-left) + ammo counter
            var wbar = new HBoxContainer { Position = new Vector2(24, 68) };
            wbar.AddThemeConstantOverride("separation", 8);
            string[] shortNames = { "1 · PISTOL", "2 · AR", "3 · SCATTER" };
            for (int i = 0; i < 3; i++)
            {
                var b = new Button { Text = shortNames[i], CustomMinimumSize = new Vector2(118, 40) };
                b.AddThemeFontSizeOverride("font_size", 13);
                int idx = i;
                b.Pressed += () => SwitchWeapon(idx);
                _weaponBtns.Add(b);
                wbar.AddChild(b);
            }
            _ui.AddChild(wbar);
            _ammoLabel = MkLabel(UIR(), "30 / 30", new Vector2(1030, 84), new Vector2(226, 30), 22, TermGreen, HorizontalAlignment.Right);

            // ---- UI background upgrade: vignette + scanlines + corner brackets + HUD backdrops ----
            var vignette = new TextureRect
            {
                Texture = MakeVignetteTex(),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                Size = new Vector2(1280, 720),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            UIR().AddChild(vignette);
            UIR().MoveChild(vignette, 0); // behind HUD labels

            var scanImg = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            for (int sy = 0; sy < 4; sy++)
            for (int sx = 0; sx < 4; sx++)
                scanImg.SetPixel(sx, sy, sy == 0 ? new Color(0, 0, 0, 0.13f) : new Color(0, 0, 0, 0));
            var scan = new TextureRect
            {
                Texture = ImageTexture.CreateFromImage(scanImg),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Tile,
                Size = new Vector2(1280, 720),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, 0.55f),
            };
            UIR().AddChild(scan);
            UIR().MoveChild(scan, 1);

            var bracket = new Color(0.62f, 0.94f, 0.7f, 0.35f);
            void Bracket(float x, float y, bool flipX, bool flipY)
            {
                var h = new ColorRect { Color = bracket, Position = new Vector2(x, y), Size = new Vector2(46, 3) };
                var v = new ColorRect { Color = bracket, Position = new Vector2(x, y), Size = new Vector2(3, 46) };
                if (flipX) h.Position = new Vector2(x - 43, y);
                if (flipY) v.Position = new Vector2(x, y - 43);
                UIR().AddChild(h); UIR().AddChild(v);
            }
            Bracket(16, 14, false, false); Bracket(1264, 14, true, false);
            Bracket(16, 706, false, true); Bracket(1264, 706, true, true);

            // HUD backdrops
            void Backdrop(float x, float y, float w, float h)
            {
                var p = new PanelContainer { Position = new Vector2(x, y), Size = new Vector2(w, h), MouseFilter = Control.MouseFilterEnum.Ignore };
                p.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.02f, 0.03f, 0.02f, 0.5f), new Color(0.3f, 0.55f, 0.4f, 0.3f)));
                UIR().AddChild(p);
                UIR().MoveChild(p, 2);
            }
            Backdrop(12, 12, 324, 104);   // vitals + weapons
            Backdrop(1008, 12, 260, 108); // score + badge + ammo

            // enemy hp bar pool + boss bar + player hp number
            for (int i = 0; i < 32; i++)
            {
                var barRoot = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(48, 7) };
                var bgc = new ColorRect { Color = new Color(0, 0, 0, 0.62f), Size = new Vector2(48, 7) };
                var fill = new ColorRect { Color = TermGreen, Position = new Vector2(1, 1), Size = new Vector2(46, 5) };
                barRoot.AddChild(bgc); barRoot.AddChild(fill);
                _ui.AddChild(barRoot);
                _hpBars.Add((barRoot, fill));
            }
            _bossBar = new PanelContainer { Position = new Vector2(340, 100), Size = new Vector2(600, 30), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            _bossBar.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.05f, 0.01f, 0.02f, 0.75f), new Color(1f, 0.3f, 0.37f, 0.6f)));
            var bossRow = new HBoxContainer();
            var bossLbl = new Label { Text = "▚ PATIENT ZERO // AVATAR INTEGRITY  " };
            bossLbl.AddThemeFontSizeOverride("font_size", 12);
            bossLbl.AddThemeColorOverride("font_color", AlertRed);
            bossRow.AddChild(bossLbl);
            var bossBg = new ColorRect { Color = new Color(0, 0, 0, 0.6f), CustomMinimumSize = new Vector2(360, 10) };
            _bossFill = new ColorRect { Color = AlertRed, Size = new Vector2(360, 10) };
            var bossWrap = new Control { CustomMinimumSize = new Vector2(360, 10) };
            bossWrap.AddChild(bossBg);
            bossBg.AddChild(_bossFill);
            bossRow.AddChild(bossWrap);
            _bossBar.AddChild(bossRow);
            _ui.AddChild(_bossBar);
            _hpNum = MkLabel(UIR(), "100 / 100", new Vector2(24, 46), new Vector2(200, 18), 12, TermDim);

            // virtual joysticks (mobile)
            _ringTex = MakeRingTex(140, 0.36f, 0.5f, new Color(0.62f, 0.94f, 0.7f, 0.4f));
            _discTex = MakeDiscTex(64, new Color(0.62f, 0.94f, 0.7f, 0.55f));
            _joyBaseL = new TextureRect { Texture = _ringTex, Size = new Vector2(140, 140), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            _joyKnobL = new TextureRect { Texture = _discTex, Size = new Vector2(64, 64), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            _joyBaseR = new TextureRect { Texture = _ringTex, Size = new Vector2(140, 140), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            _joyKnobR = new TextureRect { Texture = _discTex, Size = new Vector2(64, 64), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            _ui.AddChild(_joyBaseL); _ui.AddChild(_joyKnobL); _ui.AddChild(_joyBaseR); _ui.AddChild(_joyKnobR);

            // Camera mode button
            var camBtn = new Button { Text = "CAM: TOP [C]", Position = new Vector2(24, 596), Size = new Vector2(180, 60) };
            camBtn.AddThemeFontSizeOverride("font_size", 15);
            camBtn.Pressed += CycleCamMode;
            StyleBtn(camBtn);
            _ui.AddChild(camBtn);
            _camBtn = camBtn;

            // Crosshair (TPP/FPP) — small 4-bar reticle, font-independent
            _crosshair = new Control { Position = new Vector2(640, 352), MouseFilter = Control.MouseFilterEnum.Ignore };
            foreach (var (p, s) in new[] { (new Vector2(-1.5f, -12), new Vector2(3, 9)), (new Vector2(-1.5f, 3), new Vector2(3, 9)), (new Vector2(-12, -1.5f), new Vector2(9, 3)), (new Vector2(3, -1.5f), new Vector2(9, 3)) })
            {
                var bar = new ColorRect { Color = new Color(0.62f, 0.94f, 0.7f, 0.9f), Position = p, Size = s };
                _crosshair.AddChild(bar);
            }
            _ui.AddChild(_crosshair);
            _crosshair.Visible = false;

            BuildStartPanel();
            BuildOverPanel();
        }

        private Control UIR()
        {
            if (_uiRootControl == null)
            {
                _uiRootControl = new Control { Size = new Vector2(1280, 720), MouseFilter = Control.MouseFilterEnum.Ignore };
                _ui.AddChild(_uiRootControl);
            }
            return _uiRootControl;
        }
        private Control? _uiRootControl;

        private void BuildStartPanel()
        {
            var bg = new ColorRect { Color = new Color(0.02f, 0.025f, 0.02f, 0.94f), Size = new Vector2(1280, 720) };
            var panel = new Control { Name = "StartPanel" };
            panel.AddChild(bg);

            // rotating reactor ring behind the title
            _menuRing = new TextureRect
            {
                Texture = MakeRingTex(480, 0.42f, 0.47f, new Color(0.16f, 1f, 0.5f, 0.3f)),
                Size = new Vector2(480, 480),
                Position = new Vector2(400, 60),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                PivotOffset = new Vector2(240, 240),
            };
            panel.AddChild(_menuRing);
            _titleGlow = MkLabel(panel, "PATIENT ZERO", new Vector2(142, 122), new Vector2(1000, 80), 72, new Color(0.16f, 1f, 0.5f, 0.4f), HorizontalAlignment.Center);
            var title = MkLabel(panel, "PATIENT ZERO", new Vector2(140, 120), new Vector2(1000, 80), 72, TermGreen, HorizontalAlignment.Center);
            _menuAccent = new ColorRect { Color = new Color(0.16f, 1f, 0.5f, 0.6f), Position = new Vector2(540, 214), Size = new Vector2(200, 2) };
            panel.AddChild(_menuAccent);
            MkLabel(panel, "P R O T O C O L", new Vector2(440, 200), new Vector2(400, 30), 20, TermDim, HorizontalAlignment.Center);
            _startSpec = MkLabel(panel, "", new Vector2(440, 260), new Vector2(400, 30), 22, AlertRed, HorizontalAlignment.Center);
            _startGreet = MkLabel(panel, "", new Vector2(290, 310), new Vector2(700, 70), 20, TermGreen, HorizontalAlignment.Center);
            _startSeed = MkLabel(panel, "", new Vector2(340, 390), new Vector2(600, 24), 15, TermDim, HorizontalAlignment.Center);
            _startBest = MkLabel(panel, "", new Vector2(340, 420), new Vector2(600, 24), 14, TermDim, HorizontalAlignment.Center);

            var btn = new Button { Text = "BEGIN EXPOSURE", Position = new Vector2(515, 480), Size = new Vector2(250, 64) };
            btn.AddThemeFontSizeOverride("font_size", 20);
            btn.Pressed += StartRun;
            panel.AddChild(btn);

            MkLabel(panel, "WASD / LEFT THUMB — MOVE · MOUSE / RIGHT THUMB — AIM+FIRE · SPACE — PURGE\nIT STUDIES YOU. IT REMEMBERS YOU. IT REPORTS YOU.",
                new Vector2(290, 580), new Vector2(700, 60), 13, TermDim, HorizontalAlignment.Center);

            _ui.AddChild(panel);
            _startPanel = panel;
        }

        private void BuildOverPanel()
        {
            var panel = new Control { Name = "OverPanel", Visible = false };
            var bg = new ColorRect { Color = new Color(0.02f, 0.02f, 0.025f, 0.94f), Size = new Vector2(1280, 720) };
            panel.AddChild(bg);

            MkLabel(panel, "SPECIMEN TERMINATED", new Vector2(190, 60), new Vector2(900, 50), 40, AlertRed, HorizontalAlignment.Center);

            var card = new PanelContainer { Position = new Vector2(340, 130), Size = new Vector2(600, 420) };
            card.AddThemeStyleboxOverride("panel", PanelStyle(new Color(0.012f, 0.03f, 0.02f, 0.92f), new Color(1f, 0.3f, 0.37f, 0.5f)));
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 6);

            _rRun = MkLabel(v, "", Vector2.Zero, new Vector2(560, 24), 16, TermDim);
            _rSpec = MkLabel(v, "", Vector2.Zero, new Vector2(560, 24), 16, TermGreen);
            _rWave = MkLabel(v, "", Vector2.Zero, new Vector2(560, 24), 16, TermGreen);
            _rKills = MkLabel(v, "", Vector2.Zero, new Vector2(560, 24), 16, TermGreen);
            _rArchetype = MkLabel(v, "", Vector2.Zero, new Vector2(560, 28), 22, AlertRed);
            _rCause = MkLabel(v, "", Vector2.Zero, new Vector2(560, 40), 14, TermGreen);
            _rWeak = MkLabel(v, "", Vector2.Zero, new Vector2(560, 24), 14, TermGreen);
            var adaptRow = MkLabel(v, "", Vector2.Zero, new Vector2(560, 24), 14, TermDim);
            _adaptFill = new ColorRect { Color = TermGreen, Position = new Vector2(0, 0), Size = new Vector2(0, 10), CustomMinimumSize = new Vector2(0, 10) };
            v.AddChild(_adaptFill);
            _rRemark = MkLabel(v, "", Vector2.Zero, new Vector2(560, 50), 16, TermGreen);
            _rSource = MkLabel(v, "", Vector2.Zero, new Vector2(560, 20), 11, TermDim);
            card.AddChild(v);
            panel.AddChild(card);

            var share = new Button { Text = "SHARE REPORT", Position = new Vector2(400, 580), Size = new Vector2(230, 58) };
            share.Pressed += ShareReport;
            panel.AddChild(share);
            var restart = new Button { Text = "RUN AGAIN", Position = new Vector2(660, 580), Size = new Vector2(230, 58) };
            restart.Pressed += () => GetTree().ReloadCurrentScene();
            panel.AddChild(restart);
            MkLabel(panel, "IT REMEMBERS YOU NEXT TIME.", new Vector2(440, 660), new Vector2(400, 24), 13, TermDim, HorizontalAlignment.Center);

            _ui.AddChild(panel);
            _overPanel = panel;
        }

        // ==================================================================
        // GAME FLOW
        // ==================================================================
        private void ShowStart()
        {
            _startSpec.Text = $"SPECIMEN #{_profile.SpecimenNumber:000}";
            _startGreet.Text = PatientZeroBrain.Greeting(_profile);
            _startSeed.Text = _seedLabel;
            _startBest.Text = _profile.BestWave > 0 ? $"BEST: WAVE {_profile.BestWave} · {_profile.BestScore} PTS" : "NO PRIOR DATA ON FILE";
            _startPanel.Visible = true;
            _overPanel.Visible = false;
        }

        private void StartRun()
        {
            if (_phase == Phase.Playing) return;
            _phase = Phase.Playing;
            _startPanel.Visible = false;
            _time = 0; _wave = 0; _score = 0; _kills = 0;
            _history.Clear();
            foreach (var kv in _enemyNodes) kv.Value.QueueFree();
            _enemyNodes.Clear(); _enemyAnims.Clear(); _enemyRigs.Clear();
            _enemies.Clear(); _projectiles.Clear(); _particles.Clear(); _spawnQueue.Clear();
            foreach (var kv in _boltNodes) kv.Value.QueueFree();
            _boltNodes.Clear(); _bolts.Clear();
            foreach (var d in _decals) if (IsInstanceValid(d)) d.QueueFree();
            _decals.Clear(); _bloodMats.Clear();
            foreach (var b in _bulletNodes) b.node.QueueFree();
            _bulletNodes.Clear();
            _player.Pos = new Vector2(0, 6);
            _player.Hp = Config.PlayerMaxHp;
            _scoreLabel.Text = "0";
            UpdateHp();
            ApplyCamVisuals();
            if (_bgm != null && !_bgm.Playing) _bgm.Play();
            if (_bgmBoss != null && !_bgmBoss.Playing) _bgmBoss.Play();
            _weaponIdx = 1;
            for (int i = 0; i < _ammo.Length && i < Config.Weapons.Length; i++) _ammo[i] = Config.Weapons[i].Mag;
            _reloading = false;
            for (int i = 0; i < _weaponNodes.Count; i++) if (_weaponNodes[i] != null) _weaponNodes[i]!.Visible = i == _weaponIdx;
            UpdateWeaponHud();
            _mouseLookActive = false;
            StartWave(1, Config.Wave1Bias(_bucket), ZoneName.Balanced);
        }

        private void StartWave(int n, WaveComp comp, ZoneName zone)
        {
            _wave = n;
            _waveLabel.Text = $"WAVE {n:00}";
            _waveBanner.Text = $"WAVE {n:00}";
            _waveBanner.Visible = true;
            _bannerT = 0;
            PlaySfx("wave");

            var types = new List<EnemyType>();
            for (int i = 0; i < comp.Standard; i++) types.Add(EnemyType.Standard);
            for (int i = 0; i < comp.Fast; i++) types.Add(EnemyType.Fast);
            for (int i = 0; i < comp.Tanky; i++) types.Add(EnemyType.Tanky);
            for (int i = types.Count - 1; i > 0; i--)
            {
                int j = (int)(GD.Randi() % (i + 1));
                (types[i], types[j]) = (types[j], types[i]);
            }
            var zones = Config.ZoneSpawns.Keys.ToList();
            for (int i = 0; i < types.Count; i++)
            {
                bool useBias = zone != ZoneName.Balanced && Config.ZoneSpawns.ContainsKey(zone) && GD.Randf() < 0.6f;
                var z = useBias ? zone : zones[(int)(GD.Randi() % zones.Count)];
                var pts = Config.ZoneSpawns[z];
                var basePt = pts[(int)(GD.Randi() % pts.Length)];
                _spawnQueue.Add((_time + i * Config.SpawnStagger, types[i],
                    basePt + new Vector2((float)GD.RandRange(-1.5, 1.5), (float)GD.RandRange(-1.5, 1.5))));
            }
            _logger.Reset();

            // PATIENT ZERO MANIFESTS — boss avatar every 3rd wave
            if (_forceBoss || n % 3 == 0)
            {
                float lastT = _spawnQueue.Count > 0 ? _spawnQueue[^1].at : _time;
                _spawnQueue.Add((lastT + 1.2f, EnemyType.Boss, new Vector2(0, -10)));
                _waveBanner.Text = $"WAVE {n:00} — IT MANIFESTS";
                ShowTaunt("Enough. I will attend to this specimen personally.", "» PATIENT ZERO MANIFESTS — boss engagement");
                PlaySfx("boss");
                _bossActive = true;
            }
        }

        private void EndWave()
        {
            _logger.Stop();
            var summary = _logger.Compile(_wave, _player.Hp / Config.PlayerMaxHp, _bucket);
            _history.Add(summary);
            _phase = Phase.Intermission;
            _intermissionT = 0;
            _decision = null;
            var prev = _history.Count > 1 ? _history[^2] : null;
            int nextWave = _wave + 1;
            _pendingDecision = Task.Run(async () => await PatientZeroBrain.Decide(summary, prev, _profile, nextWave));
        }

        private void OnDecisionReady(AIDecision d)
        {
            _decision = d;
            _brainBadge.Text = d.Source == "gemini" ? "GEMINI // ONLINE" : "PZ-CORE // LOCAL";
            ShowTaunt(d.Taunt, d.Reasoning);
        }

        private void LaunchNextWave()
        {
            var d = _decision;
            _decision = null;
            _phase = Phase.Playing;
            if (d != null) StartWave(_wave + 1, d.Composition, d.Zone);
            else StartWave(_wave + 1, new WaveComp { Standard = 3, Fast = 2, Tanky = 1 }, ZoneName.Balanced);
        }

        // ==================================================================
        // COMBAT
        // ==================================================================
        private void SpawnEnemy(EnemyType type, Vector2 pos)
        {
            var e = Enemy.Create(type, pos);
            // wave power scaling — Patient Zero's army hardens as you survive
            float hpMul = 1f + 0.09f * (_wave - 1);
            float dmgMul = 1f + 0.05f * (_wave - 1);
            float spdMul = Mathf.Min(1f + 0.025f * (_wave - 1), 1.4f);
            e.Hp *= hpMul; e.MaxHp = e.Hp;
            e.Damage *= dmgMul;
            e.Speed *= spdMul;
            e.Id = _nextEnemyId++;
            _enemies.Add(e);

            Node3D node;
            if (!_modelCache.TryGetValue(type, out var scene))
            {
                string customName = type switch
                {
                    EnemyType.Fast => "zombie_runner",
                    EnemyType.Tanky => "zombie_brute",
                    EnemyType.Boss => "patient_zero",
                    _ => "zombie_standard",
                };
                scene = LoadCharacter(ModelPaths[type], customName);
                _modelCache[type] = scene;
            }
            float targetH = type switch
            {
                EnemyType.Fast => 1.6f,
                EnemyType.Tanky => 2.3f,
                EnemyType.Boss => 2.7f,
                _ => 1.85f,
            };
            AnimationPlayer? ap = null;
            if (scene != null)
            {
                node = NormalizeModel(scene.Instantiate<Node3D>(), targetH);
                node.Scale = Vector3.One * ModelScale[type] * 0.05f; // spawn scale-in
                ap = FindAnim(node);
                if (ap != null) PlayAnim(ap, "Walk", type == EnemyType.Fast ? 1.5f : 1f);
                // villain model keeps its original dark materials — no theme tint
            }
            else
            {
                node = new Node3D();
                var mesh = new MeshInstance3D
                {
                    Mesh = new CapsuleMesh { Radius = Config.Enemies[type].radius * 0.55f, Height = Config.Enemies[type].radius * 2.2f },
                    MaterialOverride = Mat(Config.EnemyTint(_theme, type), 0.85f),
                    Position = new Vector3(0, Config.Enemies[type].radius, 0),
                };
                node.AddChild(mesh);
            }
            node.Position = new Vector3(pos.X, 0, pos.Y);
            _enemyRoot.AddChild(node);
            _enemyNodes[e.Id] = node;
            _enemyAnims[e.Id] = ap;
            _enemyRigs[e.Id] = FindRig(node);
            CollectBloodMats(e.Id, node);
        }

        private static void TintModel(Node root, Color tint, float amount)
        {
            foreach (var child in root.GetChildren())
            {
                if (child is MeshInstance3D mi)
                {
                    for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++) { }
                    // tint via material override on first surface (KayKit shares one atlas material)
                    var src = mi.GetActiveMaterial(0) as StandardMaterial3D;
                    if (src != null)
                    {
                        var m = (StandardMaterial3D)src.Duplicate();
                        m.AlbedoColor = src.AlbedoColor.Lerp(tint, amount);
                        mi.SetSurfaceOverrideMaterial(0, m);
                    }
                }
                TintModel(child, tint, amount);
            }
        }

        private void KillEnemy(Enemy e, string kind)
        {
            if (e.Dead) return;
            e.Dead = true;
            float dist = _player.Pos.DistanceTo(e.Pos);
            _logger.TrackKill(kind, dist);
            _kills++;
            PlaySfx("hit");
            _score += Config.Enemies[e.Type].score;
            _scoreLabel.Text = _score.ToString();

            if (_enemyNodes.TryGetValue(e.Id, out var node))
            {
                _enemyNodes.Remove(e.Id);
                AnimationPlayer? ap = _enemyAnims.GetValueOrDefault(e.Id);
                _enemyAnims.Remove(e.Id);
                _enemyRigs.Remove(e.Id);
                SpawnBurst(node.Position, BloodRed, e.Type == EnemyType.Boss ? 70 : 30, e.Type == EnemyType.Boss ? 9f : 6.5f);
                BloodDecal(e.Pos, e.Type == EnemyType.Boss ? 2.4f : 1.2f);
                _bloodMats.Remove(e.Id);
                if (e.Type == EnemyType.Boss)
                {
                    ShowTaunt("A temporary avatar. I remain.", "» avatar destroyed — core intelligence unaffected");
                    _shake = 0.8f;
                    _bossActive = false;
                }
                if (ap != null && HasAnim(ap, "Death"))
                {
                    PlayAnim(ap, "Death", 1.1f);
                    var n = node;
                    GetTree().CreateTimer(0.85f).Timeout += () => { if (IsInstanceValid(n)) n.QueueFree(); };
                }
                else
                {
                    var tween = CreateTween();
                    tween.TweenProperty(node, "scale", Vector3.One * 0.001f, 0.22f);
                    tween.TweenCallback(Callable.From(() => node.QueueFree()));
                }
            }
        }

        private static bool HasAnim(AnimationPlayer ap, string want) =>
            ap.GetAnimationList().Any(a => a.Contains(want, StringComparison.OrdinalIgnoreCase));

        private void SpawnBurst(Vector3 pos, Color color, int amount, float speed)
        {
            var p = new CpuParticles3D
            {
                Emitting = true,
                OneShot = true,
                Amount = amount,
                Lifetime = 0.6f,
                Explosiveness = 0.92f,
                Spread = 55f,
                InitialVelocityMin = speed * 0.5f,
                InitialVelocityMax = speed,
                Gravity = new Vector3(0, -6f, 0),
                ScaleAmountMin = 0.05f,
                ScaleAmountMax = 0.14f,
                Color = color,
                Position = pos + new Vector3(0, 0.6f, 0),
                Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f },
            };
            p.Finished += () => p.QueueFree();
            _fxRoot.AddChild(p);
        }

        private void FireBullet()
        {
            var p = _player;
            var w = Config.Weapons[_weaponIdx];
            var dir = p.Aim.Normalized();
            for (int i = 0; i < w.Pellets; i++)
            {
                var d = dir;
                if (w.Spread > 0)
                {
                    float ang = ((float)GD.Randf() - 0.5f) * 2f * w.Spread;
                    d = new Vector2(
                        dir.X * Mathf.Cos(ang) - dir.Y * Mathf.Sin(ang),
                        dir.X * Mathf.Sin(ang) + dir.Y * Mathf.Cos(ang));
                }
                var proj = new Projectile
                {
                    Pos = p.Pos + d * 0.9f,
                    Vel = d * w.Speed,
                    Damage = w.Damage,
                };
                _projectiles.Add(proj);
                var mat = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = w.BulletColor,
                    EmissionEnabled = true,
                    Emission = w.BulletColor,
                    EmissionEnergyMultiplier = 3.2f,
                };
                // flame bolt: stretched emissive core + fire trail + light
                var boltNode = new Node3D { Position = new Vector3(proj.Pos.X, 0.95f, proj.Pos.Y) };
                boltNode.LookAt(boltNode.Position + new Vector3(d.X, 0, d.Y), Vector3.Up);
                var core = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = w.Pellets > 1 ? 0.09f : 0.12f, Height = 0.24f },
                    MaterialOverride = mat,
                    Scale = new Vector3(1f, 1f, 2.4f),
                };
                boltNode.AddChild(core);
                var flame = new CpuParticles3D
                {
                    Emitting = true,
                    Amount = 14,
                    Lifetime = 0.22f,
                    LocalCoords = false,
                    Spread = 22f,
                    InitialVelocityMin = 0.3f,
                    InitialVelocityMax = 1.2f,
                    Gravity = new Vector3(0, 1.6f, 0),
                    ScaleAmountMin = 0.06f,
                    ScaleAmountMax = 0.18f,
                    Color = new Color(w.BulletColor.R, w.BulletColor.G, w.BulletColor.B, 0.85f),
                    Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f },
                };
                boltNode.AddChild(flame);
                var blight = new OmniLight3D { LightColor = w.BulletColor, LightEnergy = 1.3f, OmniRange = 4f };
                boltNode.AddChild(blight);
                _fxRoot.AddChild(boltNode);
                _bulletNodes.Add((boltNode, proj));
            }
            PlaySfx(w.Sound);
            _muzzleLight.LightEnergy = 2.4f;
            _muzzleLight.LightColor = w.BulletColor;
            _muzzleLight.Position = new Vector3(p.Pos.X + dir.X * 0.8f, 1.1f, p.Pos.Y + dir.Y * 0.8f);
            // gun kick
            var wn = _weaponNodes.Count > _weaponIdx ? _weaponNodes[_weaponIdx] : null;
            if (wn != null)
            {
                var restPos = _weaponBoneSpace ? new Vector3(0.02f, -0.16f, 0.05f) : new Vector3(0.28f, 1.0f, 0.22f);
                var kickPos = _weaponBoneSpace ? new Vector3(0.02f, -0.11f, 0.05f) : new Vector3(0.28f, 1.0f, 0.13f);
                wn.Position = kickPos;
                _kickTween?.Kill();
                _kickTween = CreateTween();
                _kickTween.TweenProperty(wn, "position", restPos, 0.09f);
            }
        }

        private void TryMelee()
        {
            var p = _player;
            if (p.MeleeCd > 0) return;
            bool hit = false;
            foreach (var e in _enemies)
            {
                if (e.Dead || e.SpawnT > 0) continue;
                if (p.Pos.DistanceTo(e.Pos) <= Config.MeleeRange + e.Radius)
                {
                    e.Hp -= Config.MeleeDmg;
                    e.Flash = 1;
                    SpawnBurst(new Vector3(e.Pos.X, 1.0f, e.Pos.Y), BloodRed, 12, 4.5f);
                    BloodDecal(e.Pos, 0.8f);
                    hit = true;
                    if (e.Hp <= 0) KillEnemy(e, "melee");
                }
            }
            if (hit)
            {
                PlaySfx("melee");
                p.MeleeCd = Config.MeleeCooldown;
                PlayAnim(_playerAnim, "1H_Melee_Attack_Slice_Horizontal", 1.6f);
                SpawnBurst(new Vector3(p.Pos.X + p.Aim.X, 0.8f, p.Pos.Y + p.Aim.Y), TermGreen, 8, 4f);
            }
        }

        private void TryPurge()
        {
            var p = _player;
            if (p.PurgeCd > 0) return;
            p.PurgeCd = Config.PurgeCooldown;
            PlaySfx("purge");
            _purgeRingT = 0;
            _shake = 0.5f;
            foreach (var e in _enemies)
            {
                if (e.Dead || e.SpawnT > 0) continue;
                if (p.Pos.DistanceTo(e.Pos) <= Config.PurgeRadius + e.Radius)
                {
                    e.Hp -= Config.PurgeDmg;
                    e.Flash = 1;
                    SpawnBurst(new Vector3(e.Pos.X, 1.0f, e.Pos.Y), BloodRed, 16, 6f);
                    BloodDecal(e.Pos, 1.1f);
                    if (e.Hp <= 0) KillEnemy(e, "ranged");
                }
            }
            foreach (var b in _bolts)
                if (!b.Dead && b.Pos.DistanceTo(p.Pos) <= Config.PurgeRadius)
                    KillBolt(b);
        }

        private void DamagePlayer(float dmg)
        {
            var p = _player;
            if (p.Invuln > 0 || _phase != Phase.Playing) return;
            p.Hp -= dmg;
            p.Invuln = Config.InvulnTime;
            p.HitFlash = 1;
            PlaySfx("hurt");
            _shake = Mathf.Min(_shake + 0.3f, 0.7f);
            UpdateHp();
            if (p.Hp <= 0) OnPlayerDeath();
        }

        private void UpdateHp()
        {
            float pct = Mathf.Clamp(_player.Hp / Config.PlayerMaxHp, 0f, 1f);
            _hpFill.Size = new Vector2(296 * pct, 12);
            _hpFill.Color = pct < 0.3f ? AlertRed : TermGreen;
            if (_hpNum != null) _hpNum.Text = $"{Mathf.CeilToInt(Mathf.Max(0, _player.Hp))} / {(int)Config.PlayerMaxHp}";
        }

        private void OnPlayerDeath()
        {
            _phase = Phase.Over;
            _tauntPanel.Visible = false;
            _purgeBtn.Visible = false;
            ApplyMouseMode();
            _crosshair.Visible = false;
            SpawnBurst(new Vector3(_player.Pos.X, 0.8f, _player.Pos.Y), AlertRed, 40, 8f);
            _playerNode.Visible = false;
            PlaySfx("over");
            _bossActive = false;
            _pendingAutopsy = Task.Run(async () =>
                await PatientZeroBrain.Autopsy(_history, _profile, _wave, _kills, _score));
        }

        private void OnAutopsyReady((AutopsyReport report, string archetypeKey) r)
        {
            var (report, key) = r;
            _profile.RecordRun(new RunRecord
            {
                WavesSurvived = _wave,
                Score = _score,
                Kills = _kills,
                Archetype = key,
                CauseOfDeath = report.CauseOfDeath,
            }, _history);
            _lastReport = report;

            _rRun.Text = $"▚ AUTOPSY REPORT — RUN {_profile.RunCount:00}";
            _rSpec.Text = $"SPECIMEN #{_profile.SpecimenNumber:000}";
            _rWave.Text = $"WAVES SURVIVED: {_wave}";
            _rKills.Text = $"KILLS / SCORE: {_kills} / {_score}";
            _rArchetype.Text = $"ARCHETYPE: {report.Archetype.ToUpperInvariant()}";
            _rCause.Text = $"CAUSE OF DEATH: {report.CauseOfDeath}";
            _rWeak.Text = $"WEAKNESS: {report.Weakness}";
            _adaptFill.CustomMinimumSize = new Vector2(560 * Mathf.Clamp(report.AdaptabilityIndex, 0f, 1f), 10);
            _adaptFill.Size = new Vector2(560 * Mathf.Clamp(report.AdaptabilityIndex, 0f, 1f), 10);
            _rRemark.Text = $"\"{report.ClosingRemark}\"";
            _rSource.Text = report.Source == "gemini" ? "REPORT AUTHORED BY GEMINI FLASH" : "REPORT AUTHORED BY PZ-CORE (LOCAL MODEL)";
            _overPanel.Visible = true;
        }

        private void ShareReport()
        {
            if (_lastReport == null) return;
            DisplayServer.ClipboardSet(
                PatientZeroBrain.BuildShareText(_profile, _wave, _kills, _score, _lastReport));
        }

        // ==================================================================
        // TAUNT TYPEWRITER
        // ==================================================================
        private void ShowTaunt(string text, string reasoning)
        {
            _tauntFull = text;
            _tauntShown = 0;
            _tauntTick = 0;
            _tauntLabel.Text = "";
            _reasonLabel.Text = "";
            _tauntPanel.Visible = true;
            _tauntHideAt = _time + Config.TauntHold + text.Length * 0.03f;
            PlaySfx("taunt");
        }

        // ==================================================================
        // INPUT
        // ==================================================================
        public override void _UnhandledInput(InputEvent e)
        {
            if (e is InputEventScreenTouch touch)
            {
                if (touch.Pressed)
                {
                    bool aim = touch.Position.X > 640;
                    _touches[touch.Index] = (touch.Position, touch.Position, aim);
                }
                else _touches.Remove(touch.Index);
            }
            else if (e is InputEventScreenDrag drag)
            {
                if (_touches.TryGetValue(drag.Index, out var t))
                {
                    if (t.aim && _camMode != CamMode.Top)
                    {
                        var rel = drag.Position - t.cur;
                        _yaw -= rel.X * 0.006f;
                        _pitch = Mathf.Clamp(_pitch - rel.Y * 0.005f, -0.9f, 0.6f);
                        _mouseLookActive = true;
                    }
                    _touches[drag.Index] = (t.origin, drag.Position, t.aim);
                }
            }
            else if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                _mouseDown = mb.Pressed;
                _mousePos = mb.Position;
            }
            else if (e is InputEventMouseMotion mm)
            {
                _mousePos = mm.Position;
                if (Input.MouseMode == Input.MouseModeEnum.Captured && _camMode != CamMode.Top)
                {
                    _mouseLookActive = true;
                    _yaw -= mm.Relative.X * 0.0035f;
                    _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.003f, -0.9f, 0.6f);
                }
            }
            else if (e is InputEventKey kb && kb.Pressed && !kb.Echo)
            {
                if (kb.PhysicalKeycode == Key.Space)
                {
                    if (_phase == Phase.Menu) StartRun();
                    else if (_phase == Phase.Playing) TryPurge();
                }
                if (kb.PhysicalKeycode == Key.Enter && _phase == Phase.Menu) StartRun();
                if (kb.PhysicalKeycode == Key.Enter && _phase == Phase.Over && _overPanel.Visible)
                    GetTree().ReloadCurrentScene();
                if (kb.PhysicalKeycode == Key.C) CycleCamMode();
                if (kb.PhysicalKeycode == Key.Escape) Input.MouseMode = Input.MouseModeEnum.Visible;
                if (_phase == Phase.Playing)
                {
                    if (kb.PhysicalKeycode == Key.Key1) SwitchWeapon(0);
                    if (kb.PhysicalKeycode == Key.Key2) SwitchWeapon(1);
                    if (kb.PhysicalKeycode == Key.Key3) SwitchWeapon(2);
                    if (kb.PhysicalKeycode == Key.R) StartReload();
                }
            }
        }

        private Vector2 MoveInput()
        {
            float x = 0, y = 0;
            if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left)) x -= 1;
            if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) x += 1;
            if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up)) y -= 1;
            if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down)) y += 1;
            foreach (var t in _touches.Values)
            {
                if (t.aim) continue;
                var d = (t.cur - t.origin) / 90f;
                if (d.Length() > 0.15f) { x += d.X; y += d.Y; }
            }
            var v = new Vector2(x, y);
            if (v.Length() > 1) v = v.Normalized();
            if (_camMode != CamMode.Top && v.LengthSquared() > 0.0001f)
            {
                var f = new Vector2(-Mathf.Sin(_yaw), -Mathf.Cos(_yaw));
                var r = new Vector2(-f.Y, f.X);
                v = f * -v.Y + r * v.X;
            }
            return v;
        }

        private (Vector2 dir, bool firing) AimInput()
        {
            var p = _player;

            if (_camMode != CamMode.Top)
            {
                bool aimTouch = false;
                foreach (var t in _touches.Values) if (t.aim) aimTouch = true;

                bool auto = !_mouseLookActive && !aimTouch && !_fireHeld;
                Enemy? best = null; float bd = float.MaxValue;
                if (auto || _fireHeld)
                {
                    foreach (var e in _enemies)
                    {
                        if (e.Dead || e.SpawnT > 0) continue;
                        float d = p.Pos.DistanceTo(e.Pos);
                        if (d < bd) { bd = d; best = e; }
                    }
                    if (best != null && auto)
                    {
                        var dd = best.Pos - p.Pos;
                        _yaw = Mathf.Atan2(-dd.X, -dd.Y);
                    }
                }
                var aim = new Vector2(-Mathf.Sin(_yaw), -Mathf.Cos(_yaw));
                bool firing = _mouseDown || aimTouch || _fireHeld || (auto && best != null && _autoFire) || Input.IsPhysicalKeyPressed(Key.F);
                return (aim, firing);
            }

            foreach (var t in _touches.Values)
            {
                if (!t.aim) continue;
                var w = ScreenToGround(t.cur);
                var d = w - p.Pos;
                if (d.Length() > 0.2f) return (d.Normalized(), true);
            }
            if (_mouseDown)
            {
                var w = ScreenToGround(_mousePos);
                var d = w - p.Pos;
                if (d.Length() > 0.2f) return (d.Normalized(), true);
            }
            Enemy? bestT = null; float bdT = float.MaxValue;
            foreach (var e in _enemies)
            {
                if (e.Dead || e.SpawnT > 0) continue;
                float d = p.Pos.DistanceTo(e.Pos);
                if (d < bdT) { bdT = d; bestT = e; }
            }
            if (bestT != null) return ((bestT.Pos - p.Pos).Normalized(), _autoFire || Input.IsPhysicalKeyPressed(Key.F));
            return (p.Aim, false);
        }

        private Vector2 ScreenToGround(Vector2 screenPos)
        {
            // map screen px (1280x720 logical via stretch) to viewport
            var vp = GetViewport();
            var vpSize = vp.GetVisibleRect().Size;
            var scaled = screenPos * (vpSize / new Vector2(1280, 720));
            var from = _cam.ProjectRayOrigin(scaled);
            var dir = _cam.ProjectRayNormal(scaled);
            if (Mathf.Abs(dir.Y) < 0.0001f) return _player.Pos;
            float t = -from.Y / dir.Y;
            var hit = from + dir * t;
            return new Vector2(hit.X, hit.Z);
        }

        // ==================================================================
        // MAIN LOOP
        // ==================================================================
        public override void _Process(double delta)
        {
            float dt = Mathf.Min((float)delta, 0.05f);
            _time += dt;
            var p = _player;

            // dynamic music crossfade — boss theme when Patient Zero manifests
            if (_bossActive != _bossMusicOn && _bgm != null && _bgmBoss != null)
            {
                _bossMusicOn = _bossActive;
                _musicTween?.Kill();
                _musicTween = CreateTween().SetParallel();
                _musicTween.TweenProperty(_bgm, "volume_db", _bossMusicOn ? -80f : -9f, 1.4f);
                _musicTween.TweenProperty(_bgmBoss, "volume_db", _bossMusicOn ? -8f : -80f, 1.4f);
            }

            UpdateEnemyBars();

            // virtual joystick visuals (follow touches)
            if (_phase == Phase.Playing && _joyBaseL != null)
            {
                (Vector2 o, Vector2 c, bool active) moveS = (Vector2.Zero, Vector2.Zero, false), aimS = (Vector2.Zero, Vector2.Zero, false);
                foreach (var tv in _touches.Values)
                    if (tv.aim) aimS = (tv.origin, tv.cur, true); else moveS = (tv.origin, tv.cur, true);
                float k = 1280f / GetViewport().GetVisibleRect().Size.X;
                SetJoy(_joyBaseL, _joyKnobL, moveS, k);
                SetJoy(_joyBaseR, _joyKnobR, aimS, k);
            }
            else if (_joyBaseL != null && _joyBaseL.Visible)
            {
                _joyBaseL.Visible = _joyKnobL.Visible = _joyBaseR.Visible = _joyKnobR.Visible = false;
            }

            // menu fx
            if (_startPanel.Visible)
            {
                if (_menuRing != null) _menuRing.Rotation += dt * 0.35f;
                if (_titleGlow != null) _titleGlow.Modulate = new Color(1, 1, 1, 0.7f + 0.3f * Mathf.Sin(_time * 2.2f));
                if (_menuAccent != null) _menuAccent.Size = new Vector2(200 + 60 * Mathf.Sin(_time * 1.8f), 2);
            }

            // AI tasks completing
            if (_pendingDecision is { IsCompleted: true })
            {
                var d = _pendingDecision.Result;
                _pendingDecision = null;
                if (_phase == Phase.Intermission) OnDecisionReady(d);
            }
            if (_pendingAutopsy is { IsCompleted: true })
            {
                var r = _pendingAutopsy.Result;
                _pendingAutopsy = null;
                if (r.report != null) OnAutopsyReady(r);
            }

            // intermission flow
            if (_phase == Phase.Intermission)
            {
                _intermissionT += dt;
                if (_decision != null && _intermissionT > Config.TauntHold + 0.6f) LaunchNextWave();
            }

            // taunt typewriter
            if (_tauntPanel.Visible && _tauntShown < _tauntFull.Length)
            {
                _tauntTick += dt;
                while (_tauntTick > 0.03f && _tauntShown < _tauntFull.Length)
                {
                    _tauntTick -= 0.03f;
                    _tauntShown++;
                }
                _tauntLabel.Text = _tauntFull.Substring(0, _tauntShown);
                if (_tauntShown >= _tauntFull.Length && _decision != null)
                    _reasonLabel.Text = _decision.Reasoning;
            }
            if (_tauntPanel.Visible && _tauntShown >= _tauntFull.Length && _time > _tauntHideAt)
                _tauntPanel.Visible = false;

            // wave banner fade
            if (_bannerT >= 0)
            {
                _bannerT += dt;
                float a = _bannerT < 0.4f ? _bannerT / 0.4f : _bannerT < 1.3f ? 1f : Mathf.Max(0, 1f - (_bannerT - 1.3f) / 0.5f);
                _waveBanner.Modulate = new Color(1, 1, 1, a);
                if (_bannerT > 1.8f) { _waveBanner.Visible = false; _bannerT = -1f; }
            }

            // purge ring anim
            if (_purgeRingT >= 0)
            {
                _purgeRingT += dt;
                float k = _purgeRingT / 0.45f;
                if (k >= 1) { _purgeRingT = -1; _purgeRing.Visible = false; }
                else
                {
                    _purgeRing.Visible = true;
                    float r = Mathf.Lerp(0.5f, Config.PurgeRadius, k);
                    _purgeRing.Scale = new Vector3(r, 1, r);
                    _purgeRing.Position = new Vector3(p.Pos.X, 0.15f, p.Pos.Y);
                }
            }

            _muzzleLight.LightEnergy = Mathf.Max(0, _muzzleLight.LightEnergy - dt * 22f);

            if (_phase == Phase.Playing)
            {
                // spawn queue
                for (int i = _spawnQueue.Count - 1; i >= 0; i--)
                {
                    if (_time >= _spawnQueue[i].at)
                    {
                        SpawnEnemy(_spawnQueue[i].type, _spawnQueue[i].pos);
                        _spawnQueue.RemoveAt(i);
                    }
                }

                // movement
                var mv = MoveInput();
                p.Vel = mv * Config.PlayerSpeed;
                p.Pos += p.Vel * dt;
                p.Pos.X = Mathf.Clamp(p.Pos.X, -Config.WorldW / 2 + Config.PlayerRadius, Config.WorldW / 2 - Config.PlayerRadius);
                p.Pos.Y = Mathf.Clamp(p.Pos.Y, -Config.WorldH / 2 + Config.PlayerRadius, Config.WorldH / 2 - Config.PlayerRadius);
                foreach (var pil in Config.Pillars)
                {
                    var d = p.Pos - pil.Pos;
                    float dist = d.Length();
                    float minD = pil.R + Config.PlayerRadius;
                    if (dist < minD && dist > 0.001f) p.Pos = pil.Pos + d / dist * minD;
                }

                // aim + fire
                var (aimDir, firing) = AimInput();
                if (aimDir.LengthSquared() > 0.001f) p.Aim = aimDir;
                p.FireCd -= dt;
                if (firing && p.FireCd <= 0)
                {
                    if (_ammo[_weaponIdx] <= 0 && !_reloading) StartReload();
                    if (!_reloading && _ammo[_weaponIdx] > 0)
                    {
                        _ammo[_weaponIdx]--;
                        FireBullet();
                        p.FireCd = Config.Weapons[_weaponIdx].FireCd;
                        UpdateWeaponHud();
                    }
                }
                if (_playerAnim != null)
                {
                    if (mv.LengthSquared() > 0.01f) PlayAnim(_playerAnim, "Walk", 1.1f);
                    else PlayAnim(_playerAnim, "Idle", 1f);
                }

                p.MeleeCd -= dt;
                TryMelee();
                p.PurgeCd = Mathf.Max(0, p.PurgeCd - dt);
                _purgeBtn.Text = p.PurgeCd <= 0 ? "PURGE [SPACE]" : $"PURGE {Mathf.CeilToInt(p.PurgeCd)}s";
                p.Invuln = Mathf.Max(0, p.Invuln - dt);
                p.HitFlash = Mathf.Max(0, p.HitFlash - dt * 3);

                _logger.TrackFrame(dt, p.Pos, p.Vel);

                // enemies
                foreach (var e in _enemies)
                {
                    if (e.Dead) continue;
                    if (e.SpawnT > 0) { e.SpawnT -= dt; continue; }
                    e.Flash = Mathf.Max(0, e.Flash - dt * 4);
                    e.AttackCd -= dt;

                    var to = p.Pos - e.Pos;
                    float dist = to.Length();
                    var dirV = dist > 0.001f ? to / dist : Vector2.Zero;
                    if (e.Type == EnemyType.Boss && dist < 10f) dirV *= 0.15f; // boss holds ground to cast
                    var steer = dirV;
                    foreach (var o in _enemies)
                    {
                        if (ReferenceEquals(o, e) || o.Dead) continue;
                        var away = e.Pos - o.Pos;
                        float od = away.Length();
                        float want = (e.Radius + o.Radius) * 1.4f;
                        if (od < want && od > 0.001f) steer += away / od * 0.7f;
                    }
                    foreach (var pil in Config.Pillars)
                    {
                        var away = e.Pos - pil.Pos;
                        float pd = away.Length();
                        if (pd < pil.R + e.Radius + 0.6f && pd > 0.001f) steer += away / pd * 1.5f;
                    }
                    if (steer.LengthSquared() > 0.001f) steer = steer.Normalized();
                    e.Vel = steer * e.Speed;

                    // fast reapers lunge — telegraphed dash
                    if (e.Type == EnemyType.Fast)
                    {
                        e.LungeCd -= dt;
                        if (e.LungeT > 0)
                        {
                            e.LungeT -= dt;
                            e.Vel = e.LungeDir * (e.Speed * 3.1f);
                        }
                        else if (e.LungeCd <= 0 && dist > 3.2f && dist < 7.5f)
                        {
                            e.LungeCd = 2.8f;
                            e.LungeT = 0.26f;
                            e.LungeDir = dirV;
                        }
                    }
                    e.Pos += e.Vel * dt;
                    e.Pos.X = Mathf.Clamp(e.Pos.X, -Config.WorldW / 2 + e.Radius, Config.WorldW / 2 - e.Radius);
                    e.Pos.Y = Mathf.Clamp(e.Pos.Y, -Config.WorldH / 2 + e.Radius, Config.WorldH / 2 - e.Radius);

                    if (dist <= Config.EnemyAttackRange + e.Radius && e.AttackCd <= 0)
                    {
                        e.AttackCd = Config.EnemyAttackCd;
                        DamagePlayer(e.Damage);
                        if (_enemyAnims.TryGetValue(e.Id, out var ap)) PlayAnim(ap, "attack", 1.4f);
                    }

                    // crazy water attack — boss + tanky cast bolts from range
                    float castRange = e.Type == EnemyType.Boss ? 13f : e.Type == EnemyType.Tanky ? 8f : 0f;
                    e.CastCd -= dt;
                    if (castRange > 0 && e.CastCd <= 0 && dist < castRange && dist > 2.3f)
                    {
                        e.CastCd = e.Type == EnemyType.Boss ? 3.2f : 5.5f;
                        CastWaterBolt(e);
                        if (_enemyAnims.TryGetValue(e.Id, out var apc)) PlayAnim(apc, "attack", 1.2f);
                    }
                }

                // projectiles
                foreach (var b in _projectiles)
                {
                    if (b.Dead) continue;
                    b.Pos += b.Vel * dt;
                    b.Life -= dt;
                    if (b.Life <= 0 || Mathf.Abs(b.Pos.X) > Config.WorldW / 2 || Mathf.Abs(b.Pos.Y) > Config.WorldH / 2)
                    { b.Dead = true; continue; }
                    bool blocked = false;
                    foreach (var pil in Config.Pillars)
                        if (b.Pos.DistanceTo(pil.Pos) < pil.R) { blocked = true; break; }
                    if (blocked) { b.Dead = true; continue; }
                    foreach (var e in _enemies)
                    {
                        if (e.Dead || e.SpawnT > 0) continue;
                        if (b.Pos.DistanceTo(e.Pos) < e.Radius + 0.18f)
                        {
                            e.Hp -= b.Damage;
                            e.Flash = 1;
                            b.Dead = true;
                            SpawnBurst(new Vector3(b.Pos.X, 1.0f, b.Pos.Y), BloodRed, 14, 5f);
                            BloodDecal(e.Pos, 0.7f);
                            if (e.Hp <= 0) KillEnemy(e, "ranged");
                            break;
                        }
                    }
                }

                // enemy water bolts
                foreach (var b in _bolts)
                {
                    if (b.Dead) continue;
                    b.Pos += b.Vel * dt;
                    b.Life -= dt;
                    if (b.Life <= 0) { KillBolt(b); continue; }
                    bool blocked = false;
                    foreach (var pil in Config.Pillars)
                        if (b.Pos.DistanceTo(pil.Pos) < pil.R) { blocked = true; break; }
                    if (blocked) { KillBolt(b); continue; }
                    if (b.Pos.DistanceTo(p.Pos) < Config.PlayerRadius + 0.45f)
                    {
                        DamagePlayer(16f);
                        KillBolt(b);
                    }
                }
                _bolts.RemoveAll(b => b.Dead);

                _enemies.RemoveAll(e => e.Dead);
                _projectiles.RemoveAll(b => b.Dead);

                if (_spawnQueue.Count == 0 && _enemies.Count == 0)
                {
                    _score += 50 * _wave;
                    _scoreLabel.Text = _score.ToString();
                    EndWave();
                }
            }

            SyncVisuals(dt);
            UpdateCamera(dt);

            // screenshot dev mode
            if (_screenshotMode && _shotAt > 0 && _time >= _shotAt)
            {
                _shotAt = -1;
                var img = GetViewport().GetTexture().GetImage();
                img.SavePng("/tmp/pz_shot.png");
                GD.Print("SCREENSHOT SAVED /tmp/pz_shot.png");
                GetTree().Quit();
            }
        }

        // ==================================================================
        // VISUAL SYNC (sim → 3D nodes)
        // ==================================================================
        private void SyncVisuals(float dt)
        {
            // player
            if (_playerNode.Visible)
            {
                float pbob = _player.Vel.LengthSquared() > 0.1f ? Mathf.Abs(Mathf.Sin(_time * 8f)) * 0.05f : 0f;
                _playerNode.Position = new Vector3(_player.Pos.X, pbob, _player.Pos.Y);
                if (_playerRig != null && _playerAnim == null)
                {
                    float speedK = Mathf.Clamp(_player.Vel.Length() / Config.PlayerSpeed, 0f, 1f);
                    PoseWalk(_playerRig, _time * 9.5f, 0.12f + 0.45f * speedK);
                }
                if (_player.Aim.LengthSquared() > 0.001f)
                {
                    float yaw = Mathf.Atan2(_player.Aim.X, _player.Aim.Y);
                    _playerNode.Rotation = new Vector3(0, yaw, 0);
                }
                if (_player.HitFlash > 0.4f)
                    _playerNode.Scale = Vector3.One * 1.05f * (1f + _player.HitFlash * 0.06f);
                else
                    _playerNode.Scale = Vector3.One * 1.05f;
            }

            // enemies
            foreach (var e in _enemies)
            {
                if (!_enemyNodes.TryGetValue(e.Id, out var node)) continue;
                float rise = e.SpawnT > 0 ? -1.5f * (e.SpawnT / 0.55f) : 0f;
                bool hasAnim = _enemyAnims.TryGetValue(e.Id, out var apE) && apE != null;
                float bob = 0f, roll = 0f;
                if (!hasAnim && e.SpawnT <= 0)
                {
                    float sp = e.Type == EnemyType.Fast ? 9f : 5.5f;
                    bob = Mathf.Abs(Mathf.Sin(_time * sp + e.Id * 1.7f)) * 0.055f;
                    roll = Mathf.Sin(_time * 2.4f + e.Id) * 0.07f; // shamble sway
                }
                node.Position = new Vector3(e.Pos.X, rise + bob, e.Pos.Y);
                // rigged models: real walk cycle driven by bone poses
                if (e.SpawnT <= 0 && !hasAnim && _enemyRigs.TryGetValue(e.Id, out var rig) && rig != null)
                {
                    float speedK = Mathf.Clamp(e.Vel.Length() / Mathf.Max(0.01f, e.Speed), 0f, 1f);
                    float cadence = e.Type == EnemyType.Fast ? 10.5f : e.Type == EnemyType.Boss ? 4.5f : 6.5f;
                    PoseWalk(rig, _time * cadence + e.Id * 1.3f, (0.15f + 0.5f * speedK) * (e.Type == EnemyType.Boss ? 0.8f : 1f));
                }
                if (_bloodMats.TryGetValue(e.Id, out var bm))
                {
                    float k = Mathf.Clamp(e.Flash * 1.4f, 0f, 1f);
                    for (int mi = 0; mi < bm.mats.Count; mi++)
                        bm.mats[mi].AlbedoColor = bm.orig[mi].Lerp(BloodRed, k);
                }
                float baseScale = ModelScale.GetValueOrDefault(e.Type, 1f);
                float s = e.SpawnT > 0 ? baseScale * Mathf.Max(0.05f, 1f - e.SpawnT / 0.55f) : baseScale;
                if (e.Flash > 0.3f) s *= 1.12f;
                node.Scale = Vector3.One * s;
                if (e.Vel.LengthSquared() > 0.01f)
                {
                    float yaw = Mathf.Atan2(e.Vel.X, e.Vel.Y);
                    node.Rotation = new Vector3(0, yaw, roll);
                }
                // keep walking after spawn, attack anim handled on hit
                if (hasAnim && e.SpawnT <= 0 && apE!.CurrentAnimation.ToString().Contains("Attack") && !apE.IsPlaying())
                {
                    PlayAnim(apE, "Walk", e.Type == EnemyType.Fast ? 1.5f : 1f);
                }
            }

            // water bolts
            foreach (var kv in _boltNodes)
                if (!kv.Key.Dead)
                    kv.Value.Position = new Vector3(kv.Key.Pos.X, 1.1f + Mathf.Sin(_time * 6f) * 0.08f, kv.Key.Pos.Y);

            // bullets (flame flicker)
            for (int i = _bulletNodes.Count - 1; i >= 0; i--)
            {
                var (node, sim) = _bulletNodes[i];
                if (sim.Dead)
                {
                    node.QueueFree();
                    _bulletNodes.RemoveAt(i);
                    continue;
                }
                node.Position = new Vector3(sim.Pos.X, 0.95f, sim.Pos.Y);
                float fl = 1f + 0.14f * Mathf.Sin(_time * 31f + i * 2.1f);
                node.Scale = new Vector3(fl, fl, 1f + 0.2f * Mathf.Sin(_time * 26f + i));
            }

            // particles (sim-driven fallback bursts use CpuParticles; sim list only for compat)
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var pt = _particles[i];
                pt.Pos += pt.Vel * dt;
                pt.Vel *= 0.92f;
                pt.Life -= dt;
                if (pt.Life <= 0) _particles.RemoveAt(i);
            }
        }

        // ==================================================================
        // CAMERA MODES — TOP / TPP / FPP
        // ==================================================================
        private void CycleCamMode()
        {
            _camMode = _camMode == CamMode.Top ? CamMode.Tpp : _camMode == CamMode.Tpp ? CamMode.Fpp : CamMode.Top;
            _camBlend = 0f;
            ApplyCamVisuals();
        }

        private void ApplyCamVisuals()
        {
            _camBtn.Text = _camMode switch
            {
                CamMode.Tpp => "CAM: TPP [C]",
                CamMode.Fpp => "CAM: FPP [C]",
                _ => "CAM: TOP [C]",
            };
            _crosshair.Visible = _camMode != CamMode.Top && _phase == Phase.Playing;
            _playerNode.Visible = _camMode != CamMode.Fpp;
            ApplyMouseMode();
        }

        private void ApplyMouseMode()
        {
            Input.MouseMode = _phase == Phase.Playing && _camMode != CamMode.Top && !_screenshotMode
                ? Input.MouseModeEnum.Captured
                : Input.MouseModeEnum.Visible;
        }

        private Vector3 CamForward() => new(-Mathf.Sin(_yaw), 0, -Mathf.Cos(_yaw));

        private void UpdateCamera(float dt)
        {
            var p3 = new Vector3(_player.Pos.X, 0, _player.Pos.Y);
            var fwd = CamForward();
            Vector3 desired, desiredRot;
            switch (_camMode)
            {
                case CamMode.Tpp:
                {
                    var target = p3 + Vector3.Up * 1.7f;
                    desired = target - fwd * 4.6f + Vector3.Up * 2.6f;
                    desiredRot = new Vector3(-0.42f + _pitch * 0.55f, _yaw, 0);
                    break;
                }
                case CamMode.Fpp:
                {
                    desired = p3 + Vector3.Up * 1.62f - fwd * 0.15f;
                    desiredRot = new Vector3(_pitch, _yaw, 0);
                    break;
                }
                default:
                {
                    desired = new Vector3(0, 25f, 15.5f);
                    desiredRot = new Vector3(Mathf.DegToRad(-58f), 0, 0);
                    break;
                }
            }
            if (_camBlend < 1f)
            {
                _camBlend = Mathf.Min(1f, _camBlend + dt * 3.2f);
                float k = _camBlend * _camBlend * (3f - 2f * _camBlend); // smoothstep
                _camPos = _camPos.Lerp(desired, k);
                _camRot = _camRot.Lerp(desiredRot, k);
            }
            else { _camPos = desired; _camRot = desiredRot; }

            Vector3 off = Vector3.Zero;
            if (_shake > 0.001f)
            {
                _shake = Mathf.Max(0, _shake - dt * CamShakeDecay);
                off = new Vector3(
                    (float)GD.RandRange(-_shake, _shake),
                    (float)GD.RandRange(-_shake, _shake) * 0.4f,
                    (float)GD.RandRange(-_shake, _shake));
            }
            _cam.Position = _camPos + off;
            _cam.Rotation = _camRot;
        }
    }
}
