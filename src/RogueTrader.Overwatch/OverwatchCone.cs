using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Abilities.Components.PatternAttack;
using Kingmaker.UnitLogic.Abilities.Components.Patterns;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Parts;
using UnityEngine;

namespace RogueTraderOverwatch;

// The Overwatch ability: a cone sized by the cursor, one shot per AP left at cast (at most 2): the enemy caught moving,
// then at once the next-closest one in the cone.
internal static class OverwatchCone
{
    public const string AbilityGuid = "b5855687981e4ba9a7dfc8c4af03229f";

    public const int MinRadius = 3;
    public const int NearAngle = 90;
    public const int FarAngle = 20;
    public const int MinReshotDistance = 2;

    public static bool IsOurs(AbilityData ability) => ability?.Blueprint?.AssetGuid == AbilityGuid;

    // A direct shot stays the better choice (RTO-T91): the shots are what the AP left after paying Overwatch's own
    // cost would have bought (that AP / its weapon's attack cost): fewer than shooting directly with the AP it held
    // (the same single shot at 1 AP, but conditional and with the snap-shot penalty), and never more than MaxShots: late-game AP would otherwise turn one cone into a wave-clearer, on both
    // sides. At least 1.
    public const int MaxShots = 2;

    public static int ShotsFor(BaseUnitEntity unit, int actionPoints, int castCost)
    {
        int cost = Math.Max(1, Abilities.RangedAttack(unit)?.CalculateActionPointCost() ?? 1);
        return Mathf.Clamp((actionPoints - Math.Max(0, castCost)) / cost, 1, MaxShots);
    }

    public static int RadiusFor(int distance, int range) => Mathf.Clamp(distance, MinRadius, Math.Max(MinRadius, range));

    public static AoEPattern ConeFor(int distance, int range)
    {
        int radius = RadiusFor(distance, range);
        int max = Math.Max(MinRadius, range);
        float t = max == MinRadius ? 1f : (radius - MinRadius) / (float)(max - MinRadius);
        int angle = Mathf.RoundToInt(Mathf.Lerp(NearAngle, FarAngle, t));
        return AoEPattern.Cone(angle, radius);
    }

    // Shots left are the rank of this buff, which the engine adds and removes with the overwatch
    // (AbilityEffectOverwatch.ApplyingBuffs) and saves with the unit.
    public const string ShotsBuffGuid = "e5ce47452a6d47d78e88178f4ee7bba5";

    public static Buff ShotsBuff(BaseUnitEntity unit) =>
        unit.Buffs.GetBuff(ResourcesLibrary.TryGetBlueprint<BlueprintBuff>(ShotsBuffGuid));

    internal sealed class Watch
    {
        public readonly Dictionary<string, CustomGridNodeBase> LastShotAt = new();
    }

    // Keyed by the overwatcher's UniqueId. Only the re-shot positions live here (not saved): after a
    // reload each enemy in the cone counts as not yet shot.
    public static readonly Dictionary<string, Watch> Watches = new();
    public static readonly Dictionary<string, int> PendingShots = new();

    public static void Log(Exception ex, string where) => PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: " + where);
}

[HarmonyPatch(typeof(AbilityAoEPatternSettings), nameof(AbilityAoEPatternSettings.GetOrientedPattern))]
internal static class OverwatchCone_ResizeByCursor
{
    // One seam for both the targeting preview and AbilityEffectOverwatch.GetOverwatchArea, so the locked zone matches the preview.
    private static bool Prefix(AbilityAoEPatternSettings __instance, IAbilityDataProviderForPattern ability, CustomGridNodeBase casterNode, CustomGridNodeBase targetNode, bool coveredTargetsOnly, ref OrientedPatternData __result)
    {
        try
        {
            if (!OverwatchCone.IsOurs(ability?.Data) || casterNode == null || targetNode == null)
                return true;
            int distance = casterNode.CellDistanceTo(targetNode);
            var pattern = OverwatchCone.ConeFor(distance, ability.RangeCells);
            __result = AoEPatternHelper.GetOrientedPattern(ability, ability.Caster, pattern, __instance, casterNode, targetNode, __instance.CastOnSameLevel, true, coveredTargetsOnly, out _);
            if (ability.Caster is BaseUnitEntity previewUnit)
                AbilityOutline.ShowPreview(previewUnit, __result, AbilityOutline.OverwatchColor);
            return false;
        }
        catch (Exception ex)
        {
            OverwatchCone.Log(ex, "resizing the cone");
            return true;
        }
    }
}

[HarmonyPatch(typeof(UnitUseAbility), "OnStart")]
internal static class OverwatchCone_CaptureActionPoints
{
    // AP is read when the command starts, before UnitUseAbility spends the cost.
    private static void Prefix(UnitUseAbility __instance)
    {
        try
        {
            if (OverwatchCone.IsOurs(__instance.Ability) && __instance.Executor is BaseUnitEntity unit)
                OverwatchCone.PendingShots[unit.UniqueId] = unit.CombatState.ActionPointsYellow;
        }
        catch (Exception ex)
        {
            OverwatchCone.Log(ex, "reading action points");
        }
    }
}

