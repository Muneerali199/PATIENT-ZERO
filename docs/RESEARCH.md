# 🔬 RESEARCH — Why Patient Zero Wins

> **R&D / Business Model / Player Psychology** | v1.0 | September 2026
> The evidence file: market proof, competitive moat, why players choose it, why judges fund it.

---

## 1. The Idea (one line)

> **A wave-survival shooter where the difficulty system is a live AI that profiles each player, remembers them between sessions, and writes their autopsy report when they die.**

Three claims, all verifiable in a 90-second demo:
1. **It studies you** — behavioral fingerprint → wave composition that counters YOUR habits
2. **It remembers you** — cross-session nemesis memory ("Specimen #7 returns")
3. **It reports you** — shareable AI-written autopsy card on death

---

## 2. Market Proof — This Genre Prints

Wave-survival arena shooters are one of the most validated indie genres of the decade:

| Title | Signal | What it proves for us |
|---|---|---|
| **Vampire Survivors** (poncle, 2022) | Millions of copies at ~$3, BAFTA **Best Game 2023**, spawned the entire "survivors-like" genre | Minimal art + deep systemic loop beats AAA polish. Our canvas aesthetic is a feature, not a compromise. |
| **Archero** (Habby, 2019) | Reported to cross nine figures in player spending (Sensor Tower coverage) | Mobile arena-shooter + short runs + meta progression = massive F2P economics. |
| **Survivor.io** (Habby, 2022) | Reported $100M+ player spending within its first year | Mobile wave-survival specifically works at scale. |
| **Brotato / 20 Minutes Till Dawn / Halls of Torment** | Each sold hundreds of thousands at $3–8 | The audience actively seeks new mechanical twists on the same loop. **Our twist is the AI.** |

