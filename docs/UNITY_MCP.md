# 🔌 UNITY MCP — Setup & Connection Guide

> How this repo's AI agent connects to Unity Editor via Unity MCP (Model Context Protocol).
> Reference: `com.unity.ai.assistant` docs — the MCP bridge ships with Unity 6.2+ AI features.

## Status on this machine

| Step | State |
|---|---|
| Unity Hub 3.21.2 | ✅ Installed (`/Applications/Unity Hub.app`) |
| Unity 6000.3.24f1 LTS (x64) | ⏳ Downloaded headless via Hub CLI (`--headless install`) |
| License | ⛔ **Needs you** — see below |
| MCP driver | ✅ `tools/unity_mcp.py` (stdio JSON-RPC client, ready) |

## ⛔ The one manual step (only you can do it)

Unity requires a **Unity ID sign-in** to activate the (free) Personal license. I can't create accounts or log in for you.

1. Open **Unity Hub** (Applications → Unity Hub)
2. Sign in (top-right) — free Unity ID, or create one
3. License activates automatically (Personal)
4. Install finishes if paused: Hub → Installs → 6000.3.24f1

That's it — everything after this is automated by me.

## What happens next (automated once licensed)

```
You sign in once
   │
   ▼
I create/open the Unity project (unity/ folder)
   │
   ▼
Editor starts → MCP bridge auto-launches → relay installs to ~/.unity/relay/
   │
   ▼
tools/unity_mcp.py doctor   → handshake check
tools/unity_mcp.py list     → discover Unity's MCP tools
tools/unity_mcp.py call ... → I build scenes, import assets, edit scripts, run play mode
   │
   ▼
First connection shows an approval dialog in Project Settings → AI → Unity MCP
→ approve "patient-zero-driver" once (it's remembered after that)
```

## Driver commands

| Command | What it does |
|---|---|
| `python3 tools/unity_mcp.py doctor` | Verifies relay exists, Unity is running, MCP handshake works |
| `python3 tools/unity_mcp.py list` | Lists all Unity MCP tools (scene/asset/script/console automation) |
| `python3 tools/unity_mcp.py call <tool> '<json>'` | Executes a Unity MCP tool call |

## Why both engines?

- **Godot (current game)**: fully autonomous — no license wall, headless builds, ships today
- **Unity (parallel port)**: Unity MCP gives AI-native editor automation, Unity Asset Store / Mixamo realistic characters, and the biggest market reach. The C# game systems (brain, logger, memory, autopsy) port 1:1 — they're engine-agnostic by design.

## Security notes

- The MCP bridge is **local-only** (Unix socket on macOS) — no network exposure
- Direct client connections require a one-time approval dialog in Unity — you stay in control
- The driver never sends your project anywhere; it talks only to your local editor
