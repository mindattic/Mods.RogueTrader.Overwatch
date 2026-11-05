# Design

## Abilities

### Shared rules
- Party members always have Reflex Shot, Overwatch and Suppression on their ability bar, greyed out
  unless the active weapon set holds a ranged weapon, plus Hustle (RTO-T109), which needs no weapon;
  the whole set sits at the end of row 2 by default (RTO-T112). Enemies get the first three on any
  ranged weapon (Category Pistol, Basic or Heavy, not Thrown) through its weapon slots, never Hustle.
  See "Who gets the buttons".
- Being pushed counts as moving for Overwatch and Suppression: the engine's overwatch reacts to any
  position change (`OverwatchController.HandleEntityPositionChanged`), so a push into Overwatch's cone
  draws fire, and Suppression fires when its target is pushed off its square. Reflex Shot fires once,
  immediately on cast (RTO-T111), so there's no armed zone left for a push to trigger.
- Pets never overwatch, even with a ranged weapon: they don't plan ahead. Party pets get no buttons,
  and the enemy AI never casts the abilities for a pet.
- Each shot is the weapon's normal single shot, with normal hit chance (ranged hit chance already
  scales with distance: `RuleCalculateHitChances.OnTriggerRanged` multiplies by
  `RuleCalculateAbilityDistanceFactor`) plus the ability's own effect on the real roll: Overwatch's reach
  penalty is added on top (section below); Reflex Shot and Suppression's shots have none - Reflex Shot's
  target is simply chosen at random (RTO-T111) rather than its chance scaled. Reflex Shot alone deals
  33% damage (RTO-T82, below).
- **Overwatch's and Suppression's triggered shots are attacks of opportunity (`IsAttackOfOpportunity`;
  RTO-T50, RTO-T85, RTO-T94):** taking a shot in the spur of the moment because the chance presented
  itself, exactly what a melee threat's free swing already is when something leaves its reach. Fired with
  a `Clone(false)` of the real trigger ability flagged `IsAttackOfOpportunity = true` (Overwatch's own
  seam, `Abilities_AttackOfOpportunity` on `PartOverwatch.TryTriggerAttack`; Suppression flags its own
  clone directly, since it doesn't run through `PartOverwatch`), so talents and traits that key off that
  flag (`CheckIfAttackOfOpportunityGetter`, `AttackOfOpportunityModifier`) apply to both. Reflex Shot
  carried this flag and drew from the same reaction budget through RTO-T94–T110; since RTO-T111 it's a
  direct cast on the caster's own turn and AP, not a reactive trigger, so it no longer flags
  `IsAttackOfOpportunity` or spends from the budget below.
  - **Shared reaction budget (RTO-T94):** the same per-round attack-of-opportunity budget a melee threat
    already draws from (`PartUnitCombatState.CanAttackOfOpportunity`/`MaxAttacksOfOpportunityPerRound`,
    the `AttackOfOpportunityCount` stat, 1 by default and raised by talents the way Combat Reflexes
    raises it on the tabletop) gates Overwatch's and Suppression's shots, checked at the moment each shot
    would actually fire and spent only then (`Abilities.CanMakeAttackOfOpportunity`/`SpentAttackOfOpportunity`).
    A mook who already reacted this round - a melee attack of opportunity, an earlier shot from either -
    can't also loose another: Overwatch's follow-up shot checks it again, independently of the AP-funded
    shot budget below, so a two-shot cone can still net only one shot for a capped unit. The base game
    already exempts player characters and Elite+ enemies from this cap entirely, so this only reins in
    regular enemy mooks and never costs the player a shot; it was chosen over a bespoke limiter because
    this ruleset already has its own "Combat Reflexes"-style reaction economy, and reusing it (rather
    than, say, XCOM's one-reaction-per-overwatching-unit model, which this mod's shot-budget and re-shot
    rules already cover on their own terms) keeps every kind of reaction - melee, Overwatch, Suppression -
    spent from the one pool the rest of the game already understands.
  - The three still keep distinct roles: Reflex Shot is a bit of chaos thrown in alongside a spare AP;
    Overwatch is an intentional setup for a shot with attack-of-opportunity bonuses; Suppression pins an
    enemy down, and a Suppressed unit's own ranged attacks lose a quarter of their hit chance (RTO-T86,
    eased from half in RTO-T87).
