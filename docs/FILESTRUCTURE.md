# 📁 FILESTRUCTURE — Project File Organization

> **File Structure Reference** | v1.0 | September 2026
> Recommended project organization for Patient Zero: Protocol.

---

## 1. Unity Project Structure

```
PATIENT-ZERO/
├── 📄 README.md                          # Project overview & quickstart
├── 📄 PRD.md                             # Product Requirements Document
├── 📄 BRAIN.md                           # AI System Design (Patient Zero)
├── 📄 PLAN.md                            # Implementation Plan & Timeline
├── 📄 TECHSTACK.md                       # Technical Architecture
├── 📄 GAMEDESIGN.md                      # Game Design Document
├── 📄 SAFETY.md                          # Safety Rails & Failure Handling
├── 📄 DEMO.md                            # Demo Script & Presentation Guide
├── 📄 PROMPTS.md                         # AI Prompt Engineering Reference
├── 📄 FILESTRUCTURE.md                   # This document
├── 📄 .gitignore                         # Git ignore rules
│
├── 📁 Assets/
│   ├── 📁 Scripts/
│   │   ├── 📁 Player/
│   │   │   ├── PlayerController.cs       # Movement, HP, death
│   │   │   ├── PlayerInput.cs            # Touch input processing
│   │   │   ├── VirtualJoystick.cs        # Joystick UI component
│   │   │   └── PlayerCombat.cs           # Ranged fire, melee, special
│   │   │
│   │   ├── 📁 Enemy/
│   │   │   ├── EnemyController.cs        # Base enemy behavior (single class)
│   │   │   ├── EnemyConfig.cs            # ScriptableObject for type stats
│   │   │   └── EnemyHealth.cs            # HP, damage, death events
│   │   │
│   │   ├── 📁 Combat/
│   │   │   ├── Projectile.cs             # Ranged projectile behavior
│   │   │   ├── MeleeDetector.cs          # Proximity auto-melee trigger
│   │   │   ├── SpecialAbility.cs         # AoE cooldown ability
│   │   │   └── DamageSystem.cs           # Central damage calculation
│   │   │
│   │   ├── 📁 Wave/
│   │   │   ├── WaveManager.cs            # Wave lifecycle state machine
│   │   │   ├── EnemySpawner.cs           # Instantiate enemies per composition
│   │   │   ├── SpawnPointRegistry.cs     # Maps zone names to spawn transforms
│   │   │   └── WaveConfig.cs             # Wave data (composition, zone bias)
│   │   │
│   │   ├── 📁 AI/
│   │   │   ├── BehaviorLogger.cs         # Per-frame/per-kill stat tracking
│   │   │   ├── BehaviorSummary.cs        # Data class for AI input schema
│   │   │   ├── AIClient.cs               # Gemini API HTTP client
│   │   │   ├── AIDecision.cs             # Data class for AI output schema
│   │   │   ├── SafetyRails.cs            # Output clamping & validation
│   │   │   └── FallbackProvider.cs       # Deterministic fallback compositions
│   │   │
│   │   ├── 📁 Location/
│   │   │   ├── LocationService.cs        # Device geolocation wrapper
│   │   │   ├── GeocodeService.cs         # Reverse geocode (API + local lookup)
│   │   │   ├── ThemeManager.cs           # Apply theme bucket to arena
│   │   │   └── ThemeBucketData.cs        # Theme bucket definitions
│   │   │
│   │   ├── 📁 UI/
│   │   │   ├── HUDController.cs          # HP bar, wave counter, score
│   │   │   ├── TauntDisplay.cs           # Typewriter text, glitch effect
│   │   │   ├── GameOverScreen.cs         # Final score, restart button
│   │   │   └── CooldownRing.cs           # Special ability cooldown visual
│   │   │
│   │   └── 📁 Core/
│   │       ├── GameManager.cs            # Top-level game state & flow
│   │       ├── ScoreManager.cs           # Point tracking & display
│   │       └── Constants.cs              # Game-wide constants & config
│   │
│   ├── 📁 Prefabs/
│   │   ├── Player.prefab                 # Player character
│   │   ├── Enemy_Standard.prefab         # Standard zombie (uses EnemyConfig)
│   │   ├── Enemy_Fast.prefab             # Fast zombie (uses EnemyConfig)
│   │   ├── Enemy_Tanky.prefab            # Tanky zombie (uses EnemyConfig)
│   │   ├── Projectile.prefab            # Ranged projectile
│   │   └── AoE_Effect.prefab            # Special ability visual effect
│   │
│   ├── 📁 ScriptableObjects/
│   │   ├── EnemyConfig_Standard.asset   # Standard zombie stats
│   │   ├── EnemyConfig_Fast.asset       # Fast zombie stats
│   │   ├── EnemyConfig_Tanky.asset      # Tanky zombie stats
│   │   ├── Theme_Coastal.asset          # Coastal arena theme
│   │   ├── Theme_Desert.asset           # Desert arena theme
│   │   ├── Theme_Temperate.asset        # Temperate arena theme
│   │   ├── Theme_Mountain.asset         # Mountain arena theme
│   │   └── Theme_Urban.asset            # Urban arena theme
│   │
│   ├── 📁 Materials/
│   │   ├── Floor_Default.mat
│   │   ├── Wall_Default.mat
│   │   ├── Pillar_Default.mat
│   │   ├── Player_Mat.mat
│   │   ├── Enemy_Standard_Mat.mat
│   │   ├── Enemy_Fast_Mat.mat
│   │   └── Enemy_Tanky_Mat.mat
│   │
│   ├── 📁 Scenes/
│   │   └── GameScene.unity              # Single game scene
│   │
│   ├── 📁 UI/
│   │   ├── HUD.prefab                   # In-game HUD canvas
│   │   ├── GameOver.prefab              # Game over overlay
│   │   ├── Joystick.prefab             # Virtual joystick
│   │   └── Fonts/
│   │       └── JetBrainsMono-Regular.ttf # Terminal font for taunts
│   │
│   ├── 📁 Audio/
│   │   ├── sfx_shoot.wav
│   │   ├── sfx_hit.wav
│   │   ├── sfx_melee.wav
│   │   ├── sfx_enemy_death.wav
│   │   ├── sfx_player_damage.wav
│   │   ├── sfx_aoe_blast.wav
│   │   ├── sfx_wave_start.wav
│   │   ├── sfx_wave_clear.wav
│   │   ├── sfx_taunt_appear.wav
│   │   ├── sfx_game_over.wav
│   │   └── ambient_loop.wav
│   │
│   ├── 📁 Particles/
│   │   ├── HitEffect.prefab
│   │   ├── DeathEffect.prefab
│   │   └── AoEBlast.prefab
│   │
│   └── 📁 Config/
│       ├── ai_config.json               # API endpoint, model name, temperature
│       ├── system_prompt.txt             # Patient Zero system prompt (loaded at runtime)
│       └── city_lookup.json             # Hardcoded city → theme bucket table
│
├── 📁 prompts/                          # Prompt version history
│   ├── v1.0.txt
│   └── v1.1.txt
│
└── 📁 docs/
    └── demo_backup_video.mp4            # Pre-recorded demo backup
```

