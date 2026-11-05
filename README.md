# RogueTrader.Overwatch

Overwatch for Warhammer 40,000: Rogue Trader: three abilities that let any gun hold ground, built on
the game's own overwatch engine (Owlcat shipped it in the game but never gave players a way to use it).

| Ability | What it does |
|---|---|
| **Reflex Shot** | XCOM-style overwatch. Pick the direction you expect them from; until your next turn, take one shot at the first enemy that moves (including being pushed) or acts within your weapon's range, in any direction. A reaction, not an aimed shot: it keeps your own hit chance (Ballistic Skill, cover, range and talents as usual), scaled by how far you had to swing from the direction you picked — all of it dead ahead, down to a tenth of it a quarter-turn away or more — and deals 33% of the weapon's damage. Always 1 AP. |
| **Overwatch** | Aim a cone sized by the cursor: close gives a short, wide cone (3 cells, 90°), far a long, narrow one (up to weapon range, 20°). Up to 2 shots (one per attack the AP left after casting it would have paid for, at least 1 and at most 2, so never more than shooting directly), at enemies that move, are pushed or act inside it: the first shot at the enemy caught moving, the second at once at the next-closest enemy in the cone. One already shot is shot again only after it moves 2+ cells. A buff shows the shots left and survives save and load. Shots carry a snap-shot penalty that grows with how far the cone reaches, from -1 at the shortest reach to -15 at full weapon range; everyone currently standing in the cone shows it with an Overwatched debuff. Each shot counts as an attack of opportunity, so talents and traits that boost attacks of opportunity apply. |
| **Suppression** | Pin one enemy. It can stay put and fight, but every time it leaves its square, by walking, charging, jumping or being pushed, it takes a ranged attack of opportunity (one per move or ability, not per square). While Suppressed, its own ranged attacks lose a quarter of their hit chance, and enemies know it: they'd rather stay put and shoot back than move. For the whole suppression your hero holds a firing pose and keeps firing at it, a visual cue only that never hits; a weapon with no projectile (like Pasqal's staff) shows a glowing red line joining them instead. |

Enemies don't route around your Reflex Shot circles or Overwatch cones: they move as they would anyway and get caught by the shots (RTO-T87, T88).

All three cost 1 AP (Overwatch and Suppression 1 more with a Heavy weapon active), end your turn and
last until your next turn. Every shot is your weapon's normal attack with its normal hit chance (which
already drops with distance), plus the ability's own effect: Reflex Shot scales it by how much aim your
facing retained, Overwatch adds a -1 to -15 reach penalty; Suppression's shot has neither. Reflex Shot
also deals 33% damage (RTO-T82, RTO-T96). Overwatch's and Suppression's shots are real attacks of opportunity
(Reflex Shot's are not), so talents and traits that specifically boost attacks of opportunity apply to them (RTO-T50,
T85). A Suppressed unit's own ranged attacks keep 75% of their final hit chance, shown in the hit-chance gauge too
(RTO-T86, T87). Taking any damage breaks a unit's concentration: its Overwatch, Reflex Shot
or Suppression ends at once. While you aim Overwatch or Reflex Shot, thin 50%-transparent lines trace the area's tile
edges (yellow for Reflex Shot, blue for Overwatch) and every enemy inside shows the game's own hit-chance gauge for the shot it would take.

**Who gets them**
- **Party:** every party member, on their ability bar in slots 8–10 of the first row (or the last
  free slots of rows 2 and 3). Greyed out whenever the active weapon set has no gun (melee only, two
  melee weapons, unarmed); lit as soon as it has one. A button you remove stays removed.
- **Enemies:** any enemy holding a pistol, rifle or heavy weapon. They fight normally, overwatch when
  they've finished moving with nothing to shoot and a party member is within weapon range + 8 cells
  (Overwatch with 2+ AP, Reflex Shot with 1, if they can afford its cost), and suppress when every target they can shoot is in full
  cover.
- **Pets:** never, even with a ranged weapon: they don't plan ahead.

The full rules and how each maps onto the engine: [docs/DESIGN.md](docs/DESIGN.md).

## Languages

Ability and buff names and descriptions are translated into all 8 of the game's official UI
languages, matching whichever one the game itself is set to:

