# Testing in game

A checklist for confirming the mod works, one case at a time. Every expected result below matches what
the code does today; if the game shows something else, that's a bug worth a ticket. Note each result in
the table at the end.

## Setup

1. Build and install: `dotnet build src/RogueTrader.Overwatch`. This writes the mod to
   `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Warhammer 40000 Rogue Trader\Modifications\RogueTrader.Overwatch`
   and adds it to `EnabledModifications` in `OwlcatModificationManagerSettings.json` in the same folder.
2. Start the game (or restart it, if it was running).
3. Logs live in `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Warhammer 40000 Rogue Trader\`:
   `Player.log` (Unity) and `GameLogFull.txt` (the game's own log). Search both for:
   - `RogueTrader.Overwatch` and `Suppression:`: every error the mod's code catches starts with one of
     these. There should be none.
   - `Failed loading blueprint`, `Patch file not found`, `Failed patch blueprint`: the mod loader
     couldn't read one of the mod's files.
   - Expected, healthy lines: `Initialize assembly: RogueTrader.Overwatch`, `Load blueprint:` once per
     file in `Blueprints\`, and `Patching blueprint: ... (ec16ff5722314efd887fa64f6f6e2558)` (the
     enemies' shared fact) once a save is loaded.

Abbreviations: **AP** = action points (yellow), **MP** = movement points (blue), **cell** = one grid
square.

## A. Install and loading

**A1. The mod loads.** Start the game, open Owlcat's mod window (its key is listed under Settings,
Controls, General).
Expected: "Overwatch" by MindAttic is listed and enabled. The log has the healthy lines above and no errors.

**A2. Loading a save.** Load any save.
Expected: no `RogueTrader.Overwatch` errors in the log.

## B. Ability bar placement (party, RTO-T112)

The bar is 10 slots per row. The mod places each of Reflex Shot, Overwatch, Suppression and Hustle that
isn't already on a character's bar: first into free slots at the very end of row 2 (a window sized to the
whole set - slots 17–20 for 4 abilities), then the first free slot anywhere. It runs when an area loads,
when a companion is recruited and whenever the bar is refreshed for a character, whatever the game's
"auto-fill action bar" setting says.

**B1. Existing save.** Load a mid-game save, select each party member in turn.
Expected: every party member shows the four buttons in the last slots of row 2 (if those were empty),
with the custom icons.

**B2. New game.** Start a new game and reach the first fight.
Expected: as B1 for the Rogue Trader, and for each companion as they join.

**B3. Recruited companion.** Recruit a companion (or load a save just before a recruitment).
Expected: the new companion has the four buttons straight away, placed as in B1.

**B4. Mercenary.** Hire a mercenary (custom companion) and add them to the party.
Expected: they have the four buttons, placed as in B1, after the next area load at the latest.

**B4a. Pets.** With a pet in the party (including one with a ranged weapon), select it.
Expected: no Reflex Shot, Overwatch, Suppression or Hustle buttons on its bar.

**B5. Row 2 full.** Drag four other abilities onto the end of row 2, replacing the four buttons. Select
another character and back (or change area).
Expected: the four buttons reappear in the first free slots anywhere. The bar doesn't grow extra empty
rows. (If they don't reappear at all, the game counted the replace as a removal: see B6, and note it.)

**B6. Removing a button.** Drag one of the four buttons off the bar. Change area, save and load.
Expected: it stays off. The mod never re-adds a button the player removed. (Drag it back from the
character's abilities to restore it.)

**B7. Moving a button.** Drag a button to another slot. Change area, save and load.
Expected: it stays where you put it, and isn't duplicated.

## C. Greyed out without a gun

The buttons are usable only when the **active weapon set** holds a ranged weapon (pistol, basic or
heavy) in the main hand, the off hand or an extra limb. Otherwise they're greyed out. They're also
unusable out of combat. Check each case in combat, on a party member's ability bar:

| # | Active weapon set | Expected |
|---|---|---|
| C1 | Rifle (basic, two-handed) | Lit |
| C2 | Pistol | Lit |
| C3 | Heavy weapon | Lit |
| C4 | One melee weapon | Greyed |
| C5 | Two melee weapons | Greyed |
| C6 | Sword in main hand, pistol in off hand | Lit; shots use the **pistol** |
| C7 | Nothing (unarmed) | Greyed |
| C8 | Grenades or other thrown weapons only | Greyed |
| C9 | A character with mechadendrites (e.g. Pasqal) holding only melee weapons | Greyed, and no errors in the log |
| C9a | A character with mechadendrites (e.g. Pasqal) with a ranged weapon in a hand (RTO-T53) | Lit, and no errors in the log |

**C10. Swapping weapon sets.** Set 1 a rifle, set 2 a sword. Swap sets in combat.
Expected: the buttons grey out and light up with each swap, immediately.

**C11. Unequipping mid-combat.** In combat, open the inventory, unequip every ranged weapon from the
active set, close the inventory.
Expected: greyed as soon as the inventory closes. Re-equip a gun: lit again.

**C12. Out of combat.** Walk around outside combat.
Expected: the buttons are on the bar but can't be used.

## D. Reflex Shot (redesigned RTO-T111)

Ends your turn. Costs 1 AP, even with a Heavy weapon (RTO-T82). No aiming: the instant it's cast, it fires
once at a randomly chosen enemy within your weapon's range and line of sight. It keeps your own hit chance
in full (Ballistic Skill, range falloff, cover, talents, the weapon's own bonus - no penalty of any kind)
and deals 33% of the weapon's damage.

**D1. Cast.** In combat, with a gun and at least one enemy in range and line of sight, press Reflex Shot.
Expected: no targeting step at all (no circle, no facing to pick); a shot fires at once at one of the
enemies in range, shown in the combat log like a normal attack; your turn ends.

**D2. Random target.** Cast Reflex Shot several times (reload a save between casts, or across several
fights) with two or more enemies in range and line of sight.
Expected: over several casts, different enemies get picked, not always the same one or always the nearest.

**D2d. Damage (RTO-T82).** Note a character's damage range against an enemy for a normal shot, then let a
Reflex Shot from them fire at that enemy.
Expected: the damage is about 33% of a normal shot's (other damage modifiers still apply), and the
damage breakdown names Reflex Shot as the reduction.

**D2e. Burst weapon (RTO-T82).** Reflex Shot with a burst-fire weapon (e.g. an autogun or heavy stubber).
Expected: every round of the burst rolls with 33% damage, all at the one randomly chosen target. A later
attack by the same shooter (e.g. its own next turn) has none of it.

**D2f. Hit chance unpenalized (RTO-T111).** Note a character's normal hit chance against an enemy for a
direct shot, then let a Reflex Shot from them fire at that same enemy (set up a fight with only that one
enemy in range so the target is guaranteed).
Expected: the Reflex Shot's hit chance matches the normal shot's exactly (only the damage is reduced, not
the chance to hit).

**D3. No enemy in range.** Cast Reflex Shot with every enemy out of weapon range or out of line of sight.
Expected: no shot fires; the AP is still spent and the turn still ends.

**D4. Radius follows the weapon.** With a pistol, only an enemy within the pistol's range is eligible; with
a long rifle, one farther out is too.
Expected: an enemy beyond the active weapon's range is never picked.

## E. Overwatch

Ends your turn. Costs 1 AP (2 with a Heavy weapon). **Shots = (the AP you had before casting − Overwatch's own cost) ÷ your weapon's attack cost, 1 to 2**: what the AP left after
paying for it would have bought, never more than 2 (RTO-T91). With a 1-AP attack and a non-Heavy weapon: 1 or 2 AP → 1 shot,
3 or more → 2; with a Heavy weapon (cost 2) and a 2-AP attack: 3 to 5 AP → 1, 6 or more → 2. The cone is sized by
the cursor: from 3 cells up to your weapon's range, 90° wide when close down to 20° at full range.
Every Overwatch shot counts as an attack of opportunity (RTO-T85, see K2).

**E1. Drag the cone.** Press Overwatch, move the cursor close to the character, then far away.
Expected: the highlighted cone grows longer and narrower as the cursor moves away, shorter and wider as
it comes close. It never gets shorter than 3 cells or longer than the weapon's range.

**E2. Locked cone matches the preview.** Click to cast.
Expected: no shot on cast, your turn ends, and the zone shown afterwards matches the preview you clicked.

**E3. Empty cone.** Cast Overwatch where no enemy is inside the cone.
Expected: it still starts (shots-left buff appears, see E8).

**E4. Shot cap.** Cast with 3 AP left (a non-Heavy weapon with a 1-AP attack). Let three or more enemies
walk through the cone.
Expected: the buff shows 2 (RTO-T91); at most 2 shots in total, then it ends and the buff disappears.

**E4a. Cap holds with more AP (RTO-T76, T91).** Cast with 4 AP left (same weapon).
Expected: the shots-left buff shows 2, not 3 or 4.

**E4b. One AP (RTO-T76, T91).** Cast with exactly 1 AP left (a non-Heavy weapon).
Expected: the buff shows 1 (nothing is left after paying for it, but there's always at least 1 shot); one
shot, then it ends.

**E4c. Follow-up shot (RTO-T83).** Cast with 3+ AP (2 shots, RTO-T91). Two or more enemies in the cone (all marked
Overwatched); one of them moves.
Expected: two shots back to back: the one that moved, then the next-closest Overwatched enemy to the
shooter (which didn't move). Then the overwatch ends. With only one enemy in the cone, the second shot
waits for its next move (see E6).

**E4d. Two AP (RTO-T91).** Cast with exactly 2 AP left (a non-Heavy weapon with a 1-AP attack).
Expected: the buff shows 1, not 2: the 1 AP left after paying for Overwatch buys one attack, so shooting
directly (2 shots) stays the better choice.

**E5. Spread fire.** Two enemies in the cone; enemy A moves first and is shot, then A moves again 2+
cells while enemy B is still in the cone unshot.
Expected: A's second move draws no fire. When B moves (or acts), B is shot.

**E6. 2-cell re-shot.** One enemy alone in the cone, 2+ shots left. It walks 6 cells through the cone.
Expected: shots at entry, after 2 cells and after 4 cells (until shots run out), not one per cell.

**E6a. Pushed into the cone.** Push an enemy into the cone, or push one already shot 2+ cells within it.
Expected: a shot, the same as walking in; a push of 2+ cells counts toward the re-shot distance.

**E7. Acting in place.** An enemy already shot stays put and shoots or uses an ability.
Expected: no second shot at it. (An enemy *not yet* shot that acts in the cone does draw a shot.)

**E8. Shots-left buff.** Cast with 3 AP (a 1-AP attack).
Expected: an "Overwatch" buff on the shooter with the Overwatch-shots icon, showing 2; it counts down
with each shot and disappears with the last one or at your next turn.

**E9. Save and load mid-overwatch.** Cast with 3+ AP (2 shots, RTO-T91), let one shot fire (buff shows 1), save, load.
Expected: the buff still shows 1 and the overwatch still fires 1 more shot. (Known: which enemies were
already shot isn't saved, so after loading every enemy in the cone counts as not yet shot.)

**E10. Ends at your turn.** Shots left when your next turn starts.
Expected: the overwatch and the buff end.

## F. Suppression

Ends your turn. Costs 1 AP (2 with a Heavy weapon). Target one enemy within weapon range. Until your next turn, leaving the
square it stands on, by any means, draws one real attack from you (once per move or ability, not per
square); acting in place draws nothing. While it's Suppressed, the target's own ranged attacks lose a
quarter of their hit chance (RTO-T86, eased from half in RTO-T87, see N).

**F1. Cast.** Press Suppression, pick an enemy in range.
Expected: no shot on cast, your turn ends, the enemy gets the "Suppressed" debuff with its icon.

**F2. Target moves.** On its turn the suppressed enemy moves.
Expected: one attack at it, early in the move, with normal hit chance and damage. If it moves again
later in its turn, one more attack for that move (one per move, not one per cell).

**F2a. Leaves by ability or push.** The suppressed enemy charges or jumps, or is pushed off its square.
Expected: one attack, the same as a move.

**F3. Target acts in place.** It shoots or uses an ability without moving.
Expected: no attack.

**F4. Other enemies.** A different enemy moves near you.
Expected: no attack from Suppression.

**F5. Cosmetic fire (RTO-T66, T67, T69).** For the whole suppression, on every turn, watch the hero.
Expected: under a second after Suppressed lands, then every 0.9–1.5 s while the hero is idle and can see
the target, the hero fires at it: the attack animation and the weapon's projectile streaking past the
target on its left or right (RTO-T81), never into it, and ending on the ground behind it.
No hit, no impact effect, no damage, no ammo used, no combat-log lines. Between shots the hero holds the
firing pose, weapon trained on the target. Pauses while a real attack (F2) plays; with a weapon that has no projectile (e.g. Pasqal's staff; RTO-T84) a glowing red line
(RTO-T65) joins the hero and the target the whole time.

**F5a. Damage interrupts (RTO-T67).** Suppress an enemy, then let anything damage the suppressor.
Expected: the Suppressed debuff, the red line and the cosmetic fire end at once. The same damage ends an
active Overwatch or Reflex Shot on the unit that took it.

**F6. Out of range or sight.** The target moves out of the hero's weapon range or line of sight.
Expected: no attack while it can't be targeted, and no cosmetic fire without line of sight.

**F7. Ends at your turn.** Your next turn starts.
Expected: the Suppressed debuff is gone.

**F8. Two suppressors.** Two party members suppress the same enemy; it moves.
Expected: one attack from each.

## G. Enemies

Enemies get the three abilities on their ranged weapons (in the weapon section, not an ability bar), and
the AI uses them only once it has finished moving and hasn't spent any AP this turn, with a ranged
weapon in its active set (main hand, off hand or extra limb; RTO-T79):

**G1. Normal turns unchanged.** Enemies that can shoot someone in the open.
Expected: they shoot normally. No overwatch.

**G2. Overwatch when nothing to shoot.** An enemy with 2+ AP ends its move with no one it can shoot, but
a party member within its weapon range + 8 cells.
Expected: it casts Overwatch, a cone aimed at the nearest party member and reaching toward them (capped
at its weapon range), and its turn ends. Walking into the cone draws its shots.

**G3. Reflex Shot with 1 AP.** Same, but the enemy has only 1 AP (or Overwatch can't be aimed).
Expected: Reflex Shot instead. A Heavy-armed enemy with 1 AP casts Reflex Shot too: it costs 1 AP
whatever the weapon (RTO-T82).

**G4. Suppression when everyone is in full cover.** An enemy can target party members, but every one of
them is in full cover.
Expected: it suppresses the nearest one not already suppressed. That character then draws a shot each
time they move.

**G5. Nobody near.** No party member within weapon range + 8 cells.
Expected: no overwatch; the enemy's turn ends as before.

**G6. Not avoiding overwatch (RTO-T87, T88).** Put Overwatch down over the enemies' approach, then end
your turn.
Expected: enemies take the path they'd take anyway, walking into the cone and drawing its shots, instead
of routing around it.

**G6a. Enemy pets.** A fight with enemy animals or pets, ideally one with a ranged attack.
Expected: they never use Reflex Shot, Overwatch or Suppression.

**G7. Allied NPCs.** In a fight with allied non-party units holding guns.
Expected: they follow the same rules as enemies (G1–G5), against their own enemies. Watch for anything odd.

**G8. Walking through Overwatch (RTO-T87, T88).** A hero casts Overwatch with its cone over the enemies'
approach, then ends the turn.
Expected: an enemy whose shortest path crosses the cone walks straight through it rather than around it,
and draws the shot. (Reflex Shot no longer arms a zone to route around, RTO-T111: see D instead.)

**G9. Pinned enemy (RTO-T90).** Suppress a ranged enemy that has a hero in line of sight, then end your turn.
Expected: on its turn it mostly stays on its square and shoots back from there (its hit chance reduced,
N1–N5) rather than moving to a better spot; if it does move, it draws the suppressor's attack (F2).

## H. Icons

**H1.** Check every icon: the three buttons (Reflex Shot, Overwatch, Suppression), the shots-left buff
(E8) and the Suppressed debuff (F1).
Expected: the custom art from `assets/`, not the game's borrowed placeholders (Take Aim, Killzone, etc.).
Tooltips show the names and descriptions from `mod/Localization/enGB.json`.

## I. Uninstall

**I1.** Outside combat, disable the mod in the mod window, restart, load the save.
Expected: the save loads. The buttons are gone. Note any errors in the log.

## J. Heavy weapon AP cost (RTO-T51)

Overwatch and Suppression cost 1 extra AP (2 total) when the active ranged weapon is Heavy, on top of
the normal 1 AP for a Pistol or Basic weapon. Reflex Shot always costs 1 AP (RTO-T82).

**J1. Heavy weapon, 1 AP left.** Equip a Heavy weapon (e.g. a heavy bolter) with only 1 AP available.
Expected: Overwatch and Suppression are unusable (not enough AP for the 2 AP cost); Reflex Shot is
usable and costs 1 AP.

**J2. Heavy weapon, 2+ AP.** Same, with 2 or more AP available.
Expected: casting Overwatch or Suppression costs 2 AP, not 1; Reflex Shot costs 1. Repeat with a Pistol
or Basic weapon active: each of the three costs 1 AP.

## K. Suppression and Overwatch as real attacks of opportunity (RTO-T50, T85, T94)

**K1. Talent/trait bonus.** With a character who has a talent, feat or weapon trait that specifically
boosts attacks of opportunity (bonus to-hit or damage), cast Suppression and let the target move.
Expected: the triggered shot's hit-chance or damage breakdown shows that bonus, the same as it would on
a real melee attack of opportunity.

**K2. Overwatch shot (RTO-T85).** With the same character, cast Overwatch and let an enemy walk into the
cone.
Expected: the overwatch shot's hit-chance or damage breakdown shows the attack-of-opportunity bonus, next
to the "Overwatch" reach penalty (which is unchanged, see L1–L2). With 2 shots, the second (including a
follow-up shot, E4c) shows it too. A normal shot from the same character on its own turn doesn't.

**K3. Reflex Shot is no longer one (RTO-T111).** With the same character, let a Reflex Shot fire.
Expected: no attack-of-opportunity bonus in its breakdown: since RTO-T111 it's a direct cast on the
caster's own turn, not a reactive trigger.

## L. Overwatch snap-shot penalty and the Overwatched debuff (RTO-T54, T87)

**L1. Close, wide cone.** Cast Overwatch at the minimum reach (3 cells, 90°).
Expected: enemies inside get the Overwatched debuff at rank 1, and a shot at one shows a -1 "Overwatch"
line in the hit-chance breakdown, on top of the weapon's normal range falloff (the penalty runs 1 to 15,
30 before RTO-T87, and the rank always equals it).

**L2. Far, narrow cone.** Cast Overwatch aimed at the weapon's full range (narrowest cone).
Expected: enemies inside get the Overwatched debuff at rank 15, and a shot at one shows an extra -15
"Overwatch" line in the hit-chance breakdown, on top of the normal range penalty.

**L3. Live tracking.** With an active Overwatch cone, walk an enemy into it, then out of it, without
drawing a shot (e.g. moving less than the minimum re-shot distance isn't relevant here — any entry
should apply the debuff immediately).
Expected: the Overwatched debuff appears the instant the enemy's square is inside the cone and
disappears the instant it leaves, independent of whether it has been shot.

**L4. Multiple overwatchers.** Two party members overwatch the same enemy with cones of different
reach (e.g. one close, one far).
Expected: the enemy's Overwatched rank shows the higher of the two penalties.

**L5. Ends with the overwatch.** Let an Overwatch run out of shots, end from the shooter's next turn, or
get cancelled.
Expected: the Overwatched debuff is removed from everyone it covered within the next tick.

## M. Tile-edge outline (RTO-T56, T63, T64, T68, T70, T76)

**M1. Overwatch.** Cast Overwatch at various reaches.
Expected: a thin blue, 50%-transparent tile-edge outline matches the cone exactly (a short wide cone or a long narrow one). It
disappears when the overwatch runs out of shots, is cancelled or interrupted, or your next turn starts.
(Reflex Shot has no aiming step and no armed zone since RTO-T111, so it never shows an outline.)

**M3. Live preview.** Press Overwatch and move the cursor before clicking.
Expected: the outline already tracks the live preview as it resizes, not just after you click to cast it.

**M3a. Pausing while aiming.** While aiming (before clicking), hold the cursor still for several
seconds, then move it again.
Expected: the outline stays the whole time the ability is selected, however long the cursor sits still.

**M3b. Cancelling.** Press Overwatch, move the cursor to preview it, then cancel (right-click, Escape, or
select a different ability) without casting.
Expected: the outline and the hit-chance overtips disappear on the next frame.

**M4. Hit-chance overtips (RTO-T71, RTO-T80).** Aim Overwatch with enemies inside the cone.
Expected: each enemy inside shows the game's own hit-chance gauge and damage range above its health
bar (the same overtip a normal attack shows, centred on the unit), updated as the cursor moves: the
game's hit chance for the weapon with Overwatch's reach penalty added. Enemies outside the cone show
nothing. (Reflex Shot has no aiming step to preview since RTO-T111.)

## N. Suppressed units lose a quarter of their hit chance (RTO-T86, T87)

A Suppressed unit's final ranged hit chance, after every modifier and after the game's hit-chance cap, is
reduced to 75%, rounded to the nearest whole number (.5 rounds to even). RTO-T86 halved it, on the
uncapped value; RTO-T87 eased it to 75% of the capped chance, so every shooter loses the same quarter. It
isn't a line of its own in the breakdown: only the final chance changes. Melee and scatter attacks, and
single shots at destructibles, are unchanged.

**N1. Gauge (party member Suppressed).** Note a party member's hit chance at an enemy (e.g. 60%). Let an
enemy suppress them (G4), then on their turn aim the same attack at the same enemy from the same square.
Expected: the hit-chance gauge shows three quarters of the earlier value (60% → 45%). Once Suppressed
ends (F5a, F7), it's back to 60%.

**N2. Rolled chance (enemy Suppressed).** Note the hit chance an enemy's shot at a party member shows in
the combat log (e.g. 70%). Suppress that enemy, then let it shoot the same character from where it stands.
Expected: the combat log shows the shot rolled at three quarters of the earlier value (70% → 52; 52.5
rounds to the even 52). A burst rolls every round at the reduced chance.

**N3. Reflex Shot from a Suppressed unit.** A Suppressed unit casts Reflex Shot.
Expected: the Reflex Shot's hit chance (unpenalized since RTO-T111) is reduced to three quarters of the
unit's normal chance at that target, the same rounding as N1-N2.

**N4. Melee and scatter unchanged.** A Suppressed unit makes a melee attack, then a scatter (cone or
area) ranged attack.
Expected: neither hit chance is reduced; both match an unsuppressed attack.

**N5. Capped shooter (RTO-T87).** Note a very accurate character's hit chance at the game's cap (e.g.
95%), then let an enemy suppress them and aim the same attack.
Expected: the gauge shows 71% (95 × 0.75 = 71.25), not the cap: the quarter comes off the capped chance,
however far above the cap the uncapped value was.

## Results

| Case | Pass / Fail | Notes |
|---|---|---|
| A1 |  |  |
| A2 |  |  |
| B1 |  |  |
| B2 |  |  |
| B3 |  |  |
| B4 |  |  |
| B4a |  |  |
| B5 |  |  |
| B6 |  |  |
| B7 |  |  |
| C1–C9, C9a |  |  |
| C10 |  |  |
| C11 |  |  |
| C12 |  |  |
| D1 |  |  |
| D2 |  |  |
| D2a |  |  |
| D2b |  |  |
| D2c |  |  |
| D2d |  |  |
| D2e |  |  |
| D3 |  |  |
| D4 |  |  |
| D4a |  |  |
| D4b |  |  |
| D5 |  |  |
| E1 |  |  |
| E2 |  |  |
| E3 |  |  |
| E4 |  |  |
| E4a |  |  |
| E4b |  |  |
| E4c |  |  |
| E4d |  |  |
| E5 |  |  |
| E6 |  |  |
| E6a |  |  |
| E7 |  |  |
| E8 |  |  |
| E9 |  |  |
| E10 |  |  |
| F1 |  |  |
| F2 |  |  |
| F2a |  |  |
| F3 |  |  |
| F4 |  |  |
| F5 |  |  |
| F5a |  |  |
| F6 |  |  |
| F7 |  |  |
| F8 |  |  |
| G1 |  |  |
| G2 |  |  |
| G3 |  |  |
| G4 |  |  |
| G5 |  |  |
| G6 |  |  |
| G6a |  |  |
| G7 |  |  |
| G8 |  |  |
| G9 |  |  |
| H1 |  |  |
| I1 |  |  |
| J1 |  |  |
| J2 |  |  |
| K1 |  |  |
| K2 |  |  |
| K3 |  |  |
| L1 |  |  |
| L2 |  |  |
| L3 |  |  |
| L4 |  |  |
| L5 |  |  |
| M1 |  |  |
| M2 |  |  |
| M3 |  |  |
| M3a |  |  |
| M3b |  |  |
| M4 |  |  |
| N1 |  |  |
| N2 |  |  |
| N3 |  |  |
| N4 |  |  |
| N5 |  |  |