---

## 2. Web-Based Project Structure (Alternative)

```
PATIENT-ZERO/
├── 📄 README.md
├── 📄 PRD.md / BRAIN.md / PLAN.md / etc.
├── 📄 package.json
├── 📄 vite.config.ts
├── 📄 tsconfig.json
├── 📄 index.html
│
├── 📁 src/
│   ├── 📄 main.ts                       # Entry point
│   ├── 📄 game.ts                       # Game loop, state management
│   │
│   ├── 📁 player/
│   │   ├── controller.ts
│   │   ├── input.ts
│   │   └── combat.ts
│   │
│   ├── 📁 enemy/
│   │   ├── controller.ts
│   │   ├── config.ts
│   │   └── types.ts
│   │
│   ├── 📁 combat/
│   │   ├── projectile.ts
│   │   ├── melee.ts
│   │   └── special.ts
│   │
│   ├── 📁 wave/
│   │   ├── manager.ts
│   │   ├── spawner.ts
│   │   └── config.ts
│   │
│   ├── 📁 ai/
│   │   ├── logger.ts
│   │   ├── client.ts
│   │   ├── safety.ts
│   │   ├── fallback.ts
│   │   └── types.ts
│   │
│   ├── 📁 location/
│   │   ├── service.ts
│   │   ├── geocode.ts
│   │   └── themes.ts
│   │
│   ├── 📁 ui/
│   │   ├── hud.ts
│   │   ├── taunt.ts
│   │   ├── gameover.ts
│   │   └── joystick.ts
│   │
│   ├── 📁 config/
│   │   ├── constants.ts
│   │   ├── ai-config.ts
│   │   └── city-lookup.ts
│   │
│   └── 📁 assets/
│       ├── 📁 audio/
│       ├── 📁 textures/
│       └── 📁 fonts/
│
├── 📁 public/
│   └── 📄 manifest.json                # PWA manifest
│
└── 📁 prompts/
    ├── v1.0.txt
    └── v1.1.txt
```

---

## 3. Key File Descriptions

| File/Folder | Purpose | When to Touch |
|---|---|---|
| `GameManager.cs` / `game.ts` | Top-level game state, coordinates all systems | Early in Phase 1, updated throughout |
| `BehaviorLogger.cs` / `logger.ts` | Tracks all player metrics per wave | Phase 3, rarely changes after |
| `AIClient.cs` / `client.ts` | Makes API calls to Gemini | Phase 4, tune for latency |
| `SafetyRails.cs` / `safety.ts` | Clamps and validates AI responses | Phase 4, adjust bounds as needed |
| `system_prompt.txt` | Patient Zero's personality and rules | Phase 4, iterate frequently |
| `WaveManager.cs` / `manager.ts` | Wave lifecycle state machine | Phase 3, core game loop |
| `ThemeManager.cs` / `themes.ts` | Arena visual theming from location | Phase 5 |
| `TauntDisplay.cs` / `taunt.ts` | Typewriter taunt text UI | Phase 6 |

---

*This structure is a recommendation. Adapt folder naming to your team's conventions, but maintain the logical separation between systems.*
