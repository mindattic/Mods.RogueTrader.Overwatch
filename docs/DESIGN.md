# Design

## Abilities

### Shared rules
- Party members always have the three buttons on their ability bar (slots 8–10 by default), greyed
  out unless the active weapon set holds a ranged weapon. Enemies get them on any ranged weapon
  (Category Pistol, Basic or Heavy, not Thrown) through its weapon slots. See "Who gets the buttons".
- Being pushed counts as moving for all three: the engine's overwatch reacts to any position
  change (`OverwatchController.HandleEntityPositionChanged`), so a push into Reflex Shot's circle or
  Overwatch's cone draws fire, and Suppression fires when its target is pushed off its square.
- Pets never overwatch, even with a ranged weapon: they don't plan ahead. Party pets get no buttons,
  and the enemy AI never casts the abilities for a pet.
- Each shot is the weapon's normal single shot, with normal hit chance (ranged hit chance already
  scales with distance: `RuleCalculateHitChances.OnTriggerRanged` multiplies by
  `RuleCalculateAbilityDistanceFactor`) plus the ability's own effect on the real roll: Reflex Shot
  scales it by how much aim the facing retained, Overwatch's reach penalty is added on top (sections
  below). Suppression's shot has neither. Reflex Shot alone deals 33% damage too (RTO-T82, RTO-T96,
  below).