[HarmonyPatch(typeof(AbilityEffectOverwatch), nameof(AbilityEffectOverwatch.Apply))]
internal static class OverwatchCone_StartBudget
{
    private static void Postfix(AbilityExecutionContext context)
    {
        try
        {
            if (!OverwatchCone.IsOurs(context?.Ability) || context.MaybeCaster is not BaseUnitEntity unit)
                return;
            if (!OverwatchCone.PendingShots.TryGetValue(unit.UniqueId, out int ap))
                ap = unit.CombatState.ActionPointsYellow;
            OverwatchCone.PendingShots.Remove(unit.UniqueId);
            OverwatchCone.Watches[unit.UniqueId] = new OverwatchCone.Watch();
            int shots = OverwatchCone.ShotsFor(unit, ap, context.Ability.CalculateActionPointCost());
            var buff = OverwatchCone.ShotsBuff(unit);
            if (buff == null)
                return;
            if (buff.Rank < shots)
                buff.AddRank(shots - buff.Rank);
            else if (buff.Rank > shots)
                buff.RemoveRank(buff.Rank - shots);
        }
        catch (Exception ex)
        {
            OverwatchCone.Log(ex, "starting the shot budget");
        }
    }
}

[HarmonyPatch(typeof(PartOverwatch), nameof(PartOverwatch.TryTriggerAttack))]
internal static class OverwatchCone_ChooseShots
{
    private static bool Prefix(PartOverwatch __instance, BaseUnitEntity target, out bool __state)
    {
        __state = false;
        try
        {
            if (!OverwatchCone.IsOurs(__instance.Ability))
                return true;
            var owner = (BaseUnitEntity)__instance.Owner;
            if (!OverwatchCone.Watches.TryGetValue(owner.UniqueId, out var watch))
                OverwatchCone.Watches[owner.UniqueId] = watch = new OverwatchCone.Watch();
            if (OverwatchCone.ShotsBuff(owner) is not { Rank: > 0 })
            {
                __instance.Stop();
                return false;
            }

            var here = (CustomGridNodeBase)target.CurrentUnwalkableNode;
            if (watch.LastShotAt.TryGetValue(target.UniqueId, out var lastNode))
            {
                // Standing still (acting in place) or creeping under the minimum never draws a repeat shot.
                if (here == null || lastNode == null || here.CellDistanceTo(lastNode) < OverwatchCone.MinReshotDistance)
                    return false;
                if (AnyUnshotEnemyInZone(__instance, owner, watch))
                    return false;
            }

            watch.LastShotAt[target.UniqueId] = here;
            // The reach penalty, captured now: the last shot stops the overwatch before its attack rolls.
            int penalty = OverwatchDebuff.PenaltyOf(owner, __instance);
            if (penalty > 0 && __instance.Ability?.Fact != null)
                OverwatchDebuff.Pending[(owner.UniqueId, target.UniqueId)] = (-penalty, __instance.Ability.Fact, Time.time,
                    __instance.AbilityOnTrigger is { IsAttackOfOpportunity: true } shotAbility ? shotAbility : null);
            // The attack-of-opportunity flagging and reaction-budget gate (RTO-T85, T94) run ahead of this prefix
            // in Abilities_AttackOfOpportunity, which also owns the trigger-ability clone swap.
            __state = true;
            return true;
        }
        catch (Exception ex)
        {
            OverwatchCone.Log(ex, "choosing an overwatch shot");
            return false;
        }
    }

    // True while the follow-up shot is being fired, so it doesn't chain a follow-up of its own.
    private static bool s_FollowingUp;

    private static void Postfix(PartOverwatch __instance, BaseUnitEntity target, bool __state)
    {
        try
        {
            if (!__state)
                return;
            // The last shot stops the overwatch, which also removes the buff; otherwise spend one rank.
            var owner = (BaseUnitEntity)__instance.Owner;
            var buff = OverwatchCone.ShotsBuff(owner);
            if (buff == null || buff.Rank <= 1)
            {
                __instance.Stop();
                return;
            }
            buff.RemoveRank(1);
            // A shot left: fire it at once at the next-closest enemy marked Overwatched (in this cone), not yet shot
            // at. With no such enemy it waits for the next trigger.
            if (s_FollowingUp || !OverwatchCone.Watches.TryGetValue(owner.UniqueId, out var watch))
                return;
            var next = NextClosest(__instance, owner, target, watch);
            if (next == null)
                return;
            s_FollowingUp = true;
            try
            {
                __instance.TryTriggerAttack(next);
            }
            finally
            {
                s_FollowingUp = false;
            }
        }
        catch (Exception ex)
        {
            OverwatchCone.Log(ex, "ending overwatch");
        }
    }

    private static BaseUnitEntity NextClosest(PartOverwatch part, BaseUnitEntity owner, BaseUnitEntity shot, OverwatchCone.Watch watch)
    {
        BaseUnitEntity best = null;
        float bestDistance = float.MaxValue;
        foreach (var unit in Game.Instance.State.AllBaseAwakeUnits)
        {
            if (unit == shot || unit.LifeState.IsDeadOrUnconscious || !owner.IsEnemy(unit) || watch.LastShotAt.ContainsKey(unit.UniqueId) || !part.Contains(unit))
                continue;
            float distance = (unit.Position - owner.Position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = unit;
            }
        }
        return best;
    }

    private static bool AnyUnshotEnemyInZone(PartOverwatch part, BaseUnitEntity owner, OverwatchCone.Watch watch)
    {
        foreach (var unit in Game.Instance.State.AllBaseAwakeUnits)
        {
            if (unit.LifeState.IsDeadOrUnconscious || !owner.IsEnemy(unit) || watch.LastShotAt.ContainsKey(unit.UniqueId))
                continue;
            if (part.Contains(unit))
                return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(PartOverwatch), nameof(PartOverwatch.Stop))]
internal static class OverwatchCone_ForgetOnStop
{
    private static void Prefix(PartOverwatch __instance)
    {
        try
        {
            if (OverwatchCone.IsOurs(__instance.Ability))
                OverwatchCone.Watches.Remove(__instance.Owner.UniqueId);
        }
        catch (Exception ex)
        {
            OverwatchCone.Log(ex, "clearing overwatch state");
        }
    }
}
