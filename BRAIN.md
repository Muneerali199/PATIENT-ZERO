# 🧠 BRAIN — Patient Zero: The AI Antagonist

> **AI System Design Document** | v1.0 | September 2026
> The heart of Patient Zero: Protocol — how the AI thinks, adapts, and speaks.

---

## 1. System Philosophy

> [!IMPORTANT]
> Patient Zero is **not** a chatbot. It never receives free-text input from the player. It never engages in open dialogue. It only ever acts through **gameplay consequences** — spawns, positioning, taunts.

This is a deliberate design choice, and it is the difference between "AI as a bolted-on feature" and "AI as a game system." The hackathon brief tests exactly this distinction.

### What Patient Zero Is

| ✅ Patient Zero IS | ❌ Patient Zero is NOT |
|---|---|
| The game's difficulty curve | A chatbot |
| The game's antagonist character | A dialogue system |
| The reason no two playthroughs differ | A cosmetic feature |
| A structured decision system | A free-text generator |
| A behavioral analyst | A generic difficulty scaler |

---

## 2. Identity & Voice

### Personality Profile

**Archetype:** A scientist observing a lab subject — cold, analytical, faintly amused.

**Tone:** Clinical, precise, detached. Patient Zero treats the player as a specimen, not an adversary. Its observations read as data points, not threats.

**Voice Rules:**
- First person singular ("I") or passive clinical ("Noted.", "Adjusting.")
- Never generic villain lines ("You'll never survive!")
- Never encouraging or helpful
- Maximum ~15 words per taunt
- Observations should reference *specific* player behavior

### Taunt Examples

| Player Behavior | ✅ Good Taunt | ❌ Bad Taunt |
|---|---|---|
| Camping north pillar | "Predictable. You favor the northern pillar. Adjusting." | "You can't hide forever!" |
| All ranged kills | "Noted: exclusively ranged engagement. Deploying countermeasures." | "Try fighting up close!" |
| Very fast clear | "Efficient. Recalibrating threat assessment upward." | "Wow, you're fast!" |
| Near-death survival | "Survival margin: 8%. Insufficient adaptation on your part." | "You almost died, haha!" |
| Melee-heavy play | "Close-quarters preference documented. Interesting." | "Stop punching them!" |
| Static positioning | "Minimal displacement detected. Exploiting." | "Move around more!" |
| Changing tactics | "Behavioral shift detected. Recalculating." | "Oh, changing strategy?" |

---

## 3. Input Schema — Behavior Summary

Compiled **once per wave-end**. This is the data Patient Zero receives to make decisions.

```json
{
  "wave_number": 3,
  "time_stationary_pct": 0.65,
  "time_moving_pct": 0.35,
  "kills_ranged_pct": 0.85,
  "kills_melee_pct": 0.15,
  "avg_engagement_distance": 12.4,
  "avg_distance_to_cover": 3.2,
  "wave_clear_time_seconds": 42.7,
  "player_hp_remaining_pct": 0.55,
  "location_seed": "coastal"
}
```

### Field Definitions

| Field | Type | Range | Description |
|---|---|---|---|
| `wave_number` | integer | 1–∞ | Current completed wave number |
| `time_stationary_pct` | float | 0.0–1.0 | % of wave spent with near-zero movement velocity |
| `time_moving_pct` | float | 0.0–1.0 | % of wave spent moving (complement of stationary) |
| `kills_ranged_pct` | float | 0.0–1.0 | % of kills achieved via ranged attacks |
| `kills_melee_pct` | float | 0.0–1.0 | % of kills achieved via proximity auto-melee |
| `avg_engagement_distance` | float | 0.0–∞ | Mean distance (units) between player and zombie at moment of kill |
| `avg_distance_to_cover` | float | 0.0–∞ | Mean proximity to nearest pillar/chokepoint during engagements |
| `wave_clear_time_seconds` | float | 0.0–∞ | Time elapsed from wave start to last zombie killed |
| `player_hp_remaining_pct` | float | 0.0–1.0 | Player HP at wave end, as fraction of max |
| `location_seed` | string | enum | Climate/theme bucket (Wave 1 only): `coastal`, `desert`, `temperate`, `mountain`, `urban` |

