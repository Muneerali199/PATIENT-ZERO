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

        // ---------- sim entities ----------
        private readonly Player _player = new();
        private readonly List<Enemy> _enemies = new();
        private readonly List<Projectile> _projectiles = new();
        private readonly List<Particle> _particles = new();
        private readonly List<(float at, EnemyType type, Vector2 pos)> _spawnQueue = new();
        private int _nextEnemyId = 1;

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
        private readonly List<(MeshInstance3D node, Projectile sim)> _bulletNodes = new();

        private static readonly Dictionary<EnemyType, string> ModelPaths = new()
        {
            { EnemyType.Standard, "res://assets/models/zombie_standard.glb" },
            { EnemyType.Fast, "res://assets/models/zombie_runner.glb" },
            { EnemyType.Tanky, "res://assets/models/zombie_brute.glb" },
            { EnemyType.Boss, "res://assets/custom/patient_zero.glb" },
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

            _profile = SpecimenProfile.Load();
            BuildWorld();
            BuildArena();
            BuildPlayer();
            BuildUi();
            ShowStart();
            if (_screenshotMode)
            {
                StartRun();
                _shotAt = 4.0f;
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

        // ---- Custom character override: drop YOUR .glb into res://assets/custom/ ----
        private static readonly string[] CustomNames =
            { "player", "zombie_standard", "zombie_runner", "zombie_brute" };

        private static PackedScene? LoadCharacter(string builtinPath, string customName)
        {
            string custom = $"res://assets/custom/{customName}.glb";
            if (ResourceLoader.Exists(custom))
            {
                var s = LoadScene(custom);
                if (s != null) return s;
            }
            return LoadScene(builtinPath);
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

        /// <summary>Wraps any GLB so it is targetHeight tall, XZ-centered, feet on the ground.
        /// Outer wrapper scale stays free for gameplay (spawn-in, hit-flash).</summary>
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

                // rifle in the hero's right hand
                var rifleScene = LoadScene("res://assets/models/rifle.glb");
                if (rifleScene != null)
                {
                    _rifle = rifleScene.Instantiate<Node3D>();
                    _rifle.Position = new Vector3(0.28f, 0.95f, 0.22f);
                    _rifle.RotationDegrees = new Vector3(0, 0, 0);
                    _playerNode.AddChild(_rifle);
                }
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
            _ui.AddChild(_purgeBtn);

            // FIRE button (hold to shoot — touch-friendly)
            _fireBtn = new Button { Text = "🔥 FIRE", Position = new Vector2(1066, 476), Size = new Vector2(190, 100) };
            _fireBtn.AddThemeFontSizeOverride("font_size", 22);
            _fireBtn.ButtonDown += () => _fireHeld = true;
            _fireBtn.ButtonUp += () => _fireHeld = false;
            _ui.AddChild(_fireBtn);

            // Camera mode button
            var camBtn = new Button { Text = "CAM: TOP [C]", Position = new Vector2(24, 596), Size = new Vector2(180, 60) };
            camBtn.AddThemeFontSizeOverride("font_size", 15);
            camBtn.Pressed += CycleCamMode;
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

            var title = MkLabel(panel, "PATIENT ZERO", new Vector2(140, 120), new Vector2(1000, 80), 72, TermGreen, HorizontalAlignment.Center);
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
            _enemyNodes.Clear(); _enemyAnims.Clear();
            _enemies.Clear(); _projectiles.Clear(); _particles.Clear(); _spawnQueue.Clear();
            foreach (var b in _bulletNodes) b.node.QueueFree();
            _bulletNodes.Clear();
            _player.Pos = new Vector2(0, 6);
            _player.Hp = Config.PlayerMaxHp;
            _scoreLabel.Text = "0";
            UpdateHp();
            ApplyCamVisuals();
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
                EnemyType.Fast => 1.55f,
                EnemyType.Tanky => 2.15f,
                EnemyType.Boss => 2.7f,
                _ => 1.75f,
            };
            AnimationPlayer? ap = null;
            if (scene != null)
            {
                node = NormalizeModel(scene.Instantiate<Node3D>(), targetH);
                node.Scale = Vector3.One * ModelScale[type] * 0.05f; // spawn scale-in
                ap = FindAnim(node);
                if (ap != null) PlayAnim(ap, "Walk", type == EnemyType.Fast ? 1.5f : 1f);
                if (type != EnemyType.Boss)
                    TintModel(node, Config.EnemyTint(_theme, type), type == EnemyType.Tanky ? 0.4f : 0.22f);
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
            _score += Config.Enemies[e.Type].score;
            _scoreLabel.Text = _score.ToString();

            if (_enemyNodes.TryGetValue(e.Id, out var node))
            {
                _enemyNodes.Remove(e.Id);
                AnimationPlayer? ap = _enemyAnims.GetValueOrDefault(e.Id);
                _enemyAnims.Remove(e.Id);
                SpawnBurst(node.Position, Config.EnemyTint(_theme, e.Type), e.Type == EnemyType.Boss ? 60 : 26, e.Type == EnemyType.Boss ? 9f : 6f);
                if (e.Type == EnemyType.Boss)
                {
                    ShowTaunt("A temporary avatar. I remain.", "» avatar destroyed — core intelligence unaffected");
                    _shake = 0.8f;
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
            var dir = p.Aim.Normalized();
            var proj = new Projectile
            {
                Pos = p.Pos + dir * 0.9f,
                Vel = dir * Config.BulletSpeed,
            };
            _projectiles.Add(proj);

            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color("#d9ffe6"),
                EmissionEnabled = true,
                Emission = new Color("#9ef0b3"),
                EmissionEnergyMultiplier = 3.2f,
            };
            var node = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f },
                MaterialOverride = mat,
                Position = new Vector3(proj.Pos.X, 0.9f, proj.Pos.Y),
            };
            _fxRoot.AddChild(node);
            _bulletNodes.Add((node, proj));
            _muzzleLight.LightEnergy = 2.2f;
            _muzzleLight.Position = new Vector3(p.Pos.X + p.Aim.X * 0.8f, 1.35f, p.Pos.Y + p.Aim.Y * 0.8f);
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
                    hit = true;
                    if (e.Hp <= 0) KillEnemy(e, "melee");
                }
            }
            if (hit)
            {
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
            _purgeRingT = 0;
            _shake = 0.5f;
            foreach (var e in _enemies)
            {
                if (e.Dead || e.SpawnT > 0) continue;
                if (p.Pos.DistanceTo(e.Pos) <= Config.PurgeRadius + e.Radius)
                {
                    e.Hp -= Config.PurgeDmg;
                    e.Flash = 1;
                    if (e.Hp <= 0) KillEnemy(e, "ranged");
                }
            }
        }

        private void DamagePlayer(float dmg)
        {
            var p = _player;
            if (p.Invuln > 0 || _phase != Phase.Playing) return;
            p.Hp -= dmg;
            p.Invuln = Config.InvulnTime;
            p.HitFlash = 1;
            _shake = Mathf.Min(_shake + 0.3f, 0.7f);
            UpdateHp();
            if (p.Hp <= 0) OnPlayerDeath();
        }

        private void UpdateHp()
        {
            float pct = Mathf.Clamp(_player.Hp / Config.PlayerMaxHp, 0f, 1f);
            _hpFill.Size = new Vector2(296 * pct, 12);
            _hpFill.Color = pct < 0.3f ? AlertRed : TermGreen;
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
                bool firing = _mouseDown || aimTouch || _fireHeld || (auto && best != null);
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
            if (bestT != null) return ((bestT.Pos - p.Pos).Normalized(), true);
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
                    FireBullet();
                    p.FireCd = Config.FireCooldown;
                    PlayAnim(_playerAnim, "1H_Ranged_Shoot", 2f);
                }
                else if (mv.LengthSquared() > 0.01f)
                {
                    PlayAnim(_playerAnim, "Walk", 1.1f);
                }
                else if (_playerAnim != null && _playerAnim.CurrentAnimation.ToString().Contains("Shoot"))
                {
                    PlayAnim(_playerAnim, "Idle", 1f);
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
                    e.Pos += e.Vel * dt;
                    e.Pos.X = Mathf.Clamp(e.Pos.X, -Config.WorldW / 2 + e.Radius, Config.WorldW / 2 - e.Radius);
                    e.Pos.Y = Mathf.Clamp(e.Pos.Y, -Config.WorldH / 2 + e.Radius, Config.WorldH / 2 - e.Radius);

                    if (dist <= Config.EnemyAttackRange + e.Radius && e.AttackCd <= 0)
                    {
                        e.AttackCd = Config.EnemyAttackCd;
                        DamagePlayer(e.Damage);
                        if (_enemyAnims.TryGetValue(e.Id, out var ap)) PlayAnim(ap, "Melee_Attack", 1.4f);
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
                            e.Hp -= Config.BulletDmg;
                            e.Flash = 1;
                            b.Dead = true;
                            if (_enemyNodes.TryGetValue(e.Id, out var hitNode))
                                SpawnBurst(hitNode.Position, new Color("#ffffff"), 4, 3f);
                            if (e.Hp <= 0) KillEnemy(e, "ranged");
                            break;
                        }
                    }
                }

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

            // bullets
            for (int i = _bulletNodes.Count - 1; i >= 0; i--)
            {
                var (node, sim) = _bulletNodes[i];
                if (sim.Dead)
                {
                    node.QueueFree();
                    _bulletNodes.RemoveAt(i);
                    continue;
                }
                node.Position = new Vector3(sim.Pos.X, 0.9f, sim.Pos.Y);
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
