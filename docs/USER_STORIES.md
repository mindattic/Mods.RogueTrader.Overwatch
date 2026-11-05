---
codex: 1
project: Mods.RogueTrader.Overwatch
code: RTO
layer: stories
status: living
updated: 2026-10-26
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# Mods.RogueTrader.Overwatch — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| RTO-T1 | 2026-10-26 | docs | closed | Start the permanent ticket log in the ticket database | — |
| RTO-T2 | 2026-10-26 | feature | closed | Create the RogueTrader.Overwatch repo: Overwatch abilities on every ranged weapon for heroes and enemies (Reaction Shot, Overwatch, Suppression) | 3d014c5 |
| RTO-T3 | 2026-10-26 | chore | closed | Build the mod without Unity: script that assembles the Owlcat mod folder (manifest, settings, blueprints, patches, enGB) and installs/enables it | 5873bbc |
| RTO-T4 | 2026-10-26 | feature | closed | Give every unit holding a ranged weapon the Overwatch buttons (AddAbilitiesToCurrentWeapon on CommonMobFact for enemies, hero units for the party) | Confirmed in game by the user, 2026-10-26 |
| RTO-T5 | 2026-10-26 | feature | closed | Reflex Shot: XCOM 2 style, fire once at the first enemy that moves or acts within range, then end | Confirmed in game by the user, 2026-10-26 |
| RTO-T6 | 2026-10-26 | feature | closed | Overwatch: draggable cone, one shot per AP left when cast; spreads fire across enemies, re-shoots one only after it moves 2+ tiles | Confirmed in game by the user, 2026-10-26 |
| RTO-T7 | 2026-10-26 | feature | wont-do | Kill Zone Overwatch: aimed wide cone, shoot every enemy that moves in it once until your next turn | Merged into RTO-T6 Overwatch (draggable cone, shots = remaining AP) per user 2026-10-26 |
| RTO-T8 | 2026-10-26 | feature | closed | Suppression: pin one enemy; it takes a ranged attack of opportunity if it moves, with cosmetic always-miss fire during its turn | Confirmed in game by the user, 2026-10-26 |
| RTO-T9 | 2026-10-26 | feature | wont-do | Lane Overwatch: long narrow line aimed down a corridor, one shot at the first enemy in it | User dropped Lane Overwatch on 2026-10-26 |
| RTO-T10 | 2026-10-26 | feature | closed | Overwatch icons: placeholder game sprites per type now, native-looking final art later | Confirmed in game by the user, 2026-10-26 |
| RTO-T11 | 2026-10-26 | feature | closed | Enemy AI uses Overwatch via BlueprintBrain (when no target, spare AP, ranged role) | Confirmed in game by the user, 2026-10-26 |
| RTO-T12 | 2026-10-26 | feature | wont-do | Drag-to-size Overwatch cone (Gears Tactics style) via C#, after the data-only types are tested | Merged into RTO-T6 Overwatch (draggable cone is part of it) per user 2026-10-26 |
| RTO-T13 | 2026-10-26 | fix | closed | Overwatch shot count is lost on save/load: an active Overwatch reloads with 1 shot instead of the AP-based count | 542091f |
| RTO-T14 | 2026-10-26 | chore | closed | Rename Reaction Shot to Reflex Shot (in-game name, files, docs) | 7afa83f |
| RTO-T15 | 2026-10-26 | feature | closed | Release 1.0.0: dist zip of the Owlcat mod folder, built by dotnet build -c Release | dist/RogueTrader.Overwatch-1.0.0.zip, built by dotnet build -c Release; confirmed in game |
| RTO-T16 | 2026-10-26 | docs | closed | Nexus Mods page: header, logo, summary and BBCode description in docs/NEXUS_PAGE.md | — |
| RTO-T17 | 2026-10-26 | fix | closed | Ship only the hotbar icons in the mod, not the Nexus header and logo | 0fe4406 |
| RTO-T18 | 2026-10-26 | feature | closed | Put Reflex Shot, Overwatch and Suppression on every party member's ability bar (last 3 slots, else later rows), greyed out without a ranged weapon in the active set | Confirmed in game by the user, 2026-10-26 |
| RTO-T19 | 2026-10-26 | docs | closed | In-game validation guide (docs/TESTING.md) covering every rule and equip situation, with README, DESIGN and Nexus page brought up to date | ffca20e |
| RTO-T20 | 2026-10-26 | chore | closed | Build on RogueTrader.Shared: game paths, references, packaging, sprite loading and logging from the shared source | d7e8fc7 |
| RTO-T21 | 2026-10-26 | feature | closed | Reflex Shot: pick an expected facing; aim bonus for targets ahead, penalty growing with the turn needed, largest at 180 degrees | Confirmed in game by the user, 2026-10-26 |
| RTO-T22 | 2026-10-26 | fix | closed | The game skips all the mod's blueprints: the install and the release zip have no Bundles folder, and LoadBundles throws before LoadBlueprints | bdb572b |
| RTO-T23 | 2026-10-26 | fix | closed | Overwatch cone starts at the clicked cell instead of the shooter: the resize patch passed directional=false | cc99f89 |
| RTO-T24 | 2026-10-26 | fix | closed | Overwatch never re-shoots anyone while a downed (unconscious) enemy is in the cone: it counted as not yet shot | b61773e |
| RTO-T25 | 2026-10-26 | fix | closed | Companions recruited or mercenaries hired mid-area get no Overwatch buttons until the next area load | 9bbc68e |
| RTO-T26 | 2026-10-26 | fix | closed | Suppression: a cancelled or blocked move leaves the target marked, so a later push or dodge draws a shot | 3abbc03 |
| RTO-T27 | 2026-10-26 | feature | closed | Suppression fires when the target leaves its square by any means (walk, charge, jump, push), once per command | 3abbc03 |
| RTO-T28 | 2026-10-26 | fix | closed | Reflex Shot's aim modifier leaks into a later Suppression shot by the same shooter at the same target (cleared only by the next overwatch cast) | 0d9c1c5 |
| RTO-T29 | 2026-10-26 | fix | closed | Suppression: a push draws a shot for every square crossed instead of one, because the target has no command of its own | e944409 |
| RTO-T30 | 2026-10-26 | fix | closed | Pets get the party Overwatch feature and three always-greyed buttons on their ability bar | 9bb7706 |
| RTO-T31 | 2026-10-26 | fix | closed | Enemies behind cover may never cast Reflex Shot: the facing point one cell away is often the cover itself and fails CanTarget | 395034e |
| RTO-T32 | 2026-10-26 | fix | closed | Reflex Shot's aim modifier can be lost before the hit roll: the attack command may end before RuleCalculateHitChances runs | 0d9c1c5 |
| RTO-T33 | 2026-10-26 | fix | closed | TESTING.md results table lacks rows for the new cases, and DESIGN says the party feature goes to every character (pets are skipped) | 4a893da |
| RTO-T34 | 2026-10-26 | fix | closed | Game hangs on the first loading screen: NullReferenceException in OwlcatModification.TryLoadBundleAsync while loading a scene bundle | 5600c29 |
| RTO-T35 | 2026-10-26 | fix | closed | Loading a save stops the game: the CommonMobFact component patch writes m_Flags as a number, which the patcher's strict enum reader rejects; blueprints also use an invalid Author | 138a2e9 |
| RTO-T36 | 2026-10-26 | docs | closed | Say that pushes trigger all three abilities: in-game descriptions, DESIGN and TESTING cases D2c and E6a | eeae417 |
| RTO-T37 | 2026-10-27 | chore | closed | Standardize phrasing across the English ability/buff descriptions as the stable source text for localization | b92b68a |
| RTO-T38 | 2026-10-27 | feature | closed | French (frFR) localization for Reflex Shot, Overwatch and Suppression | frFR |
| RTO-T39 | 2026-10-27 | feature | closed | German (deDE) localization for Reflex Shot, Overwatch and Suppression | deDE |
| RTO-T40 | 2026-10-27 | feature | closed | Spanish - Spain (esES) localization for Reflex Shot, Overwatch and Suppression | esES |
| RTO-T41 | 2026-10-27 | feature | closed | Russian (ruRU) localization for Reflex Shot, Overwatch and Suppression | ruRU |
| RTO-T42 | 2026-10-27 | feature | closed | Simplified Chinese (zhCN) localization for Reflex Shot, Overwatch and Suppression | zhCN |
| RTO-T43 | 2026-10-27 | feature | closed | Japanese (jaJP) localization for Reflex Shot, Overwatch and Suppression | jaJP |
| RTO-T44 | 2026-10-27 | feature | closed | Turkish (trTR) localization for Reflex Shot, Overwatch and Suppression | trTR |
| RTO-T45 | 2026-10-27 | feature | closed | Release 2.0.0: bump version for the full 8-language localization support | dist/RogueTrader.Overwatch-2.0.0.zip |
| RTO-T46 | 2026-10-27 | chore | closed | Add in-game screenshots to assets/ for the Nexus page | — |
| RTO-T47 | 2026-10-27 | docs | closed | README: add a dedicated Languages section listing all 8 supported UI languages | — |
| RTO-T48 | 2026-10-27 | docs | closed | NEXUS_PAGE.md: drop the stray trailing comma after Simplified Chinese in the Localization list | — |
| RTO-T49 | 2026-10-27 | docs | closed | Restore full NEXUS_PAGE.md structure, with a proper Localization section | — |
| RTO-T50 | 2026-10-28 | feature | closed | Suppression's real shot flags IsAttackOfOpportunity so AOO-specific talents/feats apply to it, not just a normal attack | commit f52f1c3 |
| RTO-T51 | 2026-10-28 | feature | closed | Reflex Shot, Overwatch and Suppression cost +1 AP to cast when the active ranged weapon is Heavy (WeaponHeaviness.Heavy) | commit f52f1c3 |
| RTO-T52 | 2026-10-28 | feature | closed | Release 3.0.0: bump version for the Suppression AOO fix (RTO-T50) and weapon-size AP cost balance change (RTO-T51) | commit f52f1c3 |
| RTO-T53 | 2026-10-29 | fix | closed | AbilityCasterHasWeaponWithRangeType crashes (NullReferenceException) for a caster with any empty additional limb, greying out all three abilities; confirmed on Pasqal (mechadendrites) | commit f52f1c3 |
| RTO-T54 | 2026-10-29 | feature | closed | Overwatch: snap-shot accuracy penalty that grows with the cone's reach, shown via an Overwatched debuff on every enemy currently inside the cone | commit f52f1c3 |
| RTO-T55 | 2026-10-29 | feature | closed | Reflex Shot's circle never reaches past the equipped weapon's own max range, instead of a flat 10 cells for every weapon | commit f52f1c3 |
| RTO-T56 | 2026-10-29 | feature | closed | A yellow LineRenderer outline traces the boundary of an active Reflex Shot circle or Overwatch cone, since the game's shared red area highlight is hard to see | commit f52f1c3 |
| RTO-T57 | 2026-10-29 | chore | closed | Build prints the installed DLL's last-write time after every deploy, so it's obvious whether a restart will pick up stale or current code | commit f52f1c3 |
| RTO-T58 | 2026-10-29 | fix | closed | Suppression's Suppressed debuff appears on the caster instead of the targeted enemy | commit f52f1c3 |
| RTO-T59 | 2026-10-29 | fix | closed | Overwatched debuff shows on enemies only while casting, then disappears; only the caster keeps an icon | commit f52f1c3 |
| RTO-T60 | 2026-10-29 | fix | closed | Reflex Shot circle stays 10 cells regardless of the weapon's range (RTO-T55 not taking effect, tooltip still says 10) | commit f52f1c3 |
| RTO-T61 | 2026-10-29 | fix | closed | Yellow outline (RTO-T56) never draws, during preview or after cast | commit f52f1c3 |
| RTO-T62 | 2026-10-29 | feature | closed | Reflex Shot: circle radius = the weapon's full range (no 10-cell ceiling), with a distance aim penalty scaling to -33 at full range | commit f52f1c3 |
| RTO-T63 | 2026-10-29 | fix | closed | Outline traces a convex hull through cell centres instead of the actual tile edges of the Reflex Shot/Overwatch area | commit f52f1c3 |
| RTO-T64 | 2026-10-29 | fix | closed | Outline is partial and too thick: draw one thin line per boundary tile edge instead of chained loops | commit f52f1c3 |
| RTO-T65 | 2026-10-29 | feature | closed | Suppression: a glowing red line from each suppressor to its Suppressed target for as long as the debuff lasts | commit f52f1c3 |
| RTO-T66 | 2026-10-29 | feature | closed | Suppression cosmetic fire: continuous firing animation for the whole suppression, no projectiles or misses, purely a visual cue | commit f52f1c3 |
| RTO-T67 | 2026-10-29 | feature | closed | Taking any damage interrupts the unit's Suppression, Overwatch and Reflex Shot; cosmetic suppression fire shoots projectiles at the target that deliver nothing | commit f52f1c3 |
| RTO-T68 | 2026-10-29 | fix | closed | Outline vanishes about a second into target selection; it must stay for the whole selection and while Overwatch/Reflex Shot is active | commit f52f1c3 |
| RTO-T69 | 2026-10-29 | feature | closed | Suppressor holds a firing/aiming pose toward the target for as long as Suppression lasts | commit f52f1c3 |
| RTO-T70 | 2026-10-29 | fix | closed | Outline gaps across height changes: add vertical connector lines at corners where boundary edges meet at different heights | commit f52f1c3 |
| RTO-T71 | 2026-10-29 | feature | wont-do | While aiming Overwatch or Reflex Shot, show each enemy inside the area its projected hit chance, including our modifiers | superseded by RTO-T80 (overtip gauge instead of custom labels) |
| RTO-T72 | 2026-10-29 | feature | wont-do | Reflex Shot: flat -33 reaction penalty on every shot (replaces the distance-scaled penalty), plus the facing modifier | superseded by RTO-T82: flat -33 reaction penalty replaced by the aim-modifier/base-hit-chance redesign |
| RTO-T73 | 2026-10-29 | fix | closed | Bughunt: exhaustive sweep of the mod's source, blueprints and localization; fix every confirmed bug | fixed in src/RogueTrader.Overwatch/ReflexShot.cs: ReflexShot_AimModifier never cleared ReflexShot.Pending after a successful match, so a later unrelated attack on the same target within 5s inherited Reflex Shot's base hit chance, damage cut and stale aim modifier |
| RTO-T74 | 2026-10-29 | fix | closed | Bughunt pass 2: review the pass-1 fixes, cross-patch interactions, save/load and uninstall safety, design-doc consistency | reviewed RTO-T73's fix for correctness, cross-patch Harmony priority ordering, save/load and uninstall safety, design-doc consistency; nothing further found |
| RTO-T75 | 2026-10-29 | docs | closed | Bring all docs, README, Nexus page, changelog and in-game ability text in line with the 3.0.0 rules; Overwatch penalty 1-30 so the debuff rank equals it | commit 6869221 |
| RTO-T76 | 2026-10-29 | feature | closed | Overwatch shots = AP before casting / weapon attack cost, capped at 2; outlines 50% transparent, yellow for Reflex Shot, blue for Overwatch | commit f52f1c3 (outline transparency); commit 6869221 (shot formula, RTO-T91) |
| RTO-T77 | 2026-10-29 | fix | wont-do | Hit-chance labels overlap the game's overhead unit icon: raise them above it and use a smaller font | superseded by RTO-T80: no custom label exists to reposition, game's own overtip gauge is used |
| RTO-T78 | 2026-10-29 | fix | wont-do | Hit-chance labels use the game's own hit-percentage text colour instead of a red-to-green gradient | superseded by RTO-T80: no custom label exists to recolor, game's own overtip gauge is used |
| RTO-T79 | 2026-10-29 | fix | closed | Bughunt: enemy AI ignores off-hand ranged weapons; drop temporary diag logging and unused LockedRadius | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T80 | 2026-10-29 | fix | closed | Hit-chance preview: labels off-centre and unlike the game's hit-chance UI; show above the overwatch target icon in the game's own style | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T81 | 2026-10-29 | fix | closed | Suppression's cosmetic shots fly into the target; aim them at points around it so they always visibly miss | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T82 | 2026-10-29 | feature | closed | Reflex Shot: 33% base hit chance + modifiers, 33% damage + modifiers, 1 AP, ends turn | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T83 | 2026-10-29 | feature | closed | Overwatch: on trigger, shoot the unit caught moving, then the next-closest enemy marked Overwatched, then end | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T84 | 2026-10-29 | fix | closed | Suppression red line only for weapons with no projectile to fire (e.g. Pasqal's staff); projectile weapons rely on their missing cosmetic shots instead | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T85 | 2026-10-29 | feature | closed | Overwatch shots are attacks of opportunity, so AoO talents and bonuses apply | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T86 | 2026-10-29 | feature | closed | Suppressed units have their hit chance halved (after modifiers) while Suppressed | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T87 | 2026-10-29 | feature | closed | Balance pass: Overwatch snap-shot max -15, AI ignores Reflex Shot areas, Suppressed hit chance x0.75 of the capped chance | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T88 | 2026-10-29 | feature | closed | Enemy AI only routes around an Overwatch cone if it has line of sight to the overwatcher when planning its move | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T89 | 2026-10-29 | chore | closed | 3.0.0 balance pass and fixes: commit RTO-T79..T88 together (changes interleave in the same files) | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T90 | 2026-10-29 | feature | closed | Suppressed AI units know they are pinned: they avoid leaving their square and shoot back from where they stand | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T91 | 2026-10-29 | feature | closed | Overwatch is the next best thing to a direct shot: shots = AP left after paying its own cost / attack cost (1 to 2), so it never matches shooting directly | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T92 | 2026-10-29 | fix | closed | Spanish Overwatch description: use 'que te quedarían' for consistent tense | commits 6869221 9ea8b85 1a3c33e 84b99df |
| RTO-T93 | 2026-10-30 | fix | closed | Reflex Shot and Overwatch should respect rounds in the chamber and not fire with an empty magazine | — |
| RTO-T94 | 2026-10-30 | feature | closed | Pipe Reflex Shot's triggered shot through Attack of Opportunity like Overwatch and Suppression already are, and rebalance all three for it | — |
| RTO-T95 | 2026-10-30 | chore | closed | Audit the open backlog: close 28 tickets already shipped in the 3.0.0 commits (f52f1c3, 6869221) or superseded by later redesigns (T80, T82), leaving only the two bughunt tickets open | docs/USER_STORIES.md |
| RTO-T96 | 2026-10-30 | feature | closed | Reflex Shot's hit chance: your own Ballistic-Skill-based chance scaled by how far you had to turn (cosine of the angle, floored at 10%), not a flat 33% base with a linear +10/-30 ramp | src/RogueTrader.Overwatch/ReflexShot.cs: RetainedFraction (cosine of turn angle, floored at 10%) scales the shooter's own hit chance instead of a flat 33 base + linear ramp; docs/DESIGN.md, README.md, docs/TESTING.md and all 8 localizations updated |
| RTO-T97 | 2026-10-30 | fix | closed | Bughunt: exhaustive sweep after the Reflex Shot retained-aim rework (RTO-T96); fix every confirmed bug | docs/DESIGN.md, src/RogueTrader.Overwatch/Suppression.cs: fixed two stale comments claiming Reflex Shot's hit chance still shows a named breakdown line; no code-logic bugs found in the RTO-T96 rework on re-review |
| RTO-T98 | 2026-10-31 | chore | closed | Bump to 4.0.0: version manifest, README Status/changelog, Nexus page version/changelog/description/checklist, matching the mechanics shipped since 3.0.0 (T93, T94, T96) | mod/OwlcatModificationManifest.json, README.md, docs/NEXUS_PAGE.md: version 4.0.0, fresh changelog for T93/T94/T96, fixed stale checklist/BBCode lines claiming Reflex Shot isn't an attack of opportunity or still uses a flat 33 base |
| RTO-T99 | 2026-10-31 | chore | closed | Add 20261029174655_1.jpg, 20261029174700_1.jpg, 20261029174711_1.jpg, 20261029174715_1.jpg, 20261029174836_1.jpg, 20261029174845_1.jpg, 20261029174849_1.jpg | — |
| RTO-T100 | 2026-11-01 | chore | closed | Rename repo to Mods.RogueTrader.Overwatch for mod organization (GitHub + folder + refs) | 3299d214639575f2d3570e103beac6af47a32f0c |
| RTO-T104 | 2026-11-04 | fix | closed | Vortex deploy drops the empty Bundles folder again, so blueprints silently fail to load (regression on RTO-T22) | dad9a6e |
| RTO-T105 | 2026-11-04 | fix | closed | Suppression desyncs (reported 2026-11-04): investigate and fix | 05fe06e |