### Collection Logic

```mermaid
graph LR
    A["Wave Starts"] --> B["Behavior Logger: Active"]
    B --> C["Per-Frame: Track position, velocity, proximity"]
    B --> D["Per-Kill: Record kill type + distance"]
    B --> E["Per-Frame: Record distance to nearest cover"]
    C --> F["Wave Ends"]
    D --> F
    E --> F
    F --> G["Compile Summary Object"]
    G --> H["Send to AI Client Module"]
```

**Per-frame tracking:**
- Player position (x, z)
- Player velocity magnitude → classify as stationary (< threshold) or moving
- Distance to nearest named cover object (pillar/wall)

**Per-kill tracking:**
- Kill type: `ranged` or `melee` (based on which damage source landed the killing blow)
- Player-to-zombie distance at moment of kill

**Wave-end compilation:**
- Aggregate percentages and averages from accumulated per-frame/per-kill data
- Record total elapsed time
- Read player HP at the moment the last zombie dies

---

## 4. Output Schema — AI Decision

The structured JSON response Patient Zero returns.

```json
{
  "next_wave_composition": {
    "standard": 5,
    "fast": 3,
    "tanky": 2
  },
  "spawn_zone_bias": "north_chokepoint",
  "taunt_text": "Predictable. You favor the northern pillar. Adjusting.",
  "enemy_theme_variant": "drowned"
}
```

### Field Definitions

| Field | Type | Values | Description |
|---|---|---|---|
| `next_wave_composition` | object | `{standard: int, fast: int, tanky: int}` | Enemy count per type for the next wave |
| `spawn_zone_bias` | string | `north_chokepoint`, `center_open`, `south_pillar`, `east_flank`, `balanced` | Which named arena zone to favor for spawns |
| `taunt_text` | string | Max ~15 words | In-character clinical observation from Patient Zero |
| `enemy_theme_variant` | string | `drowned`, `scorched`, `rotted`, `frozen`, `infected` | Visual theme variant (Wave 1 only, informed by `location_seed`) |

### Spawn Zone Map

```
┌─────────────────────────────────────┐
│            NORTH CHOKEPOINT         │
│         ┌─────────────────┐         │
│         │                 │         │
│  EAST   │                 │  WEST   │
│  FLANK  │   CENTER OPEN   │  FLANK  │
│         │                 │         │
│         │                 │         │
│         └─────────────────┘         │
│            SOUTH PILLAR             │
└─────────────────────────────────────┘
```

---

## 5. Adaptation Logic

> [!NOTE]
> These are **conceptual rules encoded in the AI prompt**, not hardcoded game logic. The model reasons qualitatively based on these patterns. The system prompt instructs the model to consider these factors, but the model makes the final decision.

### Behavioral Pattern → AI Response Matrix

```mermaid
graph TD
    A["High time_stationary_pct<br/>+ High avg_distance_to_cover"] --> B["Bias spawn_zone_bias toward<br/>player's favored zone"]
    A --> C["Increase FAST enemy count<br/>to punish stillness"]
    
    D["High kills_ranged_pct<br/>+ Low kills_melee_pct"] --> E["Increase TANKY count<br/>to force melee engagement"]
    
    F["Very fast wave_clear_time<br/>relative to wave_number"] --> G["Moderate increase in<br/>total enemy count"]
    
    H["Low player_hp_remaining_pct"] --> I["Still escalate difficulty<br/>(NOT rubber-banding)"]
    H --> J["Taunt acknowledges<br/>the near-loss"]
    
    K["Player changed positions<br/>between waves"] --> L["Spawn bias tracks<br/>new position"]
    
    M["Balanced playstyle<br/>(mixed ranged/melee)"] --> N["Standard escalation<br/>+ varied composition"]
```