- **All three triggered shots are attacks of opportunity (`IsAttackOfOpportunity`; RTO-T50, RTO-T85,
  RTO-T94):** a Reflex Shot, an Overwatch shot and Suppression's real shot are all the same thing by
  nature — taking a shot in the spur of the moment because the chance presented itself, exactly what a
  melee threat's free swing already is when something leaves its reach. Reflex Shot's shot carried the
  flag only from RTO-T94 on; Overwatch's and Suppression's always did. Fired with a `Clone(false)` of the
  real trigger ability flagged `IsAttackOfOpportunity = true` (Reflex Shot and Overwatch share one seam,
  `Abilities_AttackOfOpportunity` on `PartOverwatch.TryTriggerAttack`; Suppression flags its own clone
  directly, since it doesn't run through `PartOverwatch`), so talents and traits that key off that flag
  (`CheckIfAttackOfOpportunityGetter`, `AttackOfOpportunityModifier`) apply to all three alike.
  - **Shared reaction budget (RTO-T94):** the same per-round attack-of-opportunity budget a melee threat
    already draws from (`PartUnitCombatState.CanAttackOfOpportunity`/`MaxAttacksOfOpportunityPerRound`,
    the `AttackOfOpportunityCount` stat, 1 by default and raised by talents the way Combat Reflexes
    raises it on the tabletop) now gates all three shots too, checked at the moment each shot would
    actually fire and spent only then (`Abilities.CanMakeAttackOfOpportunity`/`SpentAttackOfOpportunity`).
    A mook who already reacted this round — a melee attack of opportunity, an earlier shot from one of
    these three — can't also loose another: Overwatch's follow-up shot checks it again, independently of
    the AP-funded shot budget below, so a two-shot cone can still net only one shot for a capped unit.
    The base game already exempts player characters and Elite+ enemies from this cap entirely, so this
    only reins in regular enemy mooks and never costs the player a shot; it was chosen over a bespoke
    limiter because this ruleset already has its own "Combat Reflexes"-style reaction economy, and
    reusing it (rather than, say, XCOM's one-reaction-per-overwatching-unit model, which this mod's
    shot-budget and re-shot rules already cover on their own terms) keeps every kind of reaction —
    melee, Reflex Shot, Overwatch, Suppression — spent from the one pool the rest of the game already
    understands.
  - The three still keep distinct roles: Reflex Shot is the low-power, low-chance last move to spend a
    spare AP on; Overwatch is an intentional setup for a shot with attack-of-opportunity bonuses;
    Suppression pins an enemy down, and a Suppressed unit's own ranged attacks lose a quarter of their
    hit chance (RTO-T86, eased from half in RTO-T87).
- **Rounds in the chamber (RTO-T93):** Reflex Shot and Overwatch grey out, the same as any other
  ammo-gated ability, once the weapon they'd actually fire has no ammo left (`ItemEntityWeapon.CurrentAmmo
  <= 0`; a weapon that doesn't track ammo, `WarhammerMaxAmmo < 0`, is always available). Both blueprints
  carry the game's own `AbilityCasterMainWeaponHasAmmo`, but its stock check only reads the caster's main
  (or, with `SecondWeapon`, off) hand; a prefix on `IsCasterRestrictionPassed`, scoped to our two
  abilities, redoes it against whichever slot `Abilities.RangedWeapon` would actually fire from (main
  hand, off hand, or an extra limb), the same lookup the weapon-type check above already uses. Suppression
  doesn't carry the component: it targets an enemy directly rather than firing from a chosen facing or
  cone, so its own cast-time weapon-type check already covers it the way it always has. Once an overwatch
  is live, its shots can't outrun the chamber either: `AbilityOnTrigger` is always the weapon's own real
  attack ability (`Abilities.RangedWeapon`'s `AbilityData`, see "Which gun fires" below), so the engine's
  own `AbilityData.IsAvailable` ammo gate (`UnitUseAbility.OnAction`) silently withholds a shot that would
  outrun the magazine, Overwatch's follow-up shot included.
- All three cost 1 AP (the blueprints' `ActionPointCost`, and the slot `AP` on enemy weapons). Overwatch
  and Suppression cost 1 more AP when the active ranged weapon is Heavy (`WeaponHeaviness.Heavy`,
  RTO-T51): a prefix on `RuleCalculateAbilityActionPointCost.OnTrigger` adds to `CostBonus`, the same
  field the base game's own Heavy-only AP surcharges use (`WarhammerOverrideAbilityApCost`,
  `AffectOnlyHeavy`). Reflex Shot is a quick reaction and always costs 1 AP, whatever the weapon (the
  prefix skips it, RTO-T82). They end the turn (`WarhammerEndTurn`) and only work in combat
  (`CombatStateRestriction: InCombatOnly`).
- Overwatch ends when the overwatcher starts its next turn, acts, or combat ends
  (`OverwatchController`).
- **Damage interrupts (RTO-T67):** any real damage (`RuleDealDamage`, not fake previews, result > 0)
  stops the victim's Overwatch or Reflex Shot (`PartOverwatch.Stop`) and removes every Suppressed buff it
  cast (`DamageInterrupt.cs`).
- In-game checks for every rule here are in [TESTING.md](TESTING.md).
- The game's AI treats every active overwatch area as a threat when choosing paths (`AiBrainHelper`
  reads `PartOverwatch.OverwatchArea`), whether or not it can see the shooter, so a cone or circle would
  only ever be a no-go zone that never fires. This mod's areas are ignored instead (RTO-T87, T88): a
  postfix on `AiBrainHelper.GatherThreatsData` (`EnemyAi_OverwatchThreats`) removes active Reflex Shot
  and Overwatch overwatchers from each node's `overwatchUnits`, which `WarhammerPathAiMetricCostProvider`
  counts as path cost, so units move as they would anyway and get caught by the shots. The game's own
  overwatches are left as they are. How enemies use the abilities is under "Enemy AI" below.
- **Outline (RTO-T56, T63, T64, T68, T70):** the game's own area highlight is a shared red fill used for
  every hostile area and isn't easy to see, so `AbilityOutline.cs` draws thin lines along the
  tile edges, 50% transparent, yellow for Reflex Shot and blue for Overwatch (`AbilityOutline.ReflexColor`/
  `OverwatchColor`, RTO-T76; the suppression tether is unchanged), of the Reflex Shot circle or Overwatch cone: one `LineRenderer` per boundary tile edge
  (every side of a cell in the area that borders a cell outside it), placed through the grid's own
  transform at each cell's height, plus a vertical connector at any corner where edges meet at
  different heights (a crate top next to the floor). All lines share one `Hidden/Internal-Colored`
  alpha-blended material drawn on top of the terrain; no art asset. It's driven every frame by `VisualsDriver` (a
  hidden `DontDestroyOnLoad` object; the simulation tick doesn't run every frame while aiming): the
  ability the player is aiming (`Game.SelectedAbilityHandler`) keeps its preview, which the
  `GetOrientedPattern` prefixes redraw whenever the aimed area changes, for as long as it stays
  selected; an active overwatch keeps its outline on `PartOverwatch.OverwatchArea`; anything else
  (cast, cancelled, another ability picked) is hidden. `GetOrientedPattern` calls for other casters
  (the AI weighing targets) never draw a preview.

### Reflex Shot (RTO-T5, RTO-T21)
XCOM 2 overwatch: a circle around the unit, every direction, whose radius is the equipped weapon's own
range (RTO-T62; no fixed size). It fires once at the first enemy that moves or acts inside
(`Mode: Overwatch`, `HitOnce`), then ends. Costs 1 AP whatever the weapon (RTO-T82). Ends the turn.
- **Radius:** the blueprint's circle (radius 10) is only a placeholder; `ReflexShot_CentreOnShooter`
  rebuilds it with `AoEPattern.CopyAndOverrideRadius` at the weapon's `RangeCells`, and that one prefix
  feeds the preview, `GetOverwatchArea` (the real trigger zone) and the outline. The tooltip's
  "10-cell radius" line is drawn from the static blueprint pattern and can't follow it.
- **Facing:** casting asks for a point within that radius (`Range: Custom`, `CanTargetPoint`,
  `ShouldTurnToTarget`; the click range is set to the weapon's range by a postfix on
  `RuleCalculateAbilityRange.OnTrigger`, `ReflexShot_RangeFromWeapon`); the unit turns to face it, the direction it expects
  enemies from, lateral or diagonal. The circle stays centred on the unit whatever is clicked: a
  prefix on `AbilityAoEPatternSettings.GetOrientedPattern` passes the caster's node as the target node
  (`ReflexShot_CentreOnShooter`), the same seam the preview and `GetOverwatchArea` use.
- **Retained aim (RTO-T96):** a reflex shot keeps the shooter's own hit chance in full (Ballistic
  Skill, range falloff, cover, talents, the weapon's own bonus: the game's ranged calculation,
  `RuleCalculateHitChances.OnTriggerRanged`, runs untouched), then that result is scaled by how much
  of the weapon's aim was still on target when the shot broke: literally the cosine of the angle
  between the facing the player picked and the direction the shot actually needed
  (`ReflexShot.RetainedFraction`) — the dot product of the two directions, 1 dead ahead, 0 at a
  quarter-turn and beyond, floored at `ReflexShot.MinRetainedFraction` (0.10) instead of going to zero,
  so a shot thrown from behind is still a sliver of a chance rather than an impossibility. This
  replaced a flat `33` base with a linear `+10`/`-30` turn modifier (RTO-T82, before it RTO-T72's flat
  `-33`): tied to nothing about the shooter, it made Reflex Shot relatively *weaker* the better a
  character's Ballistic Skill got, since a flat base never benefited from that skill the way a normal
  shot does. A postfix on `OnTriggerRanged` (`ReflexShot_BaseHitChance`) applies the scale after the
  game's own result is final: the game's caps (`ResultOverkillBorder`) are reapplied, and the overkill
  crit bonus (`HitChanceOverkill` on the Righteous Fury rule) is recomputed from the scaled value, the
  same way `Suppression_ReduceHitChance`'s flat percentage cut already does. Scatter shots and the
  border chance against destructibles are left as the game has them.
- **Damage (RTO-T82):** a Reflex Shot deals 33% of the weapon's damage (`ReflexShot.DamagePercent`): a
  prefix on `RuleCalculateDamage.OnTrigger` (`ReflexShot_Damage`) adds a `PctMul_Extra` modifier under
  the Reflex Shot ability's name; the game's other damage modifiers still apply.
- **Hit-chance preview (RTO-T71, RTO-T80):** while aiming Reflex Shot or Overwatch, each enemy inside
  the previewed area shows the game's own hit-chance overtip (round gauge and damage range, as for a
  normal attack) for the shot it would take. A postfix on `AbilityDataHelper.GatherAffectedTargetsData`
  replaces our ability's per-target `AbilityTargetUIData` (an area ability counts as "always hits", so
  the overtip showed only its icon) with the weapon attack's own, under our ability; while it's built,
  a prefix on `RuleCalculateHitChances.OnTrigger` adds Overwatch's reach penalty to
  `HitChanceValueModifiers` when that's the ability being aimed, so caps, cover, dodge and talents
  apply exactly as on the real roll (`HitChancePreview.cs`). For Reflex Shot, `HitChancePreview.Pending`
  is flagged `Reflex` and carries the retained-aim fraction instead (`ReflexFraction`), so the
  hit-chance and damage patches above apply to the preview too (scaled chance, 33% damage; RTO-T82,
  RTO-T96); the one-frame damage rule cache is invalidated around it, keeping the reduced damage out of
  every other preview.
  The angle is between the facing (unit toward `PartOverwatch`'s saved target point, `m_Target`) and
  the unit-to-target direction on the ground. `Overwatch` mode stops the part as soon as the shot is
  queued, so a prefix on `PartOverwatch.TryTriggerAttack` works the fraction out and keeps it per
  shooter; a prefix on `RulePerformAttackRoll.OnTrigger` (the real attack roll, which triggers its own
  `HitChanceRule` first) marks the roll and its hit-chance rule with that fraction and the ability's
  fact as its source (`ReflexShot.Rolls`), so the hit-chance and damage patches above know to apply it.
  The damage breakdown still names Reflex Shot as the reduction (`ReflexShot_Damage`'s `PctMul_Extra`
  modifier); the hit-chance side has no line of its own since it's a rescale of the final result, not
  an added modifier — only the number itself changes, the same as Suppression's cut. UI hit-chance
  previews never see this marking (they go through the `HitChancePreview.Pending` fallback instead). It
  isn't used up by the first roll
  (RTO-T82): every roll the shooter makes at that same target within 5 s (`ReflexShot.PendingSeconds`)
  gets it, so every round of a burst counts as a Reflex Shot (retained-aim scale, 33% damage); after
  that it lapses, so no later attack (e.g. a Suppression shot) picks it up, and a new overwatch of any
  kind clears it (`ReflexShot_ForgetOnNewOverwatch`). The facing comes from saved state, so it survives
  save and load.

### Overwatch (RTO-T6)
- After pressing the button the cone resizes live with the cursor: farther gives a longer, narrower
  cone and closer a shorter, wider one. `OverwatchCone.ConeFor`: radius = the caster→cursor
  distance clamped to 3 cells … weapon range; angle runs linearly from 90° at 3 cells to 20° at
  weapon range. A prefix on `AbilityAoEPatternSettings.GetOrientedPattern` serves both the
  targeting preview and `GetOverwatchArea`, so the locked zone matches the preview. Click to lock it in.
- Casting costs 1 AP (2 with a Heavy weapon) and ends the turn. **Shots = (the AP the unit had before
  casting minus Overwatch's own AP cost) divided by its ranged weapon's attack AP cost, clamped to 1..2**:
  what the AP left after paying for Overwatch would have bought (`OverwatchCone.ShotsFor`, `MaxShots`,
  RTO-T76, T91; the AP and the cast cost, `Ability.CalculateActionPointCost()`, are read in a prefix on
  `UnitUseAbility.OnStart`, before the cost is paid). With a 1-AP attack and a non-Heavy weapon: 1 or 2
  AP → 1 shot, 3 or more → 2. With a Heavy weapon (cast cost 2) and a 2-AP attack: 3 to 5 AP → 1, 6 or
  more → 2.
- **Why at most 2:** XCOM's strongest overwatch upgrades top out near two shots per unit; late-game AP
  inflation (heroic acts, momentum) would turn an uncapped cone into a wave-clearer, and enemies use it
  too. Dividing by the attack's cost, after taking off Overwatch's own cost, keeps a direct shot the
  preferred choice and Overwatch the next best thing (RTO-T91): it never fires more shots than the unit
  could fire on its own turn, and with 2+ AP it fires fewer (at 1 AP it's the same single shot, but
  conditional and with the snap-shot penalty). It also closes the loophole where a weapon with a 2-AP
  attack got a shot per AP. The three keep distinct jobs: Reflex Shot the single cheap reaction,
  Overwatch two-target area denial, Suppression the single-target lock. Enemy AI still picks Overwatch
  only when it can shoot nobody and has 2+ AP (so 1 shot at 2 AP, 2 at 3+ with a 1-AP attack), and
  Reflex Shot with 1 AP.
- Enemies that move or act in the cone draw shots until the shots run out or the next turn starts.
- The shots left are the rank of an "Overwatch" buff on the shooter (`OverwatchShots_Buff`), which
  the engine adds and removes with the overwatch (`AbilityEffectOverwatch.ApplyingBuffs`). The
  rank shows the player the count and is saved with the game. The re-shot positions aren't saved:
  after a reload, every enemy in the cone counts as not yet shot.
- **Follow-up shot (RTO-T83):** when a shot fires and another is left, it's fired at once at the
  next-closest enemy (to the shooter) marked Overwatched in this cone and not yet shot at, so a 2-shot
  overwatch hits the enemy caught moving, then the next-closest one, then ends. With no such enemy the
  shot waits for the next trigger. A postfix on `PartOverwatch.TryTriggerAttack` calls it again for the
  second target (guarded so the follow-up chains no further); the engine queues both attacks.
- **Spread fire first:** while any enemy in the cone hasn't been shot yet, an already-shot enemy
  draws no fire.
- **Re-shots:** when there's no other choice, an already-shot enemy can be shot again, but only
  after it has moved at least 2 cells since its last shot (`MinReshotDistance`, grid distance).
- **Acting in place:** an enemy not yet shot that acts in the cone draws a shot (the engine's own
  trigger); an already-shot enemy acting without moving never draws another one.
- The rules live in a prefix and postfix on `PartOverwatch.TryTriggerAttack` (the cast uses
  `MultipleTargetOverwatch` + `HitManyTimes` so the engine never stops or skips on its own).
- **Snap-shot penalty (RTO-T54):** on top of the weapon's normal range falloff, a shot from the cone
  gets an extra hit-chance penalty that grows with how far the cone was aimed: -1 at the minimum reach
  (3 cells), linearly down to -15 at the weapon's full range (`OverwatchDebuff.PenaltyFor`,
  `MinPenalty` 1 / `MaxPenalty` 15; never 0, so the Overwatched rank always equals it). The maximum was
  -30 until RTO-T87: the game already halves the base chance past half range, so -30 hit long cones
  twice. The reach is read off the locked zone (the farthest cell of `PartOverwatch.OverwatchArea` from
  the shooter). It is worked out when the overwatch queues the shot (`OverwatchCone_ChooseShots`) and kept per (shooter, target) in
  `OverwatchDebuff.Pending`, then added to that shot's real roll (`RulePerformAttackRoll`) under the
  Overwatch ability's name, the same seam Reflex Shot uses, so it shows in the hit-chance breakdown and
  combat log. Captured at queue time because the last shot stops the overwatch before its roll, and so
  the overwatcher's other attacks (e.g. a melee attack of opportunity) never pick it up.
- **Attack of opportunity (RTO-T85, RTO-T94):** an Overwatch shot is a prepared attack of opportunity,
  same as Reflex Shot's own single shot (see "Shared rules" above). For the duration of each shot,
  `Abilities_AttackOfOpportunity` swaps the part's trigger ability (`PartOverwatch.m_AbilityOnTrigger`,
  shared by both abilities) for a `Clone(false)` flagged `IsAttackOfOpportunity = true`, and its postfix
  puts the original back, so feats and traits that key off that flag (`CheckIfAttackOfOpportunityGetter`,
  `AttackOfOpportunityModifier`) apply, the same as Suppression's attack (RTO-T50). The same patch gates
  the shot on the shared per-round reaction budget first, high priority so `OverwatchCone_ChooseShots`
  never reaches its own shots-left bookkeeping for a shooter with no reaction left. The follow-up shot
  goes through the same prefix and gets it too (and the earlier shot's reaction has already been spent by
  the time it's chosen, so a capped unit's follow-up is the one actually likely to be denied). The
  snap-shot penalty above is unchanged.
- **Overwatched debuff (RTO-T54):** every enemy currently standing in an active Overwatch cone carries
  a visible `Overwatched_Buff`, whose rank is the penalty (percent) that cone's shots carry; a postfix
  on `OverwatchController.Tick` adds, updates and removes it live as enemies cross in or out, or when
  the overwatch ends, independent of the engine's own buff lifecycle. An enemy covered by several
  overwatchers shows the worst (highest) of their penalties. Placeholder icon (no custom art yet).

### Suppression (RTO-T8)
- Target one enemy. Until your next turn, it takes a ranged attack of opportunity (normal hit and
  damage) each time it leaves the square it's standing on, by any means: walking, a charge or jump,
  or being pushed. One shot per command, so a long move is one shot, not one per square. Shooting or
  using abilities in place draws nothing, and other enemies are ignored. Ends the turn.
- For the whole suppression, on every turn, the hero keeps firing at the target as a visual cue only
  (RTO-T66, RTO-T67): the weapon's firing animation and its projectile flying at the target, with
  `SetDoNotDeliverHit` and `SetDoNotPlayHitEffect`, so no hit, damage, ammo, rolls or combat-log lines.
  Between shots the suppressor holds a firing pose (RTO-T69): each shot's attack animation is frozen at
  45% of its length (`AnimationBase.SetSpeed(0)`) until the next shot replaces it, and released when the
  suppression ends or the unit has a real command to play. The first one comes 0.3–0.8 s after the target is Suppressed, then every 0.9–1.5 s
  while the hero is idle and has line of sight; it pauses while the real attack of opportunity plays.
- The real attack only fires if the target can still be targeted (`CanTarget`: range and line of
  sight). Several suppressors each fire once per move.
- **Tether (RTO-T65):** while the Suppressed buff lasts, a glowing red line runs (RTO-T84: only when the suppressor's ranged attack has no projectile, e.g. Pasqal's staff; a projectile weapon's near-miss fire is its cue) from each suppressor's
  eyes to the target's chest (`SuppressionLine.cs`, synced every frame by `VisualsDriver` from the
  buffs themselves): a thin bright core over a wide translucent halo, additive-blended so it reads as a
  glow, pulsing slowly. No asset, Unity's built-in `Hidden/Internal-Colored` shader.
- How it's built (`Suppression.cs`): the ability applies a visible **Suppressed** buff to the
  target (1 round, `BuffEndCondition.TurnStartOrCombatEnd`, which `BuffsController` removes when
  the buff's *caster* starts its turn). It doesn't use `PartOverwatch`, so the target's actions
  can't trigger it. The target's square is recorded when the buff is added (`IUnitBuffHandler`), and
  rebuilt from the saved buffs after a load. When a position change (`IEntityPositionChangedHandler`)
  puts it on a different square, one shot per suppressor is queued, unless the target's current
  command already drew one. A push has no command of the target's own, so it counts under the command of
  whoever's turn it is (one shot per pushing command); a displacement neither explains draws at most one
  shot per `Tick` (RTO-T105: was a one-shot-a-second `Time.time` throttle, but wall-clock time isn't
  synced between multiplayer peers, so host and client could reach different verdicts and desync).
  A postfix on `OverwatchController.Tick` fires the queue as `UnitOverwatchAttackParams`,
  the engine's own out-of-turn path, so hit chance and damage are normal. The ability data is a fresh
  `Clone(false)` of the weapon's attack, not the shared instance the unit also fires its normal shots
  with, flagged `IsAttackOfOpportunity = true` (RTO-T50) so feats and traits that key off that flag
  (`CheckIfAttackOfOpportunityGetter`, `AttackOfOpportunityModifier`) apply here too, matching the
  "ranged attack of opportunity" description above — they didn't before, despite the description. The same tick runs the
  cosmetic fire: `UnitAnimationManager.CreateHandle(MainHandAttack)` turned toward the target, then a
  `ProjectileLauncher` with `SetDoNotDeliverHit` and `SetDoNotPlayHitEffect`, aimed not at the target but
  at a point past it and 1.2–2 m to one side, near the ground (RTO-T81), so every round visibly misses, using
  Unity's random so the game's seeded random and state hash are untouched. The held pose's freeze check
  runs every frame (`Suppression.FreezePoses`, from `VisualsDriver`), since the tick is too sparse to
  catch the hold frame.
- **Suppressed AI units know they're pinned (RTO-T90):** in the same `GatherThreatsData` postfix
  (`EnemyAi_OverwatchThreats.MarkPinned`), a Suppressed unit's own squares list its suppressors in
  `aooUnits`. `WarhammerPathAiMetricCostProvider` charges a step that leaves a node whose `aooUnits` aren't
  in the next node's as a provoked attack (1000 path cost, `ProvokedAttacks`), the same as walking out of a
  melee enemy's reach, so the AI weighs moving like provoking an attack of opportunity and mostly stays put
  and shoots back from where it stands, at its reduced hit chance and from a worse range or angle.
- **Suppressed units lose a quarter of their hit chance (RTO-T86, RTO-T87):** on top of the attack of
  opportunity, a unit carrying the Suppressed buff keeps 75% of its final ranged hit chance (RTO-T86
  halved it; RTO-T87 eased it to 75%: Suppression is a pin, not a disable, and can be a turn's last spare
  AP). A low-priority postfix on `RuleCalculateHitChances.OnTriggerRanged` (`Suppression_ReduceHitChance`,
  renamed from `Suppression_HalveHitChance`; `HitChancePercent` 75) runs after every modifier and after
  Reflex Shot's own retained-aim scale (so a Suppressed unit's Reflex Shot is reduced too), and sets the
  reduced value through `ReflexShot_BaseHitChance.SetRawResult`, which re-applies the cap
  (`ResultOverkillBorder`) and recomputes the overkill crit bonus. Since RTO-T87 it scales the final,
  capped chance (`ResultHitChance`), not the uncapped raw value, so every shooter loses the same quarter
  (`Mathf.RoundToInt`, which rounds .5 to even): a capped 95 becomes 71, 60 becomes 45, 70 (52.5) becomes 52.
  It's the same rule the hit-chance gauge reads, so the preview shows it too. Melee
  attacks (`OnTriggerRanged` doesn't run), scatter attacks and single shots at destructibles (the game's
  border chance) are left alone.
- Data alone was close but not enough: `MovedInCombatTrigger` fires only after a move ends, and
  `ContextActionAttackWithFirstWeaponAbility` queues the attack instead of firing out of turn.

Dropped: Lane Overwatch (RTO-T9). Kill Zone (RTO-T7) and the drag-to-size cone (RTO-T12) merged
into Overwatch.

### Enemy AI (RTO-T11)
Enemies get the buttons through `CommonMobFact`, and the AI considers every ability a unit has
(`DecisionContext.GetSortedAbilityList`). Left alone it sorts end-turn abilities last
(`IsFirstAbilityBetter`) but still casts them on leftover AP: Suppression goes through
`SingleTargetSelector` at any shootable enemy, and Overwatch through `AOETargetSelector`, which
only aims at enemies already inside the cone. So `EnemyAi.cs` hides the three from that search
(postfix on `DecisionContext.IsUsableAbility`) and decides in one place (prefix on
`TaskNodeSelectAbilityTarget.TickInternal`). That runs only for non-player units in turn-based
combat that hold a ranged weapon, haven't spent any AP this turn, and are done moving: no movement
points left, or the after-move pick.
- Some enemy can be shot, but every one is in full cover: **Suppression** on the nearest one not
  already suppressed ("can't get a good shot").
- Some enemy can be shot in the open: nothing changes, the normal attack runs.
- No enemy can be shot, but one is within weapon range + 8 cells (`ApproachMargin`): **Overwatch**
  aimed at the nearest one (the target point sits at its distance, capped at weapon range, so the
  resize patch sizes the cone to reach it) with 2+ AP, or **Reflex Shot** with 1 AP (or when the
  cone can't be aimed), facing the nearest enemy, where one shot in a full circle beats one shot in a cone.
  Each still needs its cost (`AbilityData.IsAvailable`): Reflex Shot is 1 AP whatever the weapon
  (RTO-T82), so a Heavy-armed enemy with 1 AP casts Reflex Shot (it could never afford Overwatch, which
  needs 2+ AP anyway).
- The ranged-weapon check uses the first ranged weapon in the active set (`Abilities.RangedAttack`:
  main hand, off hand, extra limbs; RTO-T79), so an enemy with a sword in the main hand and a pistol in
  the off hand overwatches with the pistol. The rule covers every non-player unit, allied NPCs included.
- Otherwise nothing, and the turn ends as before.

## The engine's overwatch (Code.dll)

- `AbilityEffectOverwatch` (TypeId `0966a95df3bfa574e855a652cc54afa2`) on an ability creates a
  `PartOverwatch` on the caster with a zone from the ability's attack pattern
  (`GetOverwatchArea` → `GetPatternSettings().GetOrientedPattern(ability, casterNode, targetNode)`).
  Fields: `Mode` (`Overwatch`, `SuppressionFire`, `MultipleTargetOverwatch`), `HitsPerTarget`
  (`HitOnce`, `HitManyTimes`), `UseFirstWeaponAbility` (fire the weapon's first ability) or
  `m_AbilityOnTrigger`, and `ApplyingBuffs` (buffs on the caster while it's active).
- `PartOverwatch.TryTriggerAttack` runs a `UnitOverwatchAttack` command and, in `Overwatch` mode
  only, stops after the shot. `SuppressionFire` and `MultipleTargetOverwatch` behave the same in
  code. `HitOnce` remembers units already shot.
- `OverwatchController` collects triggers from `HandleEntityPositionChanged` (every position update,
  many per move) and `HandleUnitCommandDidAct` (abilities whose `UsingInOverwatchArea` is
  `WillCauseAttack`; every ability declares it), and stops a unit's overwatch when it starts its
  turn or runs any other command.
- Unused content shipped in the game: `Weapons/Abilities/Overwatch_BasicAbility`
  (`1d271e9472c142ecaa86103caa940010`, 2 AP, live but referenced by nothing, no strings or icon)
  with cone `OverwatchCone_AttackTemplate` (`8fbb90e7352a4aab843d913ac5ddc00a`, radius 8, 30°). A
  cut Veteran version offered cone or line variants (all `ShadowDeleted`).
- Pattern shapes: `BlueprintAttackPattern` with `Type` `Cone`, `Circle`, `Ray` or `Custom`, plus
  `m_Radius` and `m_Angle`. At runtime `AoEPattern.Cone(angle, radius)`, `AoEPattern.Circle(radius)`
  and `AoEPattern.CopyAndOverrideRadius` build any size.

## Who gets the buttons, and where (RTO-T4, RTO-T18)

**Enemies: on the weapon (RTO-T4).** Every non-player unit gets `BlueprintWarhammerRoot.CommonMobFact`
(`BaseUnitEntity.OnInitialize`). An `AddFacts` patched onto it gives them `Overwatch_Feature`, whose
`AddAbilitiesToCurrentWeapon` components (TypeId `b787865cc7284095914908b39df0a903`, one per ranged
category: Pistol, Basic, Heavy) add the three abilities as weapon slots on the active weapon. The
enemy AI (RTO-T11) works from those. `AddFacts` restores missing facts on load, so units in existing
saves get it too.

**Party: their own abilities, on the ability bar (RTO-T18).** Weapon-slot abilities only show in the
weapon's section and vanish with the weapon, and the bar's auto-fill (`PartUnitUISettings.TryToInitialize`)
skips them. So `PartyGrant` (Main.cs) gives every non-pet character in `Player.AllCharacters` (on area load and on
recruit) `Overwatch_PartyFeature`, an `AddFacts` of the three abilities, and removes `Overwatch_Feature`
from anyone an earlier build gave it to.
- **Greyed out without a gun:** every ability has `AbilityCasterHasWeaponWithRangeType: Ranged`, which
  checks the active set's main hand, off hand and extra limbs. The bar re-reads availability on weapon-set
  swaps (`IUnitActiveEquipmentSetHandler`) and when the inventory and other full-screen windows close.
  Melee only, two melee weapons or nothing in the hands greys all three out; a gun in either hand lights them.
  One quirk of that game component: a hand weapon with any burst-delivery ability also passes it. The
  other is confirmed, not just a risk (RTO-T53): its extra-limb loop reads `additionalLimb.Weapon` even
  when the limb is empty, throwing a `NullReferenceException` for any caster with an unfilled additional
  limb — confirmed in game on Pasqal, who has mechadendrites, and it greys out all three regardless of
  what's in his hands. A prefix on `AbilityCasterHasWeaponWithRangeType.IsCasterRestrictionPassed`,
  scoped to our three abilities only (`Abilities.IsOurs(OwnerBlueprint)`), redoes the same check with the
  empty-limb case skipped instead of dereferenced; every other ability in the game keeps hitting the
  original, unpatched method. Out of combat they're unusable (`InCombatOnly`).
- **Placement** (`ActionBar.cs`): slots 8–10 of the first 10-slot row (the bar is
  `PartUnitUISettings.Slots`), else the free slots among the last three of row 2, then row 3, else the
  first free slot. Runs on grant and as a prefix on `PartUnitUISettings.TryToInitialize`, which the bar
  calls just before reading its slots, whether or not the game's auto-fill setting is on. An ability the
  player removed from the bar (`m_RemovedFromActionBar`) stays off.
- **Range:** a character-owned ability has `AbilityData.Weapon == null`, so `Range: Weapon` resolved to
  nothing. A postfix on `RuleCalculateAbilityRange.OnTrigger` uses the active ranged weapon's attack range
  instead; Overwatch's cone cap and Suppression's target range follow it.
- **Which gun fires:** `Abilities.RangedWeapon` picks the first ranged weapon in the active set (main hand,
  off hand, extra limbs). `UseFirstWeaponAbility` would take `GetFirstWeapon`, the melee main hand when a
  sword and pistol are held, so a postfix on `AbilityEffectOverwatch.Apply` restarts the overwatch on the
  ranged weapon's attack; Suppression's real and cosmetic shots use it too.

**Cast delivery (both).** `WarhammerAbilityAttackDelivery`'s pattern delivery applies the ability's
effects once per unit inside the area (`AbilityExecutionProcess.TickDeliveryProcess`): an empty zone never
started overwatch, and several enemies restarted it once each. A prefix on its `Deliver` (matching all three
abilities, but only Reflex Shot and Overwatch have that delivery component; Suppression targets one unit
directly) makes them deliver exactly once, to the clicked target. `WeaponAttack: None` already means no attack
is made on cast.

## Mod packaging (RTO-T3)

An Owlcat modification folder, assembled by `dotnet` with no Unity:
- `OwlcatModificationManifest.json` (UniqueName, Version, DisplayName, Description, Author).
- `OwlcatModificationSettings.json` holding `BlueprintPatches: [{Guid, Filename, PatchType}]`
  (1 = Edit, which applies `<Filename>.jbp_patch`). It must also carry empty
  `BundlesLayout: {m_Guids: [], m_Bundles: []}` and `BundleDependencies: {m_List: []}`: the loader
  reads it with Unity's `JsonUtility`, which leaves missing sections null, and every scene load asks
  each mod for bundles through `Settings.BundlesLayout.BundleNames`, so without them the game hangs
  on its first loading screen (NullReferenceException in `OwlcatModification.TryLoadBundleAsync`).
- `Blueprints/**/*.jbp` (new blueprints) and `*.jbp_patch`. Patch fields: `FieldOverrides` take
  dotted paths, `ComponentsPatches` add components, `ArrayPatches` edit reference arrays
  (`Code.GameCore.Blueprints.BlueprintPatcher`).
- `Localization/enGB.json` as `{"strings": {"<key>": {"Text": "..."}}}`, the game's own pack format
  (`LocalizationPack`). The array format in Owlcat's template example doesn't load.
- `Assemblies/RogueTrader.Overwatch.dll`: loaded by the game, which calls the one static method
  marked `[OwlcatModificationEnterPoint]`. It applies Harmony patches with the game's own
  `0Harmony.dll` for the parts data can't express: the drag cone, the shot budget and re-shot
  rules, Suppression's movement-only trigger and its cosmetic fire.
- Enable it by adding its UniqueName to `EnabledModifications` in
  `OwlcatModificationManagerSettings.json`.

Icons (RTO-T10): `assets/<Name>.png` (square PNG; the current set is 64×64 with alpha) is copied to
the mod's `Icons/` folder. After the game loads the mod's blueprints
(`OwlcatModification.ApplyContent`), `Icons.cs` turns each into a `Sprite` and sets the blueprint's
private `BlueprintMechanicEntityFact.m_Icon`, so no Unity asset bundle is needed. The `m_Icon`
references in the `.jbp` files are game sprites that stay as fallbacks if a PNG is missing. Files:
`ReflexShot`, `Overwatch`, `OverwatchShots` (shots-left buff), `Suppression`, `Suppressed` (debuff).
