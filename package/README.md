# CasinoExpansion

Adds eight card games to Schedule I's casino, plus a prize wheel. Real cards are dealt on the
table and you make the decisions there.

---

## 1. What you need first

- **Schedule I on the default Steam branch.** This is the normal one — if you have never
  deliberately changed branches, you are on it. It will **not** work on the `alternate` (Mono)
  branch.
- **MelonLoader 0.7.3.** Not 0.6, not anything older. If you need it:
  <https://github.com/LavaGang/MelonLoader.Installer/releases> — download
  `MelonLoader.Installer.exe`, run it, point it at `Schedule I.exe`, and **tick "Show Alpha
  Pre-Releases"** or 0.7.3 will not appear in the version list.

**Both of you need this mod, at the same version.** Each game is worked out on your own machine,
so someone without it would sit at a vanilla table while everyone else plays something else.

---

## 2. Where the DLL goes

Copy `Mods/CasinoExpansion.dll` into the `Mods` folder **that your launcher actually reads.**
These are different folders and picking the wrong one is the single most common reason nothing
happens.

**Launching from Steam:**

```
C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\
```

Not sure that is where your game is? In Steam, right-click **Schedule I → Manage → Browse local
files**. That opens the real folder, wherever it lives.

**Launching from Thunderstore Mod Manager, r2modman or Gale:** the manager keeps its own copy of
MelonLoader per profile and redirects the loader there, so the Steam folder above is **never
read**. Use the profile instead:

```
%AppData%\Thunderstore Mod Manager\DataFolder\ScheduleI\profiles\<your profile>\Mods\
```

Paste that into Explorer's address bar. If you have several profiles, use the one you actually
launch.

**Still unsure which?** Launch once, then open `MelonLoader\Latest.log` and look near the top for
`Core::BasePath`. That is the folder the loader reads, and `Mods` sits inside it.

---

## 3. Checking it worked

Open `MelonLoader\Latest.log` after launching. You want these lines, a second or two apart:

```
Melon Assembly loaded: '.\Mods\CasinoExpansion.dll'
CasinoExpansion v0.3.0
1 Mod loaded.
[CasinoExpansion] CasinoExpansion loaded.
```

**`0 Plugins loaded.` appears on every install and means nothing** — plugins are a separate
MelonLoader concept and nobody has any. The line that matters is the **Mods** one, further down.

In game: the casino is open around the clock, so if you can walk in during the morning, it is
working.

---

## 4. Playing

Sit at either casino table. The bet panel gains three things:

- **A game selector** (bottom right) — cycles through the eight games.
- **The rules** for whatever is selected, on the right.
- **Who is seated and who has readied up**, on the left.

Set your stake with the slider or the **± $1,000** buttons, then press **Ready**. Decisions appear
on the table itself during the hand, not on the bet panel.

**In co-op, everyone at the table must pick the same game before it will deal.** Your *bets* are
your own — in baccarat you and your friend can back opposite sides at the same table.

| Game | What you decide | Returns |
|---|---|---|
| Blackjack (high roller) | Hit, stand, double, split as often as pairs come. Side bets and insurance | 94.4% |
| Ride the Bus (high roller) | Four guesses, each harder. Cash out after any correct one | 86–95% |
| Baccarat | Back Player, Banker or Tie | 98.7 / 99.0 / 84.9% |
| Casino Hold'em | Call twice the ante, or fold, once you have seen the flop | 97.4% |
| Three Card Poker | Play or fold after three cards. Straight beats flush here | 98.1% |
| Pai Gow Poker | Which two of your seven cards go in front | 96.7% |
| Red Dog | Raise or stand once the spread is known | 97.2% |
| House game | Nothing — the table's own game, untouched | |

Returns are measured, not guessed: 300,000 rounds of each game, and they depend on how you play.

Stakes go to **$50,000** on these games. The house game keeps its normal limits.

**Prize wheel:** press **F9** and one appears in front of you.

---

## 5. If nothing loads at all

After a Schedule I update, MelonLoader's assembly generation can fail with a
`NullReferenceException` in `Pass11ComputeTypeSpecifics`, and then *no* mods load. This is a stale
cache, not a version problem — do **not** try to upgrade MelonLoader, 0.7.3 is the newest.

1. Close the game.
2. Rename these two folders (rename rather than delete, so you can undo):
   - `MelonLoader\Il2CppAssemblies\`
   - `MelonLoader\Dependencies\Il2CppAssemblyGenerator\Cpp2IL\cpp2il_out\`
3. In `UserData\Loader.cfg` set `force_regeneration = true` and `force_offline_generation = true`.
4. Launch. Regeneration takes about a minute and ends with `Assembly Generation Successful!`.
5. Set `force_regeneration` back to `false`.

---

## 6. Settings

`UserData\MelonPreferences.cfg`, under `[CasinoExpansion]` — wheel stake and position, the 24-hour
casino toggle, and the extra slot bet tiers. `TestJackpotChance` forces wheel jackpots for testing
and should stay at `0`.

## 7. Keys

| Key | Does |
|---|---|
| F9 | Put the prize wheel where you are standing |
| F8 | Multiplayer channel test — run from a non-host client |
| F10 | Log nearby casino staff |
| F11 | Log clonable UI widgets |

F8, F10 and F11 are development probes and only write to the log.

---

## Known rough edges

- Cards can overlap in Pai Gow and Casino Hold'em, where a hand needs more positions than the
  table lays out. Cosmetic.
- Blackjack's side bets (Perfect Pairs, 21+3) pay less than a real table's. They work correctly,
  the odds just are not tuned yet.
- The games have not been played in co-op yet, only tested solo. If your balances ever disagree
  after a hand, that is worth reporting — it is the one thing that could be quietly wrong.