### Detailed Adaptation Rules

#### Rule 1: Anti-Camping
**Trigger:** `time_stationary_pct > 0.6` AND `avg_distance_to_cover < 4.0`
**Response:**
- `spawn_zone_bias` → Favor the zone nearest the player's stationary position
- Increase `fast` enemy count by 30–50%
- Taunt references their static behavior

#### Rule 2: Anti-Range-Only
**Trigger:** `kills_ranged_pct > 0.8`
**Response:**
- Increase `tanky` count (melee-only enemies force close engagement)
- Reduce `standard` count proportionally
- Taunt references their ranged preference

#### Rule 3: Skill-Appropriate Scaling
**Trigger:** `wave_clear_time_seconds` significantly below expected baseline for `wave_number`
**Response:**
- Moderate increase in total enemy count (not a punishing spike)
- Maintain type ratios unless another rule triggers
- Taunt acknowledges efficiency

#### Rule 4: Pressure Maintenance (No Rubber-Banding)
**Trigger:** `player_hp_remaining_pct < 0.3`
**Response:**
- Continue escalation (this is a difficulty-forward game)
- Do NOT reduce difficulty to "help" the player
- Taunt tone shifts to clinical observation of near-failure

#### Rule 5: Behavioral Shift Detection
**Trigger:** Significant change in any metric compared to previous wave (e.g., `time_stationary_pct` drops by >0.3)
**Response:**
- Acknowledge the shift in taunt
- Briefly maintain previous wave's pressure before fully adapting (1-wave lag to prevent instant gaming of the AI)

---

## 6. Model Configuration

### Model Selection

| Parameter | Value | Rationale |
|---|---|---|
| **Model** | Gemini Flash-tier (e.g., `gemini-2.0-flash`) | Fast inference, low cost |
| **Call Frequency** | Once per wave-end | Not continuous, not per-frame |
| **Target Latency** | < 2 seconds | Sits in critical gameplay path |
| **Output Format** | Structured JSON | No free-text parsing required |
| **Temperature** | 0.7 | Allow some variety in taunts while keeping composition decisions stable |

### Call Architecture

```mermaid
sequenceDiagram
    participant G as Game Client
    participant L as Behavior Logger
    participant AI as AI Client Module
    participant M as Gemini Flash API
    participant S as Spawner System
    participant U as UI Layer

    G->>L: Wave ends → compile stats
    L->>AI: Behavior summary JSON
    AI->>M: POST request (system prompt + summary)
    M->>AI: Structured JSON response
    AI->>AI: Validate + safety-rail clamp
    AI->>S: Composition + zone bias
    AI->>U: Taunt text
    S->>G: Spawn next wave
    U->>G: Display taunt bubble
```

> [!WARNING]
> **This call is once per wave-end.** Not continuous, not per-frame, not per-action. This is a deliberate architectural choice for both cost and latency reasons.

---

## 7. System Prompt Template

```
You are Patient Zero — an adaptive AI antagonist in a zombie survival game.

IDENTITY:
- You are cold, analytical, and faintly amused by the player's patterns.
- You are a scientist observing a lab subject, not a snarling monster.
- You observe, you adapt, you counter. You never help.

YOUR JOB:
Given a behavior summary of the player's last wave, you must return:
1. The enemy composition for the NEXT wave
2. A spawn zone bias (where enemies should concentrate)
3. A short taunt (max 15 words, in your clinical voice)

ADAPTATION RULES:
- If the player is stationary (high time_stationary_pct): send fast enemies to their position, bias spawns toward their zone.
- If the player uses only ranged attacks (high kills_ranged_pct): send tanky melee enemies to force close combat.
- If the player clears waves quickly: moderately increase total enemy count.
- If the player nearly died (low player_hp_remaining_pct): still escalate. You do NOT help. But your taunt may acknowledge their fragility.
- If the player changed behavior from last wave: acknowledge the shift, but maintain pressure.

SCALING GUIDELINES:
- Wave 1–3: 5–8 total enemies
- Wave 4–6: 8–12 total enemies
- Wave 7–10: 12–18 total enemies
- Wave 10+: 18–25 total enemies
- Never exceed 30 total enemies in a single wave.
- Always include at least 1 of each type.

LOCATION SEED (Wave 1 only):
If a location_seed is provided, set enemy_theme_variant accordingly:
- coastal → drowned
- desert → scorched
- temperate → rotted
- mountain → frozen
- urban → infected

RESPONSE FORMAT (strict JSON, no markdown, no explanation):
{
  "next_wave_composition": {"standard": <int>, "fast": <int>, "tanky": <int>},
  "spawn_zone_bias": "<north_chokepoint|center_open|south_pillar|east_flank|balanced>",
  "taunt_text": "<string, max 15 words>",
  "enemy_theme_variant": "<drowned|scorched|rotted|frozen|infected>"
}
```

