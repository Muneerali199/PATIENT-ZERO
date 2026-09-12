# 🧟 Patient Zero: Protocol

> *A zombie shooter where the villain is a live AI that profiles YOU — it studies your habits, remembers you between sessions, and when you die, it writes your autopsy report.*

[![Hackathon](https://img.shields.io/badge/KICKR_Codemania-2026-blueviolet)]()
[![Category](https://img.shields.io/badge/Category-Gaming_×_AI-blue)]()
[![Build](https://img.shields.io/badge/Build_Window-24_Hours-orange)]()
[![Platform](https://img.shields.io/badge/Platform-Mobile_Web_PWA-green)]()

---

## 💀 What Is Patient Zero?

**Patient Zero: Protocol** is a mobile wave-based zombie survival shooter where the entire difficulty system, enemy design, and narrative voice is driven by a live AI antagonist — **Patient Zero** — that reads how each specific player fights and adapts every subsequent wave to counter them, seeded at game-start by the player's real-world location.

> *"The AI doesn't assist the game. The AI IS the game."*

**Camp a corner?** Patient Zero sends fast flankers to your position.
**Only shoot from range?** It deploys melee-forcing tanky enemies.
**Clear waves too quickly?** It scales enemy counts to match your skill.
**Nearly die?** It doesn't ease up — it notes your fragility and presses harder.

Every playthrough is different because every player plays differently, and Patient Zero adapts to YOU.

---

## 🎯 Core Design Philosophy

**One AI system, doing one job, visibly and mechanically.**

Patient Zero is not a chatbot. Not a dialogue system. Not a cosmetic feature. It only expresses itself through **gameplay consequences** — enemy composition, spawn positioning, and short clinical observations called "taunts."

Remove the AI, and the game has no difficulty curve, no narrative voice, no replayability.

---

## 🌍 Real-World Problem

Adaptive adversarial AI is a real, growing category — fraud bots, anti-cheat evasion, phishing systems, market manipulation algorithms. Patient Zero is a **playable demonstration** of what it feels like to be on the receiving end of an opponent that studies and counters you specifically.

> *"Every adaptive AI system that opposes people in the real world — a scam bot, an evasive attacker, a manipulative algorithm — behaves like Patient Zero. We built a game that makes that abstract threat concrete and playable."*

---

## 📚 Documentation

| Document | Description |
|---|---|
| [🏆 WINNING_PLAN.md](WINNING_PLAN.md) | **The strategy — USP, references, upgrades, demo plan. Read first.** |
| [📋 PRD.md](PRD.md) | Product Requirements — what the game is and why |
| [🧠 BRAIN.md](BRAIN.md) | AI System Design — how Patient Zero thinks and adapts |
| [📐 PLAN.md](PLAN.md) | Implementation Plan — 24-hour build timeline and features |
| [🔧 TECHSTACK.md](TECHSTACK.md) | Technical Architecture — system components and data flow |
| [🎮 GAMEDESIGN.md](GAMEDESIGN.md) | Game Design — arena, combat, enemies, scoring, juice |
| [🛡️ SAFETY.md](SAFETY.md) | Safety Rails — failure handling and demo safeguards |
| [🎤 DEMO.md](DEMO.md) | Demo Guide — 90-second presentation script |
| [🤖 PROMPTS.md](PROMPTS.md) | Prompt Engineering — system prompts and tuning guide |
| [🔌 APIS.md](APIS.md) | External APIs — all free, no credit card required |
| [📁 FILESTRUCTURE.md](FILESTRUCTURE.md) | File Structure — recommended project organization |

---

## 🎮 How It Works

```
Player starts → Location read → Arena themed to your city
  → Wave 1 plays out → You fight zombies
  → Wave ends → AI analyzes YOUR specific behavior
  → AI returns: new enemy mix + spawn position + clinical taunt
  → Safety rails applied → Next wave spawns to counter YOUR playstyle
  → Loop until death
```

### The AI Loop

1. **You play.** The game tracks everything: where you stand, how you kill, where you hide.
2. **Wave ends.** Your behavior is compiled into a structured summary.
3. **Patient Zero analyzes.** One API call to Gemini Flash with your behavior data.
4. **It adapts.** Returns enemy composition, spawn positioning, and a taunt.
5. **You feel it.** The next wave is designed to counter YOUR specific patterns.

---

## 🏗️ Tech Stack

| Component | Technology |
|---|---|
| Engine | Unity (or Three.js for web) |
| AI Model | Gemini Flash (fast, cheap, structured output) |
| Platform | Mobile (Android/iOS or mobile browser) |
| Location | Device geolocation + reverse geocode |

---

## 🚀 Quick Start

### Prerequisites
- Node.js 18+ (web stack) or Unity 2022 LTS+ (alternative)
- Gemini API key ([Get one here](https://aistudio.google.com/apikey))
- Any modern browser (desktop or mobile)

### Setup
1. Clone this repository
2. `npm install && npm run dev` (web) or open in Unity
3. Set your Gemini API key as `GEMINI_API_KEY` environment variable
4. Build and deploy (Vercel recommended — see [WINNING_PLAN.md](WINNING_PLAN.md) Section 5)

### Configuration
- **AI Config:** `src/config/ai.ts` — API endpoint, model, temperature
- **System Prompt:** `src/config/systemPrompt.ts` — Patient Zero's personality
- **City Lookup:** `src/config/cityLookup.json` — Location → theme mapping

---

## 👥 Team

*KICKR Codemania 2026 Entry*

---

## 📄 License

This project is built for the KICKR Codemania 2026 hackathon.