**The gap in the market:** every one of these games has a *static* difficulty curve. Difficulty modding exists only as sliders (Halls of Torment's "agony") or authored pacing. **Nobody in the genre has an opponent that learns the player.** That is the open slot Patient Zero occupies.

### The AI-in-games moment

- Steam now requires AI-content disclosure — the market expects AI claims to be **substantive**, not cosmetic.
- Inworld AI raised ~$50M (reported, 2023) to put LLM NPCs in games — the industry is betting on exactly this direction, but almost all of it is **dialogue/chatbots**.
- Patient Zero's contrarian bet: **zero dialogue, zero prompts — the AI only acts through gameplay consequences.** In a field of chatbot demos, we are the only team whose AI cannot be reduced to "a wrapper."

---

## 3. Competitive Moat — The Patent Angle (our best R&D find)

> [!IMPORTANT]
> **Warner Bros. holds US Patent 10,926,179** on the Nemesis System (Shadow of Mordor's "enemies that remember you and evolve"). Granted 2021, enforceable for years. This is why **no studio has shipped a nemesis system since** — the canonical implementation is legally locked.

**Patient Zero routes around the patent entirely.** The patented system is a specific authored hierarchy (orc captains, power struggles, memory events). Ours is a fundamentally different mechanism: **an LLM reasoning over behavioral telemetry, composing counter-strategies from structured data.** Same player fantasy — *"the villain knows me"* — novel technical implementation, zero patent overlap, and (unlike Mordor's) it generalizes to any game with a behavior logger.

> Judge kill-shot: *"Warner Bros patented the nemesis system, which is why you've never seen another one. We found a different way to build that fantasy — with a language model instead of an authored state machine."*

### Prior art we studied (and where each stops)

| System | Mechanism | Where it stops | Our edge |
|---|---|---|---|
| **L4D AI Director** (2008) | Authored rules over group "stress" metric | Invisible, group-level, no memory, no learning | Individual fingerprint, visible reasoning, persistent memory |
| **Alien: Isolation** (2014) | Director + alien two-brain, behavior trees | Never learns; genius was authored | Strategy changes per player, per session |
| **Hello Neighbor** (2017) | Route memorization, trap placement | One behavior dimension | Full behavioral fingerprint → open-ended counters |
| **Shadow of Mordor Nemesis** (2014) | Authored hierarchy + memory | **Patented** | LLM route = novel mechanism, no patent overlap |

---

## 4. Why Players Choose It — Player Psychology

Mapped against Self-Determination Theory (the standard lens for game motivation):

| Need | How Patient Zero serves it |
|---|---|
| **Competence** | The game is an arms race: outsmarting the AI is the skill ceiling. Winning means *you out-adapted an adaptive opponent* — the strongest competence signal available. |
| **Autonomy** | Your playstyle literally authors the game. Two players get two different games. Player choice has *systemic* consequences, not cosmetic ones. |
| **Relatedness** | The autopsy card is a social object: "I got The Camper, adaptability 0.42 — what are you?" Wordle proved the mechanic; ours is a personality judgment, which is more shareable than a grid. |

Plus three deeper hooks:

1. **The nemesis effect** — personalization creates emotional stakes. Being *remembered* ("Specimen #7 returns. The north pillar missed you.") converts a mechanic into a relationship. Loss aversion + revenge: the AI taunted you, you want another run.
2. **Variable-ratio reinforcement with a face** — wave rewards are slot-machine-like, but attributed to a *character* with intent. Anthropomorphized randomness is stickier than anonymous randomness.
3. **Dying is content, not failure** — the autopsy reframes death as a deliverable. Every run produces an artifact worth sharing. Loss → artifact → social proof → new players.

### Session economics
2–4 minute runs, instant restart, portrait one-hand play — the hyper-casual session shape that Voodoo/Habby proved, but with an AI antagonist instead of disposable novelty.

---

## 5. Business Model

### Core loop: F2P mobile, zero-CAC distribution

```
Play → die → get autopsy card → share it → friends scan/play → they die → they share
         ▲                                                    │
         └──────────── k-factor viral loop ───────────────────┘
```

| Stream | Mechanic | Notes |
|---|---|---|
| **Rewarded ads** | "SECOND EXPOSURE" — watch to revive once per run | Highest-eCPM format (industry-typical $10–40 eCPM for rewarded video); placed at the emotional peak (death). Opt-in only. |
| **Interstitial ads** | After game over screen, capped 1 per 2 runs | Death is a natural break; never interrupts play. |
| **Cosmetic biome packs** | $0.99–2.99 arena themes (void, neon-Tokyo, abyssal…) | Zero gameplay advantage; pure identity. |
| **"Full Exposure" unlock** | $2.99 one-time: removes ads + specimen dossier (lifetime stats) | The dossier is the whale product — people pay for self-knowledge. |

### Cost structure (why this is viable)

- **Inference cost:** ~3 flash-tier calls per run (wave + greeting + autopsy) = fractions of a cent. A 10k-DAU game runs on pocket change.
- **Distribution cost:** the share loop is free UA. Wordle got acquired by the NYT (reported low seven figures) on the back of exactly one shareable daily artifact.
- **Data posture:** no PII collected. Location is bucketed on-device and never stored. Compliance is trivial — a real business advantage in 2026.

### The data flywheel (the real asset)

Every run produces a labeled behavioral trace: *player fingerprint → AI counter → outcome*. That dataset — which counters actually beat which playstyles — compounds. It tunes prompts, seeds archetypes, and eventually trains a distilled on-device model. **The more people play, the smarter the villain gets. That is the moat.**

---

## 6. Why Judges Pick It

| Judging criterion | Our answer |
|---|---|
| **AI integration depth** | AI runs difficulty, narrative voice, persistence, and the share artifact. Remove it and the game has no core loop. Zero chatbot. |
| **Real-world problem** | Adaptive adversarial AI = fraud bots, scam scripts, anti-cheat evasion. We make that abstract threat *visceral and playable* — and the autopsy shows players what a behavioral profile of them looks like. |
| **Technical execution** | Structured JSON I/O, hard 2.5s timeout, safety-rail clamping, deterministic local fallback brain — **the demo works with the network unplugged.** |
| **UX** | QR → playing in 5 seconds. Adaptation felt within 2–3 waves. Autopsy is screenshot-worthy. |
| **Presentation** | The demo ends by turning judges into specimens. Nobody else hands the game to the judges. |
| **Viability/business** | This document. Proven genre, zero-CAC loop, sub-cent inference, patent-free moat, franchise-able antagonist. |

---

## 7. Honest Risks

| Risk | Mitigation (already built) |
|---|---|
| LLM latency/failure | 2.5s hard timeout → local brain, player never sees a loading state |
| "Is it really AI?" | Specimen Mode shows reasoning live; local brain is itself a rule-based AI — adaptation is never faked, only *authored differently* |
| LLM outputs garbage | Safety rails clamp every field; invalid = fallback |
| Genre fatigue | Our differentiation is the opponent, not the loop — the one thing no competitor has |
| WB patent optics | Different mechanism (LLM over telemetry vs authored hierarchy); not a legal filing, but a clear technical distinction |

---

## 8. Post-Hackathon Roadmap (franchise path)

1. **Global Necrology** — live feed: *"Specimen #114 — The Sprinter — terminated at Wave 6"* (Supabase free tier)
2. **Daily Specimen** — one AI seed per day, everyone fights the same Patient Zero (Wordle model)
3. **Possession Mode** — PvP: one player IS Patient Zero, issuing directives the LLM executes
4. **Patient Zero as IP** — the antagonist character recurs across titles (franchise villain)
5. **On-device distillation** — the flywheel data trains a local model → zero inference cost at scale

---

## 9. The Elevator Paragraph

> Wave-survival shooters are a proven, nine-figure genre — and every one of them has a static difficulty curve. Meanwhile, Warner Bros. patented the only famous "enemy that remembers you" and locked it away. Patient Zero is the first game to deliver that fantasy through a fundamentally new mechanism: an LLM that profiles each player live, counters their habits, remembers them between sessions, and writes a shareable autopsy report when it kills them. It costs fractions of a cent per player, distributes itself through share cards, and — unlike every chatbot demo at this hackathon — it is a game that cannot exist without its AI.

---

*This document is the business case. [WINNING_PLAN.md](WINNING_PLAN.md) is the strategy. [PRD.md](PRD.md) is the product.*