- **Rounds in the chamber (RTO-T93):** all three grey out, the same as any other ammo-gated ability, once
  the weapon they'd actually fire has no ammo left (`ItemEntityWeapon.CurrentAmmo <= 0`; a weapon that
  doesn't track ammo, `WarhammerMaxAmmo < 0`, is always available). Reflex Shot and Overwatch's blueprints
  carry the game's own `AbilityCasterMainWeaponHasAmmo`, but its stock check only reads the caster's main
  (or, with `SecondWeapon`, off) hand; a prefix on `IsCasterRestrictionPassed`, scoped to our two
  abilities, redoes it against whichever slot `Abilities.RangedWeapon` would actually fire from (main
  hand, off hand, or an extra limb), the same lookup the weapon-type check above already uses. Suppression
  doesn't carry the component: it targets an enemy directly, so its own cast-time weapon-type check
  already covers it the way it always has. Once an Overwatch cone is live, its shots can't outrun the
  chamber either: `AbilityOnTrigger` is always the weapon's own real attack ability
  (`Abilities.RangedWeapon`'s `AbilityData`, see "Which gun fires" below), so the engine's own
  `AbilityData.IsAvailable` ammo gate (`UnitUseAbility.OnAction`) silently withholds a shot that would
  outrun the magazine, Overwatch's follow-up shot included. Reflex Shot only ever fires once, right on
  cast, so this only matters at the moment it's cast.
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
  stops the victim's active Overwatch (`PartOverwatch.Stop`) and removes every Suppressed buff it cast
  (`DamageInterrupt.cs`). Reflex Shot fires at once on cast (RTO-T111) and has no armed window left to
  interrupt.
- In-game checks for every rule here are in [TESTING.md](TESTING.md).
- The game's AI treats every active overwatch area as a threat when choosing paths (`AiBrainHelper`
  reads `PartOverwatch.OverwatchArea`), whether or not it can see the shooter, so a cone would only ever
  be a no-go zone that never fires. This mod's cones are ignored instead (RTO-T87, T88): a postfix on
  `AiBrainHelper.GatherThreatsData` (`EnemyAi_OverwatchThreats`) removes active Overwatch overwatchers
  from each node's `overwatchUnits`, which `WarhammerPathAiMetricCostProvider` counts as path cost, so
  units move as they would anyway and get caught by the shots. The game's own overwatches are left as
  they are. How enemies use the abilities is under "Enemy AI" below.
- **Outline (RTO-T56, T63, T64, T68, T70):** the game's own area highlight is a shared red fill used for
  every hostile area and isn't easy to see, so `AbilityOutline.cs` draws thin blue lines along the tile
  edges, 50% transparent (`AbilityOutline.OverwatchColor`, RTO-T76; the suppression tether is unchanged),
  of the Overwatch cone: one `LineRenderer` per boundary tile edge (every side of a cell in the area that
  borders a cell outside it), placed through the grid's own transform at each cell's height, plus a
  vertical connector at any corner where edges meet at different heights (a crate top next to the floor).
  All lines share one `Hidden/Internal-Colored` alpha-blended material drawn on top of the terrain; no art
  asset. It's driven every frame by `VisualsDriver` (a hidden `DontDestroyOnLoad` object; the simulation
  tick doesn't run every frame while aiming): the ability the player is aiming (`Game.SelectedAbilityHandler`)
  keeps its preview, which the `GetOrientedPattern` prefix redraws whenever the aimed cone changes, for as
  long as it stays selected; an active overwatch keeps its outline on `PartOverwatch.OverwatchArea`;
  anything else (cast, cancelled, another ability picked) is hidden. `GetOrientedPattern` calls for other
  casters (the AI weighing targets) never draw a preview. Reflex Shot has no aiming step and no armed
  zone, so it never shows an outline (RTO-T111).

### Reflex Shot (RTO-T5, RTO-T21, redesigned RTO-T111)
Ends the turn, then at once fires one real shot at a randomly chosen enemy within the weapon's range and
line of sight - no facing, no waiting as a reaction. Costs 1 AP whatever the weapon (RTO-T82). A little
chaos on top of Overwatch's deliberate, aimed control of the ground: Overwatch still needs aiming to
matter, Reflex Shot is the free-for-all alternative.
- **No targeting step:** the blueprint has `CanTargetPoint: false`, `CanTargetSelf: true`, `Range:
  Personal` - casting it asks for nothing from the player, the same shape a self-buff takes. It carries
  no `AbilityEffectOverwatch`/`PartOverwatch` and no `WarhammerAbilityAttackDelivery` pattern anymore: the
  whole effect is a single `AbilityEffectRunAction` with a deliberately empty action list.