- English (`enGB`)
- French (`frFR`)
- German (`deDE`)
- Spanish - Spain (`esES`)
- Russian (`ruRU`)
- Simplified Chinese (`zhCN`)
- Japanese (`jaJP`)
- Turkish (`trTR`)

Audio stays English-only, since that's all Rogue Trader itself ships.

## Build and install

Needs [RogueTrader.Shared](https://github.com/mindattic/RogueTrader.Shared) checked out next to this repo.

```
dotnet build src/RogueTrader.Overwatch
```

Compiles the mod against the game's DLLs (no Unity), installs it to
`%USERPROFILE%\AppData\LocalLow\Owlcat Games\Warhammer 40000 Rogue Trader\Modifications\RogueTrader.Overwatch`
and enables it; start (or restart) the game. Every build prints the installed DLL's write time
(`Deployed DLL last written: ...`, RTO-T57), so there's no guessing whether a running game session is on
this build or a stale one — a restart after that line is always enough. `-c Release` also writes the
upload, `dist/RogueTrader.Overwatch-<version>.zip`. Override `GameDir` or `GameDataDir` with `-p:Name=value`.

The installed folder must keep its `Bundles` subfolder: the game's loader lists it without checking
it exists, and without it no blueprint in the mod loads. It ships a tracked placeholder file
(`mod/Bundles/.keep`) rather than staying truly empty, because a truly empty folder in the zip gets
dropped by file-based mod managers like Vortex, which deploy by linking individual files (RTO-T104).

| Path | Holds |
|---|---|
| `mod/Blueprints/` | The mod's blueprints (`*.jbp`) and patches to game blueprints (`*.jbp_patch`, listed in `mod/OwlcatModificationSettings.json`) |
| `mod/Localization/*.json` | Strings per UI language, as `{"strings": {"<key>": {"Text": "..."}}}`; one file per locale (`enGB`, `frFR`, `deDE`, `esES`, `ruRU`, `zhCN`, `jaJP`, `trTR`) |
| `assets/` | Hotbar icons (`ReflexShot`, `Overwatch`, `OverwatchShots`, `Suppression`, `Suppressed`; square PNGs, replace and rebuild) and the Nexus `Logo.png` and `Header.png` |
| `src/RogueTrader.Overwatch/` | C# (Harmony patches) for what blueprints can't express |
| `docs/` | [DESIGN.md](docs/DESIGN.md), [TESTING.md](docs/TESTING.md), [NEXUS_PAGE.md](docs/NEXUS_PAGE.md), the ticket log |

## Testing

[docs/TESTING.md](docs/TESTING.md) is the in-game checklist: every rule and equip situation, its
expected result, and what to look for in `Player.log`.

## Status

**5.0.0: ready for in-game testing, not released.** Since 4.0.0:
- Fixed a Vortex-deployed install silently dropping every blueprint (abilities, buffs) because Vortex
  strips the empty `Bundles` folder the game's loader requires; the folder now ships a real file so it
  survives deployment (RTO-T104).

Since 3.0.0:
- Reflex Shot and Overwatch grey out once the weapon they'd fire has no rounds chambered, the same as
  any other ammo-gated ability (RTO-T93).
- Reflex Shot's triggered shot is now a real attack of opportunity too, like Overwatch's and
  Suppression's already were, so attack-of-opportunity talents apply to all three alike; all three now
  also draw from the same per-round reaction budget a melee threat's free swing already does, so a mook
  that's already reacted this round can't stack a second one for free (RTO-T94).
- Reflex Shot keeps your own hit chance in full (Ballistic Skill, range, cover, talents) instead of a
  flat 33% base, scaled by how far you had to turn to fire: all of it dead ahead, down to a tenth of it
  a quarter-turn away or more, so a skilled shooter's reflexes are worth more than a flat number ever
  let them be (RTO-T96).

Work through TESTING.md before uploading `dist/RogueTrader.Overwatch-5.0.0.zip` with
[docs/NEXUS_PAGE.md](docs/NEXUS_PAGE.md).

Versions go up a whole number each release (1.0.0, 2.0.0, 3.0.0, 4.0.0, 5.0.0): bump `Version` in
`mod/OwlcatModificationManifest.json`, which names the zip.
