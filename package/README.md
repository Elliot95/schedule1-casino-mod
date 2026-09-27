# CasinoExpansion

Adds new games to Schedule I's casino. **Work in progress.**

## Requirements

- Schedule I on the **default Steam branch** (IL2CPP). Not compatible with the `alternate` (Mono) branch.
- **MelonLoader 0.7.3**

**Everyone in a co-op session needs this mod, at the same version.** The games run locally on each machine, so a player without it would see the vanilla table while everyone else plays something else.

## What's in it

- **Prize wheel** — spawns near you; a 53-slice wheel with a jackpot, tuned to stay under the slot machines' real return.
- **Table games** — pick a game at any casino table from a selector in the bet panel. **Baccarat** is playable; the rest are listed but not yet implemented.
- **Casino open 24/7** — vanilla opens it only between 16:00 and 05:00.
- **Higher slot bets** — the ladder gains $200 and $300 tiers.

## Install

**Mod Manager:** install as normal.

**Manual:** copy `Mods/CasinoExpansion.dll` into your game folder's `Mods` directory, e.g.

```
C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\
```

## Keys

| Key | Does |
|---|---|
| F9 | Move the prize wheel to where you're standing |
| F10 | Log nearby casino staff |
| F11 | Log clonable UI widgets |
| F8 | Multiplayer channel test — **run from a non-host client** |

## Settings

`UserData\MelonPreferences.cfg`, under `[CasinoExpansion]` — stake, wheel position, 24/7 toggle, slot ladder tiers. `TestJackpotChance` forces wheel jackpots for testing and should stay at `0` for real odds.

## If mods don't load at all

After a Schedule I update, MelonLoader's Il2Cpp assembly generation can fail with a `NullReferenceException` in `Pass11ComputeTypeSpecifics`, and nothing loads. This is a stale cache, not a version problem — do **not** try to upgrade MelonLoader, 0.7.3 is the newest.

1. Close the game.
2. Rename these two folders (don't delete, so you can undo):
   - `MelonLoader\Il2CppAssemblies\`
   - `MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\`
3. In `UserData\Loader.cfg` set `force_regeneration = true` and `force_offline_generation = true`.
4. Launch. Regeneration takes about a minute and ends with `Assembly Generation Successful!`.
5. Set `force_regeneration` back to `false`.
