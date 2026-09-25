# CasinoExpansion

Adds new casino games to Schedule I. **Work in progress** — this build does not add any games yet. It logs diagnostics used to develop the networking layer.

## Requirements

- Schedule I on the **default Steam branch** (IL2CPP). Not compatible with the `alternate` (Mono) branch.
- **MelonLoader 0.7.3**

Both players in a co-op session must run the same version of this mod.

## Install

**Mod Manager:** install as normal; it places the DLL for you.

**Manual:** copy `Mods/CasinoExpansion.dll` into your game folder's `Mods` directory, e.g.

```
C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\
```

## Verifying it loaded

Launch the game and check the MelonLoader console, or `MelonLoader\Latest.log`, for:

```
[CasinoExpansion] CasinoExpansion loaded.
```

Then load a save. On entering the world it reports whether the game's money and casino systems are reachable:

```
[CasinoExpansion] Scene initialized: Main
[CasinoExpansion] MoneyManager reachable. Cash balance: ...
[CasinoExpansion] BlackjackGameController: N in scene
```

## If mods don't load at all

After a Schedule I update, MelonLoader's Il2Cpp assembly generation can fail with a `NullReferenceException` in `Pass11ComputeTypeSpecifics`, and no mods load. This is a stale cache, not a version problem — do **not** try to upgrade MelonLoader, 0.7.3 is the newest.

1. Close the game.
2. Rename these two folders (don't delete, so you can undo):
   - `MelonLoader\Il2CppAssemblies\`
   - `MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\`
3. In `UserData\Loader.cfg` set `force_regeneration = true` and `force_offline_generation = true`.
4. Launch. Regeneration takes about a minute and should end with `Assembly Generation Successful!`.
5. Set `force_regeneration` back to `false`.
