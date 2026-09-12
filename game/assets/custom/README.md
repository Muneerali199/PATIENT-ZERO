# 🎨 DROP YOUR OWN CHARACTERS HERE

Drop your `.glb` files into this folder and the game uses them **automatically** — no code changes needed.

## File names (exact)

| File | Replaces | Auto-scaled to |
|---|---|---|
| `player.glb` | The soldier (player) | 1.8 m tall |
| `zombie_standard.glb` | Standard enemy | 1.75 m |
| `zombie_runner.glb` | Fast enemy | 1.55 m |
| `zombie_brute.glb` | Tanky enemy | 2.15 m |

Missing file → built-in model is used. So you can replace just one (e.g. only the player) and the rest stay default.

## Export checklist (Blender / any tool)

- Format: **GLB** (glTF binary), textures **embedded**
- Character facing **-Y in Blender** (exports as +Z forward in Godot) — if it walks backwards in-game, rotate 180° and re-export
- Y-up or Z-up both fine — the game auto-normalizes height and centers the model, feet on ground
- Rigged + animations? Supported — if your GLB has an AnimationPlayer with names containing `walk` / `attack` / `death` / `idle`, the game plays them automatically
- Unrigged? Also fine — the game applies its shamble/bob motion procedurally

## Then run

```bash
cd game
godot --headless --import --path .   # picks up new files (once)
godot --path .                        # play with YOUR characters
```
