# 🎭 CHARACTERS — Asset Pipeline & Realistic-Human Roadmap

> How characters are made/sourced, and the exact paths to photoreal humans.

## Current (shipped, verified)

| Asset | Source | License | Use |
|---|---|---|---|
| Knight.glb | KayKit Adventurers Pack | CC0 | Player |
| Skeleton_Minion / Rogue / Warrior.glb | KayKit Skeletons Pack | CC0 | Standard / Fast / Tanky enemies (walk, melee attacks, death anims) |
| pillar / wall_arched / floor_tile / banner | KayKit Dungeon Remastered | CC0 | Arena environment |
| brushed_concrete / chipped_concrete PBR | Poly Haven | CC0 | Floor + walls |

All imported headlessly (`godot --headless --import`), tinted per biome at runtime.

## Toolchain installed (this machine)

- **Blender 4.2.23 LTS (x64)** — `~/dev-tools/Blender.app`, verified headless (`blender -b`). Full procedural modeling / baking / GLB export pipeline available via Python scripts.
- **Godot 4.4.1 mono** — `/tmp/godot-mono/Godot_mono.app`, verified headless (import, build-solutions, smoke tests, in-game screenshots).
- **.NET 8 SDK** — `~/.dotnet`.

## Realistic-human attempts — honest status

| Route | Status | Blocker |
|---|---|---|
| **MPFB2 (MakeHuman for Blender)** | ❌ 3 strikes | v2.0.17 ships unbuilt source; it's a Blender *Extension* (needs their `build_utilities` packaging step + first-run asset-library download). Headless enable fails inside `locationservice.py` (extension-path lookup). |
| MB-Lab | ❌ not attempted | Blender 2.x-era addon, unmaintained. |

### Next-session options (pick one)

1. **Build MPFB2 as a proper extension** — clone repo, run `build_utilities`, produce extension zip, `bpy.ops.extensions.package_install`, then scripted human → Rigify rig → GLB. Est. 30–60 min, medium risk.
2. **Unity MCP route** — user does one-time Unity Hub login (license) → I drive Unity via MCP/CLI → use Mixamo/Asset-Store realistic characters, auto-import to our game. Requires: Unity Hub + Editor install, one GUI login.
3. **Drop-in purchase/free download** — any rigged realistic human GLB/FBX (CC0/CC-BY) → I wire it in (~10 min: import, retarget walk/attack/death anims, biome tint).

> Note: at TOP/TPP camera distance, stylized reads *better* than photoreal (Uncanny valley risk). Realistic humans pay off most in **FPP enemy encounters** — that's where we'd spend the effort.
