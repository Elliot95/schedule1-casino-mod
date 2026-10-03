# CasinoExpansion

Adds new games to Schedule I's casino. **Work in progress.**

## Requirements

- Schedule I on the **default Steam branch** (IL2CPP). Not compatible with the `alternate` (Mono) branch.
- **MelonLoader 0.7.3**

**Everyone in a co-op session needs this mod, at the same version.** The games run locally on each machine, so a player without it would see the vanilla table while everyone else plays something else.

## What's in it

- **Eight table games**, picked at any casino table from a selector in the bet panel. Real cards are dealt onto the felt, and decisions are made at the table.

  | Game | The decision | Returns | Measured playing |
  |---|---|---|---|
  | Blackjack (high roller) | Hit, stand, double, split without limit | 94.4% | mimic the dealer |
  | Ride the Bus (high roller) | Four guesses; cash out after any | 86.1% | riding all four |
  | Ride the Bus (high roller) | — | 94.7% | cashing out after two |
  | Baccarat — Player | Which side to back | 98.7% | — |
  | Baccarat — Banker | | 99.0% | — |
  | Baccarat — Tie | | 84.9% | — |
  | Casino Hold'em | Call twice the ante, or fold, on the flop | 97.4% | always calling |
  | Three Card Poker | Play or fold after three cards | 98.1% | play Q-6-4 or better |
  | Pai Gow Poker | Which two cards to set in front | 96.7% | the house way |
  | Red Dog | Raise or stand on the spread | 97.2% | raise on spread 7+ |
  | House game | — the table's own, untouched | | |

  These are **measured**, not calculated: 300,000 rounds per game through the shipped code,
  with `tools/rtp`. Return depends on how you play, so the strategy used is named beside each.
  Blackjack's 94.4% is what mimicking the dealer returns — played properly it is far closer to
  break-even, and the figure here is the floor rather than the ceiling.

- **Stakes to $50,000** on the mod games, with $1,000 nudges either side of the slider.
- **Prize wheel** — spawns near you; a 53-slice wheel with a jackpot, tuned to stay under the slot machines' real return.
- **Casino open 24/7** — vanilla opens it only between 16:00 and 05:00.
- **Higher slot bets** — the ladder gains $200 and $300 tiers.

## Install

Copy `Mods/CasinoExpansion.dll` into the `Mods` folder **that your launcher actually reads** — these are not the same place.

**If you launch from Steam:**

```
C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\
```

**If you launch from Thunderstore Mod Manager / r2modman / Gale:** the manager keeps its own MelonLoader per profile and redirects the loader there, so the game folder above is never read. Use the profile instead:

```
%AppData%\Thunderstore Mod Manager\DataFolder\ScheduleI\profiles\<profile>\Mods\
```

Not sure which? Launch once and look near the top of `MelonLoader\Latest.log` for `Core::BasePath` — that is the folder the loader reads, and `Mods` sits inside it.

**Check it worked.** The log should say, a couple of lines apart:

```
Melon Assembly loaded: '.\Mods\CasinoExpansion.dll'
CasinoExpansion v0.2.0
1 Mod loaded.
```

`0 Plugins loaded.` appears on every install and means nothing — plugins are a separate MelonLoader concept. The line to read is the **Mods** one.

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
