# ✅ TESTING — Testing & QA Checklist

> **Testing Document** | v1.0 | September 2026
> Systematic testing procedures for Patient Zero: Protocol — what to test, how, and when.

---

## 1. Testing Philosophy

> [!IMPORTANT]
> In a 24-hour hackathon, formal unit tests are a luxury. **Manual testing with a checklist** is the practical approach. Test early, test on device, test the demo flow.

### Testing Priority Order

1. **P0 — Demo-critical:** If this breaks during the demo, it's game over for the presentation.
2. **P1 — Core gameplay:** If this breaks, the game isn't fun or functional.
3. **P2 — Polish:** If this breaks, the game works but feels rough.

---

## 2. P0 — Demo-Critical Tests

These must pass before any demo attempt.

### 2.1 AI Adaptation Loop

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| D1 | AI call returns valid JSON | Complete Wave 1, check logs | JSON response with all required fields | ☐ |
| D2 | Taunt displays after wave | Complete Wave 1, watch HUD | Taunt text appears in bubble within 3 seconds | ☐ |
| D3 | Camping triggers counter-wave | Camp north pillar for Wave 1, ranged only | Wave 2: more tanky/fast enemies, north spawn bias | ☐ |
| D4 | AI timeout → fallback works | Disconnect WiFi mid-wave, complete wave | Next wave spawns from fallback, no crash, no freeze | ☐ |
| D5 | Taunt reads as clinical | Complete 3 waves, read all taunts | Taunts reference specific behavior, no generic villain lines | ☐ |
| D6 | Multiple waves work | Play through 5 waves | Each wave spawns, AI responds, game escalates | ☐ |

### 2.2 Location Seeding

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| D7 | Location permission granted | Fresh install, start game, grant permission | Arena theme matches city's bucket | ☐ |
| D8 | Location permission denied | Fresh install, start game, deny permission | Game starts with `temperate` theme, no crash | ☐ |
| D9 | Two different locations → two themes | Two devices with different cities (or mock locations) | Visibly different arena palettes | ☐ |

### 2.3 Game Flow

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| D10 | Game starts to game over | Play until death | Full loop: start → waves → death → game over screen | ☐ |
| D11 | Game over screen shows stats | Die after 3+ waves | Wave number, score, and restart button visible | ☐ |
| D12 | Restart works | Die, tap restart | New game starts fresh (wave 1, full HP, new AI session) | ☐ |

---

## 3. P1 — Core Gameplay Tests

### 3.1 Player Controls

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| G1 | Joystick movement | Touch and drag left side of screen | Player moves in joystick direction, smooth | ☐ |
| G2 | Joystick release → stop | Release joystick | Player stops immediately | ☐ |
| G3 | Fire button | Tap right-side fire button | Projectile fires in facing direction | ☐ |
| G4 | Fire hold → continuous | Hold fire button | Continuous fire at rate cap | ☐ |
| G5 | Special ability fire | Tap special button | AoE effect, damage to nearby enemies | ☐ |
| G6 | Special ability cooldown | Fire special, try again immediately | Button greyed out, second tap does nothing | ☐ |
| G7 | Special ability ready | Wait 15 seconds after using special | Button becomes active again, cooldown ring fills | ☐ |
| G8 | Movement + fire simultaneously | Move with joystick while tapping fire | Both work at the same time, no conflict | ☐ |

### 3.2 Combat

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| G9 | Ranged damage | Shoot an enemy | Enemy HP decreases, hit feedback visible | ☐ |
| G10 | Enemy death | Kill an enemy with ranged fire | Death effect plays, score increases, enemy removed | ☐ |
| G11 | Auto-melee trigger | Let enemy walk into proximity range | Melee attack triggers automatically, damage dealt | ☐ |
| G12 | Melee kill | Kill enemy via auto-melee | Death effect, score +25 bonus, logged as melee kill | ☐ |
| G13 | AoE kills multiple | Use special near 3+ enemies | All enemies in radius take damage | ☐ |
| G14 | Player takes damage | Let enemy attack player | HP bar decreases, damage feedback (shake/flash) | ☐ |
| G15 | Player death | Let HP reach 0 | Game over screen appears, game stops | ☐ |

### 3.3 Enemy Behavior

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| G16 | Enemies path toward player | Spawn a wave, don't move | Enemies walk toward player position | ☐ |
| G17 | Standard enemy speed | Observe standard zombie | Medium speed, reaches player in ~5 seconds from mid-range | ☐ |
| G18 | Fast enemy speed | Observe fast zombie | Noticeably faster than standard, smaller | ☐ |
| G19 | Tanky enemy durability | Shoot tanky zombie | Takes significantly more hits than standard | ☐ |
| G20 | Enemies attack in range | Let enemy reach melee range | Enemy attacks, player takes damage | ☐ |
| G21 | Three types visually distinct | Spawn all 3 types | Can clearly tell Standard vs Fast vs Tanky by sight | ☐ |

### 3.4 Wave System

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| G22 | Wave counter increments | Complete a wave | Counter increases by 1 | ☐ |
| G23 | All enemies must die | Kill all but 1 enemy | Wave doesn't end until last enemy dies | ☐ |
| G24 | Wave 1 uses location bias | Start game with known location | Wave 1 composition matches location bucket bias | ☐ |
| G25 | Wave 2+ uses AI composition | Complete Wave 1, check Wave 2 | Wave 2 spawns according to AI response (check logs) | ☐ |
| G26 | Spawn zone bias works | AI returns "north_chokepoint" bias | Most enemies spawn from north zone | ☐ |

