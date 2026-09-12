# 🛠️ MANUAL_STEPS — Manual Setup & Human Actions Required

> **Manual Steps Document** | v1.0 | September 2026
> Every action a team member must perform by hand — account creation, key generation, device setup, asset sourcing, and pre-demo preparation.

---

## 1. Before Hackathon Day (Pre-Work)

> [!IMPORTANT]
> Complete these steps **before** the 24-hour clock starts. These are not build tasks — they are setup prerequisites.

### 1.1 Accounts & API Keys

- [ ] **Google AI Studio Account**
  - Go to [https://aistudio.google.com](https://aistudio.google.com)
  - Sign in with a Google account
  - Navigate to **API Keys** → **Create API Key**
  - Copy the key and store it securely (password manager or local-only note)
  - Test the key with a curl command:
    ```bash
    curl "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=YOUR_KEY" \
      -H "Content-Type: application/json" \
      -d '{"contents":[{"parts":[{"text":"Say hello"}]}]}'
    ```
  - Verify you get a 200 response with generated text

- [x] **Reverse Geocode (BigDataCloud) — No Setup Needed**
  - BigDataCloud's free reverse geocode API requires **no API key, no account, and no credit card**
  - It works out of the box with a simple GET request (see [APIS.md](file:///d:/Projects/PATIENT-ZERO/APIS.md) Section 4)
  - Fallback: Nominatim (also free, no key) → Local lookup table (offline)

- [ ] **GitHub Repository**
  - Create a private repo for the team
  - Add all team members as collaborators
  - Initialize with `.gitignore` for Unity (or Node.js if web)
  - Clone to all team members' machines

### 1.2 Software Installation

- [ ] **Unity Hub + Unity Editor** (if Unity stack)
  - Install Unity Hub: [https://unity.com/download](https://unity.com/download)
  - Install Unity 2022.3 LTS (or latest LTS)
  - Add Android Build Support module (includes Android SDK + NDK)
  - Verify: Create empty project → Build → APK generates successfully

- [ ] **Node.js + npm** (if web stack)
  - Install Node.js 18+ LTS: [https://nodejs.org](https://nodejs.org)
  - Verify: `node --version` and `npm --version`

- [ ] **Android Studio / ADB** (for device testing)
  - Install Android Studio or standalone ADB tools
  - Enable USB debugging on test Android device:
    - Settings → About Phone → tap Build Number 7 times
    - Settings → Developer Options → Enable USB Debugging
  - Verify: `adb devices` shows your device

- [ ] **scrcpy** (for demo screen mirroring)
  - Install: [https://github.com/Genymobile/scrcpy](https://github.com/Genymobile/scrcpy)
  - Test: Connect phone via USB → run `scrcpy` → phone screen appears on laptop
  - Verify audio and low latency

### 1.3 Device Preparation

- [ ] **Primary demo device (Android phone)**
  - Factory reset (optional, for clean demo environment)
  - Remove lock screen (or use quick unlock for demo)
  - Enable Developer Options and USB Debugging
  - Enable Location Services (GPS)
  - Grant location permission to the browser (if web) or the app (if Unity)
  - Disable auto-sleep / screen timeout (set to 30 min)
  - Disable notification interruptions (Do Not Disturb mode)
  - Charge to 100% and keep charger accessible

- [ ] **Backup demo device (if available)**
  - Same setup as primary
  - Optionally set to a different city's location for the two-device demo (Beat 1)
  - For mock location: Enable Developer Options → Select Mock Location App → use a GPS spoofing app

---

## 2. Hackathon Day — Hour 0 (First 30 Minutes)

### 2.1 Project Initialization

- [ ] Clone the repo on all team machines
- [ ] Create the Unity project (or scaffold the web project)
  - Unity: `New Project → 3D (URP or Built-in) → Name: PatientZero`
  - Web: `npx -y create-vite@latest ./ -- --template vanilla-ts`
- [ ] Set up folder structure per [FILESTRUCTURE.md](file:///d:/Projects/PATIENT-ZERO/FILESTRUCTURE.md)
- [ ] Add `.gitignore` with proper rules
- [ ] Create initial commit and push

### 2.2 API Key Configuration

- [ ] Create `Assets/Config/ai_config.json` (Unity) or `src/config/ai-config.ts` (web):
  ```json
  {
    "gemini_api_key": "YOUR_KEY_HERE",
    "gemini_model": "gemini-2.0-flash",
    "gemini_endpoint": "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
    "api_timeout_ms": 2000,
    "temperature": 0.7,
    "max_output_tokens": 200
  }
  ```
- [ ] Add `ai_config.json` to `.gitignore`
- [ ] Verify API call works from within the project (not just curl)

### 2.3 Team Task Assignment

- [ ] Assign team members to phases (see [PLAN.md](file:///d:/Projects/PATIENT-ZERO/PLAN.md) Section 5)
- [ ] Set up communication channel (Discord/Slack/WhatsApp group)
- [ ] Agree on merge strategy (feature branches → main, or trunk-based)
- [ ] Set checkpoint times (every 4 hours: quick sync, push, test)

---

## 3. During Build — Manual Checkpoints

### 3.1 Every 4 Hours — Sync Checkpoint

- [ ] All team members push current work
- [ ] Build the project (verify it compiles/runs)
- [ ] Quick team sync: what's done, what's blocked, what's next
- [ ] Test on actual device (not just editor/emulator)

### 3.2 At Hour 13 (AI Integration Phase)

- [ ] **Manual prompt testing before integration:**
  1. Open [Google AI Studio](https://aistudio.google.com)
  2. Paste the system prompt from [PROMPTS.md](file:///d:/Projects/PATIENT-ZERO/PROMPTS.md)
  3. Send a sample behavior summary as the user message
  4. Verify: response is valid JSON, composition makes sense, taunt is in-character
  5. Test 3–5 different behavior summaries (camping, mobile, skilled, struggling)
  6. Iterate on prompt if taunts are generic or compositions are flat
  7. Document prompt version changes

- [ ] **Test API call from game code:**
  1. Hardcode a sample behavior summary
  2. Make the API call from within the game
  3. Log the full response
  4. Verify parsing works
  5. Verify safety rails fire on clamped values
  6. Test timeout by setting timeout to 100ms temporarily

### 3.3 At Hour 17 (Location Phase)

- [ ] **Test geolocation on real device:**
  1. Build and install on phone
  2. Grant location permission when prompted
  3. Verify coordinates are received (log them)
  4. Verify reverse geocode returns a city name
  5. Verify theme bucket is correctly resolved
  6. Verify arena visuals change based on theme

- [ ] **Test geolocation denial:**
  1. Deny location permission
  2. Verify game still starts with `temperate` theme
  3. Verify no error screen or crash

### 3.4 At Hour 19 (Polish Phase)

- [ ] **Manual visual review:**
  - Does the taunt text display correctly? (font, size, position, animation)
  - Does the game-over screen look good?
  - Are screen shake and hit effects noticeable but not distracting?
  - Do the 5 arena themes look distinct from each other?

- [ ] **Manual audio check:**
  - Are all sound effects playing at the right moments?
  - Is the volume balanced? (SFX not too loud vs ambient)
  - Does audio work on phone speakers (not just headphones)?

---

## 4. Pre-Demo Preparation (Hours 22–24)

### 4.1 Device Setup Checklist

- [ ] Uninstall old builds from demo device
- [ ] Install final build
- [ ] Run a full game session (play 5+ waves)
- [ ] Verify AI taunts appear and make sense
- [ ] Verify location theming works (or fallback is clean)
- [ ] Charge device to 100%
- [ ] Enable Do Not Disturb mode
- [ ] Disable auto-lock / set timeout to 30 min
- [ ] Clear all notifications
- [ ] Close all other apps
- [ ] Position charger within reach of demo station

### 4.2 Projection Setup

- [ ] Connect device to laptop via USB
- [ ] Launch scrcpy (or your chosen mirroring tool)
- [ ] Verify mirror appears on projector/screen
- [ ] Adjust scrcpy window size to fill projection
- [ ] Test with game running — verify framerate and touch responsiveness on mirror

### 4.3 Backup Preparation

- [ ] **Record a backup demo video:**
  1. Screen-record a full demo run on the device
  2. Follow the exact demo script from [DEMO.md](file:///d:/Projects/PATIENT-ZERO/DEMO.md)
  3. Show: location seeding → Wave 1 → camping behavior → AI taunt → counter-wave
  4. Save as MP4, 720p+, keep on laptop desktop
  5. Verify video plays smoothly on the presentation laptop

- [ ] **Prepare fallback talking points** if live demo fails:
  - Open backup video
  - Narrate over it following the same 3-beat structure
  - "We had a live demo running earlier — here's a recording of the full AI adaptation loop."

### 4.4 Demo Rehearsal

- [ ] Practice the 90-second demo at least 3 times
- [ ] Time each rehearsal (must stay under 90 seconds)
- [ ] Verify the AI produces a good taunt when you camp (test this specific behavior)
- [ ] Decide who speaks (1 person recommended — less coordination, more confident)
- [ ] Decide who controls the device (can be same person or separate)
- [ ] Practice transitions between the 3 beats

---

## 5. Asset Sourcing (Manual Downloads)

### 5.1 Fonts

- [ ] Download **JetBrains Mono** (for taunt text):
  - [https://fonts.google.com/specimen/JetBrains+Mono](https://fonts.google.com/specimen/JetBrains+Mono)
  - Download Regular (400) weight
  - Place in `Assets/UI/Fonts/` (Unity) or `src/assets/fonts/` (web)

- [ ] Download **Inter** or **Outfit** (for HUD text):
  - [https://fonts.google.com/specimen/Inter](https://fonts.google.com/specimen/Inter)
  - Download Regular + Bold weights
  - Place in fonts directory

### 5.2 Sound Effects

All sounds should be **CC0 / Public Domain** license.

| Sound | Suggested Source | Search Term |
|---|---|---|
| Gunshot | [Freesound.org](https://freesound.org) | "pistol shot short" |
| Enemy hit | [Freesound.org](https://freesound.org) | "blunt impact flesh" |
| Enemy death | [Freesound.org](https://freesound.org) | "body fall collapse" |
| Player damage | [Freesound.org](https://freesound.org) | "pain grunt male" |
| AoE explosion | [Freesound.org](https://freesound.org) | "energy blast explosion" |
| Melee hit | [Freesound.org](https://freesound.org) | "punch impact" |
| Wave start | [Freesound.org](https://freesound.org) | "alert siren short" |
| Wave clear | [Freesound.org](https://freesound.org) | "victory chime" |
| Taunt appear | [Freesound.org](https://freesound.org) | "static glitch digital" |
| Game over | [Freesound.org](https://freesound.org) | "defeat low drone" |
| Ambient loop | [Freesound.org](https://freesound.org) | "dark ambient horror loop" |

> [!TIP]
> Download sounds **before** hackathon day. Searching Freesound during the build wastes valuable time. Pre-download 2–3 options per sound and pick the best during polish phase.

### 5.3 Textures / Materials

For a low-poly look, **solid colors and gradients** are faster than textures:
- Use Unity's default materials with color tints
- If textures are needed: [Kenney.nl](https://kenney.nl/assets) has free CC0 game assets

### 5.4 Particle Effects

- Use Unity's built-in particle system with default shapes
- No need to download external particle assets
- Simple configurations: `Burst emission + Sphere shape + Fade over lifetime`

---

## 6. Post-Build Submission Steps

- [ ] **Final build:**
  - Clean build (delete old artifacts, rebuild from scratch)
  - Test on demo device one more time
  - Note the final APK size / URL

- [ ] **Repository cleanup:**
  - Remove any debug logs or test code
  - Verify `.gitignore` is excluding API keys
  - Write a final commit message: "Final submission build"
  - Push to remote

- [ ] **Submission materials** (check hackathon requirements):
  - [ ] Project name and team name
  - [ ] Repository link (make public if required)
  - [ ] One-line description
  - [ ] Longer description (use pitch from [PRD.md](file:///d:/Projects/PATIENT-ZERO/PRD.md))
  - [ ] Demo video link (if required alongside live demo)
  - [ ] Screenshots (capture 3–4 showing: arena, taunt, different theme, game over)
  - [ ] Tech stack summary (from [TECHSTACK.md](file:///d:/Projects/PATIENT-ZERO/TECHSTACK.md))

- [ ] **Presentation prep:**
  - [ ] Slides (if using — max 3 slides, see [DEMO.md](file:///d:/Projects/PATIENT-ZERO/DEMO.md) Section 7)
  - [ ] Demo device charged and ready
  - [ ] Backup video accessible
  - [ ] Speaker knows the script

---

## 7. Troubleshooting — Common Manual Fixes

| Problem | Manual Fix |
|---|---|
| API key not working | Regenerate at AI Studio, update `ai_config.json` |
| Build fails on Android | Check Android SDK version in Unity → Preferences → External Tools |
| Location always returns `temperate` | Check device GPS is ON, check permission was granted |
| scrcpy not connecting | Run `adb kill-server && adb start-server`, reconnect USB |
| Game runs slow on device | Reduce particle counts, simplify materials, check console for errors |
| AI taunts are repetitive | Increase temperature to 0.8, add "vary your language" to prompt |
| Taunt text overflows UI | Reduce max chars in safety rail, increase text box size |
| Sound not playing on device | Check AudioSource volume, check device volume, check mute switch |
| Git merge conflicts | Communicate! Assign file ownership to avoid overlap |
| Unity editor crashes | Save frequently (Ctrl+S habit), use version control |

---

*This document is a human checklist. Print it out or keep it open during the hackathon. Check boxes as you go.*
