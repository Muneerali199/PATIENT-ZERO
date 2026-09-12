# 📊 DATAFLOW — System Data Flow & Architecture Diagrams

> **Data Flow Document** | v1.0 | September 2026
> Visual architecture reference — how data moves through Patient Zero: Protocol.

---

## 1. Master Data Flow

The complete data journey from game start to game over:

```mermaid
graph TD
    subgraph INIT["🚀 Initialization (Once)"]
        START["Game Start"] --> LOC["📍 Request Device Location"]
        LOC --> PERM{"Permission<br/>Granted?"}
        PERM -->|Yes| COORDS["Receive lat/lng"]
        PERM -->|No| DEFAULT_THEME["Default: temperate"]
        COORDS --> REVERSE{"Reverse<br/>Geocode"}
        REVERSE -->|Success| CITY["City Name"]
        REVERSE -->|Fail| LOCAL{"Local Lookup<br/>Table"}
        LOCAL -->|Match| CITY
        LOCAL -->|No Match| DEFAULT_THEME
        CITY --> BUCKET["Theme Bucket<br/>(coastal/desert/temperate/mountain/urban)"]
        DEFAULT_THEME --> BUCKET
        BUCKET --> ARENA_SKIN["🎨 Apply Arena Theme<br/>(skybox, fog, floor, lights)"]
        BUCKET --> W1_BIAS["⚖️ Set Wave 1<br/>Enemy Composition Bias"]
        BUCKET --> THEME_VAR["Set enemy_theme_variant<br/>for AI (Wave 1 only)"]
    end

    subgraph GAMELOOP["🔄 Core Game Loop"]
        W1_BIAS --> SPAWN["👾 Spawn Enemies"]
        SPAWN --> COMBAT["⚔️ Combat Active"]
        
        subgraph TRACKING["📊 Per-Frame Tracking"]
            COMBAT --> T1["Track: player position"]
            COMBAT --> T2["Track: velocity (stationary vs moving)"]
            COMBAT --> T3["Track: distance to nearest cover"]
        end
        
        subgraph KILL_TRACK["💀 Per-Kill Tracking"]
            COMBAT --> K1["Record: kill type (ranged/melee)"]
            COMBAT --> K2["Record: engagement distance"]
        end
        
        COMBAT --> WAVEEND{"All Enemies<br/>Dead?"}
        WAVEEND -->|No| COMBAT
        WAVEEND -->|Yes| COMPILE["📋 Compile Behavior Summary"]
    end

    subgraph AI_LOOP["🧠 AI Decision Loop"]
        COMPILE --> SUMMARY["Behavior Summary JSON"]
        SUMMARY --> API_CALL["🔌 Send to Gemini Flash"]
        API_CALL --> TIMEOUT{"Response<br/>< 2s?"}
        TIMEOUT -->|Yes| PARSE["📦 Parse JSON Response"]
        TIMEOUT -->|No| FALLBACK["⚠️ Fallback Composition"]
        PARSE --> VALID{"Valid<br/>Schema?"}
        VALID -->|Yes| CLAMP["🛡️ Safety Rail Clamp"]
        VALID -->|No| FALLBACK
        CLAMP --> DECISION["AI Decision:<br/>composition + zone + taunt"]
        FALLBACK --> DECISION
    end

    subgraph OUTPUT["📤 Apply Decision"]
        DECISION --> SPAWNER["Wave Spawner:<br/>enemy types & counts"]
        DECISION --> ZONE["Spawn Points:<br/>zone bias weighting"]
        DECISION --> TAUNT["UI: Display taunt text"]
        SPAWNER --> SPAWN
        ZONE --> SPAWN
    end

    COMBAT --> DEATH{"Player<br/>HP = 0?"}
    DEATH -->|Yes| GAMEOVER["🏆 Game Over Screen"]
    DEATH -->|No| COMBAT
```

---

## 2. Behavior Logger Data Flow

How player metrics are collected, accumulated, and compiled:

```mermaid
graph LR
    subgraph PERFRAME["Per-Frame (every Update)"]
        POS["Player Position<br/>(x, z)"]
        VEL["Player Velocity<br/>magnitude"]
        COV["Distance to<br/>Nearest Cover"]
    end

    subgraph PERKILL["Per-Kill Event"]
        KT["Kill Type<br/>(ranged / melee)"]
        KD["Kill Distance<br/>(player → enemy)"]
    end

    subgraph ACCUM["Accumulators (reset per wave)"]
        SF["stationaryFrames++"]
        MF["movingFrames++"]
        TF["totalFrames++"]
        CD["totalCoverDist += dist"]
        RK["rangedKills++"]
        MK["meleeKills++"]
        TK["totalKills++"]
        ED["totalEngageDist += dist"]
    end

    VEL -->|"vel < 0.1"| SF
    VEL -->|"vel ≥ 0.1"| MF
    POS --> TF
    COV --> CD
    KT -->|"ranged"| RK
    KT -->|"melee"| MK
    KT --> TK
    KD --> ED

    subgraph COMPILE["Wave-End Compilation"]
        O1["time_stationary_pct = SF / TF"]
        O2["time_moving_pct = MF / TF"]
        O3["kills_ranged_pct = RK / TK"]
        O4["kills_melee_pct = MK / TK"]
        O5["avg_engagement_distance = ED / TK"]
        O6["avg_distance_to_cover = CD / TF"]
        O7["wave_clear_time_seconds = elapsed"]
        O8["player_hp_remaining_pct = hp / maxHp"]
    end

    SF --> O1
    TF --> O1
    MF --> O2
    RK --> O3
    TK --> O3
    MK --> O4
    ED --> O5
    CD --> O6
```