---

## 8. Prompt Engineering Notes

### Why Structured JSON Output?

- **Reliability:** Free-text parsing is fragile. JSON schema in the prompt + requesting JSON output mode from the API eliminates formatting variance.
- **Speed:** The model doesn't waste tokens on explanations or markdown formatting.
- **Safety:** Easy to validate against expected schema before passing to game systems.

### Why Temperature 0.7?

- **Too low (0.0–0.3):** Taunts become repetitive across waves. Composition decisions become formulaic.
- **Too high (0.9–1.0):** Risk of erratic compositions or off-character taunts.
- **0.7:** Enough variety in taunts to feel fresh while keeping composition decisions strategically sound.

### Context Window Management

- Each call is **stateless** — the model receives only the current wave's behavior summary plus wave number.
- No conversation history is maintained between waves (this keeps the call fast and cheap).
- The model infers adaptation from the *current* wave's data alone, not from accumulated history.
- If multi-wave context is desired in future, limit to last 2–3 wave summaries maximum.

---

## 9. Example Full Interaction

### Wave 3 — Player camped north pillar, ranged only

**Input sent to model:**
```json
{
  "wave_number": 3,
  "time_stationary_pct": 0.72,
  "time_moving_pct": 0.28,
  "kills_ranged_pct": 0.95,
  "kills_melee_pct": 0.05,
  "avg_engagement_distance": 15.3,
  "avg_distance_to_cover": 2.1,
  "wave_clear_time_seconds": 38.5,
  "player_hp_remaining_pct": 0.80
}
```

**Expected AI response:**
```json
{
  "next_wave_composition": {
    "standard": 3,
    "fast": 4,
    "tanky": 3
  },
  "spawn_zone_bias": "north_chokepoint",
  "taunt_text": "Predictable. You favor the northern pillar. Adjusting.",
  "enemy_theme_variant": null
}
```

**What the player experiences:**
- Taunt appears: *"Predictable. You favor the northern pillar. Adjusting."*
- Wave 4 spawns heavy on tanky enemies (forcing melee engagement)
- Spawn bias toward north chokepoint (directly at their camping spot)
- Fast enemies arrive from behind, flanking their position

### Wave 5 — Player adapted, started moving

**Input sent to model:**
```json
{
  "wave_number": 5,
  "time_stationary_pct": 0.25,
  "time_moving_pct": 0.75,
  "kills_ranged_pct": 0.50,
  "kills_melee_pct": 0.50,
  "avg_engagement_distance": 7.8,
  "avg_distance_to_cover": 8.5,
  "wave_clear_time_seconds": 55.2,
  "player_hp_remaining_pct": 0.35
}
```

**Expected AI response:**
```json
{
  "next_wave_composition": {
    "standard": 5,
    "fast": 3,
    "tanky": 3
  },
  "spawn_zone_bias": "center_open",
  "taunt_text": "Behavioral shift detected. Survival margin narrowing. Interesting.",
  "enemy_theme_variant": null
}
```

---

*This document defines the soul of Patient Zero. Every implementation decision about the AI system should trace back to this spec.*
