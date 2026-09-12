# 🎤 JUDGES — 90-Second Demo Pack

> Everything needed to evaluate PATIENT ZERO: PROTOCOL fast.

## ▶ Play it right now (no install)

**Web demo:** https://docmagic.me/PATIENT-ZERO/

![QR code](https://api.qrserver.com/v1/create-qr-code/?size=280x280&data=https%3A%2F%2Fdocmagic.me%2FPATIENT-ZERO%2F)

Point any phone camera at the QR → playing in ~5 seconds. Biome override for demos: add `?theme=desert` (or `coastal`, `mountain`, `urban`) to the URL.

## 🖥 Full 3D build (Godot + C#)

`game/` — see root README. Desktop: `godot --path .` → press `C` to cycle TOP / TPP / FPP cameras.

---

## The 90-second script

**Beat 1 — Hook (10s):** *"Every game at this hackathon has AI features. Ours has an AI villain. It doesn't chat — it profiles."* → open the game, show the specimen greeting (returning players get remembered by name/number).

**Beat 2 — Adaptation (50s):** Deliberately camp one pillar, ranged-only. Clear wave 1. Point at the taunt + reasoning line: *"It logged 80% stationary near cover, so it's flanking my exact zone with fast types and tanky units I can't out-range."* Show the counter-wave. Switch to FPP (`C`) for the "oh" moment as skeletons rush the camera.

**Beat 3 — Autopsy (20s):** Die on purpose. The AUTOPSY REPORT types itself: archetype, cause of death, adaptability index, closing remark. Hit SHARE REPORT (copies the card). *"That's the viral loop — every death is a shareable artifact."*

**Close:** *"Warner Bros patented the nemesis system, so nobody could build one. We built it anyway — with an LLM over behavioral telemetry instead of an authored state machine. The AI doesn't assist the game. It IS the game."*

## Judge Q&A kill-shots

| Q | A |
|---|---|
| Is this a chatbot wrapper? | The player never types anything. Zero text input, zero dialogue. The AI only acts through spawns, positioning, taunts, and the report. Remove it and the game has no difficulty curve at all. |
| What if the network dies? | 2.5s hard timeout → PZ-CORE local brain (a real rule-based adaptive AI, not a rubber-band). The demo works airplane-mode. |
| Real-world relevance? | Fraud bots and scam systems profile exactly like this — observe, adapt, counter. The autopsy shows players what a behavioral profile of them looks like. |
| Cost? | ~3 flash-tier calls per run, fractions of a cent. Local brain covers scale. |
| Business? | See docs/RESEARCH.md — proven wave-survival genre, share-loop UA, cosmetic + rewarded-ad monetization, sub-cent COGS. |

## Demo checklist

- [ ] Web demo loads on projector + at least one judge phone (QR)
- [ ] Play 2 waves camping → taunt references camping
- [ ] FPP moment (`C` twice) lands
- [ ] Death → autopsy card → share
- [ ] Closing line delivered looking at judges