---

## 3. AI Client Module — Internal Flow

```mermaid
sequenceDiagram
    participant WM as Wave Manager
    participant BL as Behavior Logger
    participant AC as AI Client
    participant SR as Safety Rails
    participant FB as Fallback Provider
    participant GM as Gemini Flash API
    participant SP as Enemy Spawner
    participant UI as HUD / Taunt Display

    WM->>BL: Wave ended — compile stats
    BL->>BL: Calculate percentages & averages
    BL->>AC: BehaviorSummary object

    AC->>AC: Build request (system prompt + summary JSON)
    AC->>GM: POST /generateContent (timeout: 2s)

    alt Response received < 2s
        GM->>AC: JSON response
        AC->>AC: Parse candidates[0].content.parts[0].text
        AC->>SR: Raw AIDecision object

        alt Valid schema
            SR->>SR: Clamp enemy counts (min 1, max 15 per type)
            SR->>SR: Clamp total (min 3, max 30)
            SR->>SR: Validate spawn_zone_bias enum
            SR->>SR: Sanitize taunt_text
            SR->>SP: Clamped composition + zone bias
            SR->>UI: Sanitized taunt text
        else Invalid schema
            SR->>FB: Request fallback
            FB->>SP: Fallback composition
            FB->>UI: Fallback taunt ("...")
        end

    else Timeout (>2s)
        AC->>FB: Request fallback
        FB->>SP: Fallback composition
        FB->>UI: Fallback taunt ("...")
    end

    SP->>SP: Select spawn points by zone bias
    SP->>SP: Instantiate enemies by type counts
    SP->>WM: Wave ready — start next wave

    UI->>UI: Typewriter animation for taunt
    UI->>UI: Auto-dismiss after 4 seconds
```

---

## 4. Location Seeding Flow

```mermaid
graph TD
    A["🎮 Game Start"] --> B["Request Device Location"]
    B --> C{"Location Services<br/>Enabled?"}
    
    C -->|No| D["⚠️ Default: temperate"]
    C -->|Yes| E["Prompt User for<br/>Permission"]
    
    E --> F{"Permission<br/>Granted?"}
    F -->|No| D
    F -->|Yes| G["Wait for Coordinates<br/>(timeout: 5s)"]
    
    G --> H{"Received<br/>within 5s?"}
    H -->|No| D
    H -->|Yes| I["Got lat/lng"]
    
    I --> J["Try Reverse Geocode API"]
    J --> K{"API<br/>Success?"}
    
    K -->|Yes| L["Extract City Name"]
    K -->|No| M["Try Local Lookup Table"]
    
    M --> N{"Coordinates<br/>Match Entry?"}
    N -->|Yes| O["Get Bucket from Table"]
    N -->|No| D
    
    L --> P["Map City → Theme Bucket"]
    O --> P
    D --> P
    
    P --> Q["Apply Arena Theme"]
    P --> R["Set Wave 1 Bias"]
    P --> S["Set enemy_theme_variant"]
    
    subgraph THEME_APP["Theme Application"]
        Q --> Q1["Skybox Color"]
        Q --> Q2["Fog Color + Density"]
        Q --> Q3["Floor Material"]
        Q --> Q4["Ambient Light"]
        Q --> Q5["Pillar Tint"]
    end
    
    subgraph WAVE1["Wave 1 Setup"]
        R --> R1["Standard Count"]
        R --> R2["Fast Count"]
        R --> R3["Tanky Count"]
    end
```

---

## 5. Wave Lifecycle State Machine

