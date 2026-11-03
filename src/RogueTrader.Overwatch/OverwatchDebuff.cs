using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Controllers.TurnBased;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;

namespace RogueTraderOverwatch;

// Overwatched: a visual marker (Overwatched_Buff) on every enemy currently standing in an active Overwatch cone,
// whose rank is the snap-shot hit-chance penalty a shot from that cone carries, on top of the weapon's normal
// range falloff. Added, updated and removed live every tick (OverwatchController.Tick), not by the engine; when
// several overwatchers cover the same enemy, the rank shown is the worst (highest) of their penalties.
internal static class OverwatchDebuff
{
    public const string BuffGuid = "30f01920a70d4ec28860d0bd5e4193e3";
    public const int MinPenalty = 1;
    // On top of the game's own range falloff (half the base chance past half range), so a long cone isn't hit twice
    // as hard (RTO-T87; was 30).
    public const int MaxPenalty = 15;

    public static int PenaltyFor(int radius, int range)
    {
        int max = Math.Max(OverwatchCone.MinRadius, range);
        float t = max == OverwatchCone.MinRadius ? 1f : Mathf.Clamp01((radius - OverwatchCone.MinRadius) / (float)(max - OverwatchCone.MinRadius));
        // 1 at the shortest reach, not 0: a buff rank can't be 0, and the Overwatched rank shows this exact penalty.
        return Mathf.RoundToInt(Mathf.Lerp(MinPenalty, MaxPenalty, t));
    }

    // The cone's reach is read off the locked zone itself (the farthest cell of PartOverwatch.OverwatchArea from
    // the shooter), not the last cursor distance: GetOrientedPattern runs again for the same caster after the cast
    // (hover previews, UI refreshes), so a remembered radius drifts to the 3-cell minimum.
    public static int ReachOf(BaseUnitEntity shooter, PartOverwatch part) => ReachOf(shooter, part.OverwatchArea);

    public static int ReachOf(BaseUnitEntity shooter, IEnumerable<CustomGridNodeBase> area)
    {
        if (shooter.CurrentUnwalkableNode is not CustomGridNodeBase from)
            return OverwatchCone.MinRadius;
        int reach = 0;
        foreach (var node in area)
            reach = Math.Max(reach, from.CellDistanceTo(node));
        return Math.Max(reach, OverwatchCone.MinRadius);
    }

    public static int PenaltyOf(BaseUnitEntity shooter, PartOverwatch part) =>
        PenaltyFor(ReachOf(shooter, part), part.Ability.RangeCells);

    public static bool IsActiveCone(PartOverwatch part) =>
        part != null && !part.IsStopped && OverwatchCone.IsOurs(part.Ability);

    public static BlueprintBuff Blueprint => ResourcesLibrary.TryGetBlueprint<BlueprintBuff>(BuffGuid);

    // Overwatch shots queued but not yet rolled, by (shooter, target) UniqueId: one overwatcher can queue shots at
    // two enemies in the same tick before either rolls. An additive hit-chance modifier, unlike Reflex Shot's own
    // multiplicative retained-aim fraction (RTO-T96): the cone's snap-shot penalty isn't a turn-angle effect.
    // Shot is the ability the shot fires (Abilities_AttackOfOpportunity's per-shot clone), so every round of a burst
    // gets the penalty and no other attack by the shooter does; null when unknown, and then the first roll uses it up.
    public static readonly Dictionary<(string Shooter, string Target), (int Modifier, EntityFact Source, float At, AbilityData Shot)> Pending = new();
}

[HarmonyPatch(typeof(OverwatchController), nameof(OverwatchController.Tick))]
internal static class OverwatchDebuff_Tick
{
    private static void Postfix()
    {
        try
        {
            var blueprint = OverwatchDebuff.Blueprint;
            if (blueprint == null)
                return;

            // Highest penalty any active overwatcher currently has on each enemy in its cone.
            var best = new Dictionary<BaseUnitEntity, int>();
            if (TurnController.IsInTurnBasedCombat())
            {
                foreach (var caster in Game.Instance.State.AllBaseAwakeUnits)
                {
                    var part = caster.GetOptional<PartOverwatch>();
                    if (!OverwatchDebuff.IsActiveCone(part))
                        continue;
                    int penalty = OverwatchDebuff.PenaltyOf(caster, part);
                    foreach (var unit in Game.Instance.State.AllBaseAwakeUnits)
                    {
                        if (unit.LifeState.IsDeadOrUnconscious || !caster.IsEnemy(unit) || !part.Contains(unit))
                            continue;
                        if (!best.TryGetValue(unit, out var existing) || penalty > existing)
                            best[unit] = penalty;
                    }
                }
            }

            foreach (var unit in Game.Instance.State.AllBaseAwakeUnits)
            {
                var buff = unit.Buffs.GetBuff(blueprint);
                if (best.TryGetValue(unit, out var penalty))
                {
                    // Every enemy in the cone is marked; a rank can't be 0, so a minimum-reach cone (no penalty)
                    // shows rank 1.
                    int rank = Math.Max(1, penalty);
                    if (buff == null)
                        buff = unit.Buffs.Add(blueprint, unit);
                    if (buff == null)
                        continue;
                    if (buff.Rank < rank)
                        buff.AddRank(rank - buff.Rank);
                    else if (buff.Rank > rank)
                        buff.RemoveRank(buff.Rank - rank);
                }
                else
                {
                    buff?.Remove();
                }
            }
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: overwatched debuff");
        }
    }
}

// The actual accuracy hit: added to the real attack roll, under the Overwatch ability's name, exactly like Reflex
// Shot's own aim modifier. Worked out when the overwatch queues the shot (OverwatchCone_ChooseShots) and kept for
// that shooter and target only: the last shot stops the overwatch before its roll, and the overwatcher's other
// attacks (e.g. a melee attack of opportunity) must not pick it up.
[HarmonyPatch(typeof(RulePerformAttackRoll), nameof(RulePerformAttackRoll.OnTrigger))]
internal static class OverwatchDebuff_AimModifier
{
    private static void Prefix(RulePerformAttackRoll __instance)
    {
        try
        {
            if (OverwatchDebuff.Pending.Count == 0 || __instance.Initiator is not BaseUnitEntity shooter || __instance.Target is not BaseUnitEntity target)
                return;
            var key = (shooter.UniqueId, target.UniqueId);
            if (!OverwatchDebuff.Pending.TryGetValue(key, out var shot))
                return;
            if (Time.time - shot.At > ReflexShot.PendingSeconds || shot.Source == null || shot.Modifier == 0)
            {
                OverwatchDebuff.Pending.Remove(key);
                return;
            }
            if (shot.Shot != null && __instance.Ability != shot.Shot)
                return;
            if (shot.Shot == null)
                OverwatchDebuff.Pending.Remove(key);
            __instance.HitChanceRule.HitChanceValueModifiers.Add(shot.Modifier, shot.Source);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: overwatch aim penalty");
        }
    }
}