- **Firing (`ReflexShot.cs`):** a postfix on `AbilityEffectRunAction.Apply`, scoped to Reflex Shot's own
  ability, does the whole thing. `ReflexShot.RandomTargetFor` picks uniformly among every current enemy
  within the active ranged weapon's range (`Abilities.RangedWeapon`/`RangedAttack`) and line of sight
  (`caster.InRangeInCells`, `caster.HasLOS`), using the engine's own seeded random
  (`PFStatefulRandom.Mechanics`, the same source the stock `ContextRandomTargetWithinCellsFromCaster-
  PositionEvaluator` uses) rather than `UnityEngine.Random`: unlike this mod's purely cosmetic random
  picks (Suppression's near-miss direction, RTO-T105), this pick decides real damage and must stay in
  sync between multiplayer peers. The shot itself is a fresh `Clone(false)` of the weapon's attack, fired
  through `UnitOverwatchAttackParams` (`caster.Commands.Run`) - the same out-of-turn attack path
  Suppression's real shot uses - so hit chance, damage and the visuals are the normal ones for that
  weapon. If no enemy qualifies, nothing fires (the AP is still spent, the same as an Overwatch cast that
  catches nothing).
- **Hit chance:** no penalty of any kind - the shooter's own hit chance applies in full (Ballistic Skill,
  range falloff, cover, talents: the game's ranged calculation, `RuleCalculateHitChances.OnTriggerRanged`,
  runs untouched). The chaos is in who gets shot, not in a worse chance to hit them. This replaces the old
  retained-aim scale (RTO-T96: the cosine of the angle between a picked facing and the shot's actual
  direction, floored at 10% instead of going to zero), which no longer applies now that there's no facing
  to retain.