```mermaid
stateDiagram-v2
    [*] --> GameInit: Game Start

    GameInit --> LocationSeed: Read location
    LocationSeed --> ThemeApplied: Apply theme

    ThemeApplied --> PreWave: Set Wave 1

    state PreWave {
        [*] --> ShowWaveNumber: "Wave N" text
        ShowWaveNumber --> DetermineComposition: Get composition
        DetermineComposition --> SetSpawnPoints: Apply zone bias
    }

    PreWave --> Spawning: Begin spawning

    state Spawning {
        [*] --> SpawnEnemy: For each enemy
        SpawnEnemy --> SpawnEnemy: Next enemy
        SpawnEnemy --> AllSpawned: Count reached
    }

    Spawning --> Combat: All enemies placed

    state Combat {
        [*] --> PlayerAction: Player input
        PlayerAction --> EnemyAction: Enemy AI
        EnemyAction --> DamageCheck: Resolve damage
        DamageCheck --> PlayerAction: Continue
    }

    Combat --> PlayerDied: Player HP = 0
    Combat --> WaveCleared: All enemies dead

    PlayerDied --> GameOver

    WaveCleared --> StatsCompile: Compile behavior summary
    StatsCompile --> AICall: Send to Gemini
    
    state AICall {
        [*] --> Waiting: API request sent
        Waiting --> ResponseReceived: JSON response
        Waiting --> TimedOut: >2 seconds
        ResponseReceived --> Validate: Check schema
        Validate --> SafetyClamp: Apply rails
        Validate --> Fallback: Invalid
        TimedOut --> Fallback: Use fallback
    }

    AICall --> TauntDisplay: Show taunt text
    TauntDisplay --> PreWave: Next wave

    GameOver --> [*]
```

---

## 6. Component Dependency Graph

Which systems depend on which:

```mermaid
graph BT
    GM["GameManager"] --> WM["WaveManager"]
    GM --> SM["ScoreManager"]
    GM --> TM["ThemeManager"]
    
    WM --> ES["EnemySpawner"]
    WM --> BL["BehaviorLogger"]
    WM --> AC["AIClient"]
    
    ES --> SPR["SpawnPointRegistry"]
    ES --> EC["EnemyController"]
    
    AC --> SR["SafetyRails"]
    AC --> FP["FallbackProvider"]
    
    EC --> EH["EnemyHealth"]
    EC --> CONF["EnemyConfig"]
    
    PC["PlayerController"] --> PI["PlayerInput"]
    PC --> PCO["PlayerCombat"]
    
    PCO --> PROJ["Projectile"]
    PCO --> MD["MeleeDetector"]
    PCO --> SA["SpecialAbility"]
    
    PCO --> DS["DamageSystem"]
    EC --> DS
    
    DS --> BL
    DS --> SM
    
    BL --> AC
    
    TM --> LS["LocationService"]
    TM --> GS["GeocodeService"]
    
    HUD["HUDController"] --> SM
    HUD --> PC
    HUD --> WM
    
    TD["TauntDisplay"] --> AC
    GOS["GameOverScreen"] --> GM
```

---

## 7. Event System

Key game events and which systems listen to them:

| Event | Fired By | Listeners | Data |
|---|---|---|---|
| `OnEnemyKilled` | DamageSystem | BehaviorLogger, ScoreManager, WaveManager | `{enemyType, killType, distance, position}` |
| `OnPlayerDamaged` | DamageSystem | HUDController, PlayerController | `{damageAmount, remainingHP}` |
| `OnPlayerDeath` | PlayerController | GameManager, GameOverScreen | `{finalScore, waveReached}` |
| `OnWaveStart` | WaveManager | BehaviorLogger, HUDController | `{waveNumber, composition}` |
| `OnWaveEnd` | WaveManager | BehaviorLogger, AIClient | `{waveNumber}` |
| `OnAIResponseReceived` | AIClient | WaveManager, TauntDisplay | `{AIDecision}` |
| `OnThemeResolved` | ThemeManager | Arena (visual setup) | `{themeBucket}` |
| `OnScoreChanged` | ScoreManager | HUDController | `{newScore, delta}` |

---

## 8. Network Request Timeline

Typical game session showing all network calls:

```
Time (s)  Event                           Network Call
──────────────────────────────────────────────────────────
0.0       Game Start                      ─
0.1       Request Location                📍 Device GPS (local)
0.5       Location Received               ─
0.6       Reverse Geocode                 🌐 BigDataCloud (free, ~200ms)
0.8       Theme Applied                   ─
1.0       Wave 1 Starts                   ─
          ... combat (30-60s) ...
35.0      Wave 1 Ends                     ─
35.1      Behavior Summary Compiled       ─
35.2      AI Call                          🧠 Gemini Flash (~1s)
36.2      AI Response Received            ─
36.3      Taunt Displayed                 ─
39.0      Wave 2 Starts                   ─
          ... combat (30-60s) ...
75.0      Wave 2 Ends                     ─
75.2      AI Call                          🧠 Gemini Flash (~1s)
          ... repeat ...
          
Total API calls for a 10-wave session:
  📍 GPS: 1 (local)
  🌐 Geocode: 1
  🧠 Gemini: 10
  Total: 12 network requests
```

---

*This document visualizes every data pathway in Patient Zero. Use these diagrams as architecture reference during development and debugging.*