---

## 4. P2 — Polish Tests

### 4.1 UI/HUD

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| P1 | HP bar updates smoothly | Take damage | HP bar lerps down, not jumps | ☐ |
| P2 | Score pops on increment | Kill an enemy | Score number does a brief scale-up animation | ☐ |
| P3 | Taunt typewriter effect | Complete a wave | Taunt text types in character-by-character | ☐ |
| P4 | Taunt auto-dismiss | Wait after taunt appears | Taunt fades out after ~4 seconds | ☐ |
| P5 | Cooldown ring fills | Use special ability, watch button | Ring fills clockwise over 15 seconds | ☐ |
| P6 | Low HP warning | Get below 25% HP | Red vignette appears, heartbeat sound | ☐ |

### 4.2 Visual Effects

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| P7 | Screen shake on damage | Get hit by enemy | Brief camera shake | ☐ |
| P8 | Hit particles on enemy | Shoot an enemy (don't kill) | Particle burst at hit point | ☐ |
| P9 | Death effect on enemy | Kill an enemy | Scale-down/dissolve/particle death effect | ☐ |
| P10 | AoE visual effect | Use special ability | Expanding ring/blast visual | ☐ |

### 4.3 Audio

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| P11 | Gunshot sound | Fire weapon | Short, punchy sound plays | ☐ |
| P12 | Enemy death sound | Kill enemy | Collapse/impact sound plays | ☐ |
| P13 | Wave start sound | New wave begins | Alert/chime sound plays | ☐ |
| P14 | No audio overlap/clipping | Kill 5 enemies rapidly | Sounds don't distort or stack badly | ☐ |

### 4.4 Theme Variants

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| P15 | Coastal theme | Set location to Mumbai | Blue-grey palette, dark water tones | ☐ |
| P16 | Desert theme | Set location to Jaipur | Orange haze, sand tones | ☐ |
| P17 | Temperate theme (default) | Deny location permission | Green-grey, overcast | ☐ |
| P18 | Mountain theme | Set location to Shimla | White fog, cold blue tones | ☐ |
| P19 | Urban theme | Set location to Hyderabad | Dark concrete, neon accents | ☐ |

---

## 5. Edge Case Tests

| # | Test | Steps | Expected Result | Pass? |
|---|---|---|---|---|
| E1 | Die during Wave 1 | Stand still, let enemies kill you | Game over screen works, no crash | ☐ |
| E2 | AI returns 0 for a type | Mock AI response with `"fast": 0` | Safety rail clamps to 1, wave spawns normally | ☐ |
| E3 | AI returns 100 for a type | Mock AI response with `"standard": 100` | Safety rail clamps to 15, total capped at 30 | ☐ |
| E4 | AI returns invalid zone | Mock AI response with `"spawn_zone_bias": "invalid"` | Defaults to "balanced", no crash | ☐ |
| E5 | AI returns empty taunt | Mock AI response with `"taunt_text": ""` | Displays fallback `"..."` or `"Recalculating..."` | ☐ |
| E6 | AI returns non-JSON | Mock API to return plain text | Fallback composition used, no crash | ☐ |
| E7 | Network drops mid-game | Disconnect WiFi after Wave 3 | Fallback compositions continue the game | ☐ |
| E8 | Rapid wave completion | Use AoE to instantly clear waves | Game handles fast wave transitions without bugs | ☐ |
| E9 | 0 kills in a wave (impossible?) | — | Logger handles 0 kills gracefully (no divide-by-zero) | ☐ |

---

## 6. Performance Tests

| # | Test | Device | Metric | Target | Pass? |
|---|---|---|---|---|---|
| F1 | Frame rate during combat | Demo phone | FPS | ≥ 30 FPS | ☐ |
| F2 | Frame rate with 20+ enemies | Demo phone | FPS | ≥ 25 FPS | ☐ |
| F3 | AI call latency | Demo phone on demo WiFi | Seconds | < 2.0s | ☐ |
| F4 | Game start time | Demo phone | Seconds | < 5s to first wave | ☐ |
| F5 | Memory usage | Demo phone | MB | < 500 MB | ☐ |
| F6 | APK / build size | Build output | MB | < 100 MB | ☐ |

---

## 7. Testing Schedule

| Time (Hours In) | What to Test | Priority |
|---|---|---|
| Hour 4 | G1–G2 (controls), basic movement | P1 |
| Hour 9 | G9–G15 (combat), G16–G21 (enemies) | P1 |
| Hour 13 | G22–G26 (waves), D1 (AI call) | P0/P1 |
| Hour 17 | D1–D6 (AI loop), D7–D9 (location) | P0 |
| Hour 19 | D10–D12 (full flow), E1–E9 (edge cases) | P0 |
| Hour 21 | P1–P19 (polish), F1–F6 (performance) | P2 |
| Hour 23 | Full demo rehearsal (all D tests again) | P0 |

---

## 8. Bug Severity Classification

| Severity | Definition | Action |
|---|---|---|
| 🔴 **Critical** | Demo will fail (crash, freeze, AI never responds) | Fix immediately, drop other tasks |
| 🟡 **Major** | Game works but looks broken (UI overflow, wrong theme, no sound) | Fix if time allows before demo |
| 🟢 **Minor** | Cosmetic issue (slight misalignment, imperfect animation) | Note for post-hackathon, don't fix now |
| ⚪ **Won't Fix** | Known issue that doesn't affect demo | Document and move on |

---

*Print this checklist. Test systematically. Focus on P0 tests first — if those pass, the demo will succeed.*