- **Damage (RTO-T82):** still 33% of the weapon's damage (`ReflexShot.DamagePercent`), unchanged by the
  redesign: a prefix on `RuleCalculateDamage.OnTrigger` (`ReflexShot_Damage`) adds a `PctMul_Extra`
  modifier under the Reflex Shot ability's name, keyed off a `ConditionalWeakTable<AbilityData,
  EntityFact>` (`ReflexShot.Shots`) that marks exactly the clone `ReflexShot.Fire` built, rather than the
  old facing/retained-aim bookkeeping; the game's other damage modifiers still apply.
- No more hit-chance preview for Reflex Shot: with no aiming step, there's nothing to preview.
  `HitChancePreview.cs` is Overwatch-only now; `ReflexShot.SetRawHitChance` (the helper that reapplies the
  game's own hit-chance caps and overkill crit bonus after a rescale) stayed, since Suppression's own hit-
  chance cut (`Suppression_ReduceHitChance`) still needs it.

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
  Overwatch ability's name, so it shows in the hit-chance breakdown and combat log. Captured at queue
  time because the last shot stops the overwatch before its roll, and so the overwatcher's other attacks
  (e.g. a melee attack of opportunity) never pick it up.
- **Attack of opportunity (RTO-T85, RTO-T94):** an Overwatch shot is a prepared attack of opportunity
  (see "Shared rules" above). For the duration of each shot,
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
  renamed from `Suppression_HalveHitChance`; `HitChancePercent` 75) runs after every modifier, and sets the
  reduced value through `ReflexShot.SetRawHitChance`, which re-applies the cap
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

### Hustle (RTO-T109)
Free of AP and MP, usable only on the caster's own first turn of a battle, once per battle: move up
to the grid-cell distance to whichever other current party member (`Game.Instance.Player.Party`) is
farthest away, so a character who starts a fight far from the group isn't penalized for it.
- **Range (`Hustle.RangeFor`):** the farthest other party member's `CellDistanceTo` the caster's own
  node (`CurrentUnwalkableNode`), the same pattern `OverwatchDebuff.ReachOf` uses for a cone's reach.
  Worked out fresh every time the ability's range is checked, by a postfix on
  `RuleCalculateAbilityRange.OnTrigger` (`Hustle_Range`); the blueprint's own `Range: Unlimited` is only
  a placeholder, the same role `Abilities_RangeFromActiveWeapon` plays for Overwatch and Suppression.
- **Free of cost:** `AbilityCustomMoveToTarget`'s `m_ActionsAfterMoved` runs a
  `WarhammerContextActionRestoreActionPoints` with `MovePointsToMax: true` right after the move, so the
  Movement Points it spent come back; the ability's own `ActionPointCost` is 0 and it doesn't end the
  turn, so the character can still act normally afterward.
- **First turn only, once per battle:** the blueprint's `AbilityCasterHasNoFacts` carries an empty fact
  list on purpose (its stock check would always pass); a prefix scoped to Hustle's own ability
  (`Hustle_FirstTurnOnly`) replaces it with the real gate. A unit's first turn is tracked in memory, not
  with a marker buff: `PartUnitCombatState.PrepareForNewTurn` fires once per unit's own turn start (not
  once per round), so the *second* time it fires for a unit in one battle, that unit is marked past its
  first turn (`Hustle.PastFirstTurn`). `JoinCombat`/`LeaveCombat` clear the tracking for that unit, so
  rejoining combat - a reinforcement, or leaving and coming back - gives it a first turn again.
- Kept out of `Abilities.All`/`Abilities.IsOurs` on purpose, in a separate `Abilities.BarPlaced` list
  instead (see "Placement" below): `All` also scopes `Abilities_ActionPointCostByWeaponSize`, which
  would otherwise charge Hustle a Heavy-weapon AP surcharge it was never meant to have.
- Granted the same way as the other three: added to `Overwatch_PartyFeature`'s `AddFacts` (Main.cs's
  `PartyGrant`), so every non-pet party member has it from the start, no weapon required.

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
  resize patch sizes the cone to reach it) with 2+ AP, or **Reflex Shot** with 1 AP (or when the cone
  can't be aimed) - a self-cast, same as the player's own, with no aiming needed since RTO-T111 fires
  at a random enemy in range regardless. Each still needs its cost (`AbilityData.IsAvailable`): Reflex
  Shot is 1 AP whatever the weapon (RTO-T82), so a Heavy-armed enemy with 1 AP casts Reflex Shot (it
  could never afford Overwatch, which needs 2+ AP anyway).
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
recruit) `Overwatch_PartyFeature`, an `AddFacts` of Reflex Shot, Overwatch, Suppression and Hustle (RTO-T109),
and removes `Overwatch_Feature` from anyone an earlier build gave it to.
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
- **Placement** (`ActionBar.cs`, RTO-T112): the last slots of row 2 (the bar is `PartUnitUISettings.Slots`),
  a window sized to the whole granted set (`Abilities.BarPlaced`: Reflex Shot, Overwatch, Suppression,
  Hustle - slots 17-20 for 4, growing as more are added), else the first free slot anywhere. Runs on
  grant and as a prefix on `PartUnitUISettings.TryToInitialize`, which the bar calls just before reading
  its slots, whether or not the game's auto-fill setting is on. An ability the player removed from the
  bar (`m_RemovedFromActionBar`) stays off. Previously slots 8-10 of row 1, then the last three of row 2,
  then row 3.
- **Range:** a character-owned ability has `AbilityData.Weapon == null`, so `Range: Weapon` resolved to
  nothing. A postfix on `RuleCalculateAbilityRange.OnTrigger` uses the active ranged weapon's attack range
  instead; Overwatch's cone cap and Suppression's target range follow it.
- **Which gun fires:** `Abilities.RangedWeapon` picks the first ranged weapon in the active set (main hand,
  off hand, extra limbs). `UseFirstWeaponAbility` would take `GetFirstWeapon`, the melee main hand when a
  sword and pistol are held, so a postfix on `AbilityEffectOverwatch.Apply` restarts the overwatch on the
  ranged weapon's attack; Suppression's real and cosmetic shots use it too.

**Cast delivery (Overwatch).** `WarhammerAbilityAttackDelivery`'s pattern delivery applies the ability's
effects once per unit inside the area (`AbilityExecutionProcess.TickDeliveryProcess`): an empty zone never
started overwatch, and several enemies restarted it once each. A prefix on its `Deliver` (`Abilities_DeliverOnce`,
scoped to `Abilities.IsOurs`, so it's a no-op for Suppression and Reflex Shot since RTO-T111, neither of which
carries this component) makes it deliver exactly once, to the clicked target. `WeaponAttack: None` already
means no attack is made on cast. Suppression targets one unit directly; Reflex Shot no longer targets an area
at all (RTO-T111).

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
`ReflexShot`, `Overwatch`, `OverwatchShots` (shots-left buff), `Suppression`, `Suppressed` (debuff),
`Hustle`.
