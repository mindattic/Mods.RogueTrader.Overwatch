using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AI;
using Kingmaker.AI.BehaviourTrees;
using Kingmaker.AI.BehaviourTrees.Nodes;
using Kingmaker.Blueprints;
using Kingmaker.Controllers.TurnBased;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View.Covers;
using Pathfinding;
using UnityEngine;

namespace RogueTraderOverwatch;

// How enemies use Reflex Shot, Overwatch and Suppression. Left alone, the AI sorts these end-turn abilities last but
// still casts them on leftover AP (Suppression on any shootable enemy, Overwatch only at enemies already in the cone).
// Instead the normal ability search never sees them, and one rule decides, only once the unit is done moving:
//   - it hasn't attacked this turn (all its AP left) and holds a ranged weapon;
//   - it can shoot someone but every such enemy is in full cover: Suppression on the nearest one not already suppressed;
//   - it can shoot someone in the open: nothing here, the normal attack runs;
//   - it can shoot nobody, but an enemy is within weapon range + ApproachMargin cells: Overwatch aimed at the nearest
//     one with 2+ AP (1 shot at 2 AP, 2 at 3+ with a 1-AP attack, RTO-T91), Reflex Shot with 1 AP (no aim needed
//     since RTO-T111, it fires at a random enemy in range on cast).
internal static class EnemyAi
{
    public const int ApproachMargin = 8;

    private static readonly string[] OurAbilities =
    {
        "27f7f769d0b94ab18fee0cde8a577a4d", // Reflex Shot
        OverwatchCone.AbilityGuid,
        "5db3949e7bf544bcb10cc3a2d51eb9ea", // Suppression
    };

    public static bool IsOurs(AbilityData ability) => ability?.Blueprint != null && OurAbilities.Contains(ability.Blueprint.AssetGuid);

    public static bool IsAiUnit(BaseUnitEntity unit) => unit != null && !unit.IsPlayerFaction;

    public static (AbilityData Ability, TargetWrapper Target) Decide(BaseUnitEntity unit, CastTimepointType timepoint)
    {
        // Pets don't plan ahead: they never overwatch, even with a ranged weapon.
        if (!IsAiUnit(unit) || unit.IsPet || !TurnController.IsInTurnBasedCombat() || unit.LifeState.IsDead)
            return default;
        var combat = unit.CombatState;
        if (combat.ActionPointsYellow < 1 || combat.ActionPointsYellow < combat.WarhammerInitialAPYellow.ModifiedValue)
            return default;
        if (timepoint != CastTimepointType.AfterMove && combat.ActionPointsBlue >= 1)
            return default;
        // The ranged weapon's attack, not GetFirstWeapon's: a melee main hand would hide an off-hand pistol.
        var shot = Abilities.RangedAttack(unit);
        if (shot == null || shot.Weapon == null)
            return default;
        var here = (CustomGridNodeBase)unit.CurrentUnwalkableNode;
        if (here == null)
            return default;

        var enemies = Game.Instance.State.AllBaseAwakeUnits
            .Where(e => e != unit && !e.LifeState.IsDead && unit.IsEnemy(e) && e.IsInCombat)
            .ToList();
        var shootable = new List<(BaseUnitEntity Unit, int Distance, LosCalculations.CoverType Cover)>();
        foreach (var enemy in enemies)
        {
            var node = (CustomGridNodeBase)enemy.CurrentUnwalkableNode;
            if (node != null && shot.CanTargetFromNode(here, node, new TargetWrapper(enemy), out int distance, out var cover))
                shootable.Add((enemy, distance, cover));
        }

        if (shootable.Count > 0)
        {
            if (shootable.Any(s => s.Cover != LosCalculations.CoverType.Full))
                return default;
            var suppression = Usable(unit, "5db3949e7bf544bcb10cc3a2d51eb9ea");
            var suppressed = ResourcesLibrary.TryGetBlueprint<BlueprintBuff>(Suppression.BuffGuid);
            var target = shootable.Where(s => suppressed == null || !s.Unit.Buffs.Contains(suppressed))
                .OrderBy(s => s.Distance).Select(s => s.Unit).FirstOrDefault();
            if (suppression == null || target == null)
                return default;
            var wrapper = new TargetWrapper(target);
            return suppression.CanTarget(wrapper) ? (suppression, wrapper) : default;
        }

        var overwatch = Usable(unit, OverwatchCone.AbilityGuid);
        int range = overwatch?.RangeCells ?? shot.RangeCells;
        var nearest = enemies
            .Select(e => (Unit: e, Node: (CustomGridNodeBase)e.CurrentUnwalkableNode))
            .Where(e => e.Node != null)
            .Select(e => (e.Unit, Distance: here.CellDistanceTo(e.Node)))
            .Where(e => e.Distance <= range + ApproachMargin)
            .OrderBy(e => e.Distance)
            .FirstOrDefault();
        if (nearest.Unit == null)
            return default;

        if (overwatch != null && combat.ActionPointsYellow >= 2)
        {
            // Aim at the nearest enemy, no farther than weapon range: the cone resize patch sizes it from this distance.
            var from = unit.Position;
            var toward = nearest.Unit.Position - from;
            toward.y = 0;
            float cells = Mathf.Clamp(nearest.Distance, OverwatchCone.MinRadius, Math.Max(OverwatchCone.MinRadius, range));
            var point = from + toward.normalized * (cells * GraphParamsMechanicsCache.GridCellSize);
            var wrapper = new TargetWrapper(point);
            if (toward.sqrMagnitude > 0.01f && overwatch.CanTarget(wrapper))
                return (overwatch, wrapper);
        }

        // Reflex Shot has no facing to pick anymore (RTO-T111: it fires at a random enemy in range on cast), so
        // it's a self-cast, same as the player's own.
        var reflex = Usable(unit, "27f7f769d0b94ab18fee0cde8a577a4d");
        if (reflex != null)
        {
            var self = new TargetWrapper(unit);
            if (reflex.CanTarget(self))
                return (reflex, self);
        }
        return default;
    }

    private static AbilityData Usable(BaseUnitEntity unit, string guid)
    {
        var data = unit.Abilities.RawFacts.FirstOrDefault(a => a.Blueprint.AssetGuid == guid)?.Data;
        return data != null && data.IsAvailable ? data : null;
    }

    public static void Log(Exception ex, string where) => PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: " + where);
}

// The AI's path planning treats every active overwatch area as threatened ground and routes around it, knowing about
// it whether or not it can see the shooter: an Overwatch cone would just be a no-go zone that never fires. Take this
// mod's overwatchers out of the threat map, so units move as they would anyway and get caught by the shots (RTO-T87,
// T88). Reflex Shot no longer arms a zone at all (RTO-T111), so it was never a concern here. The game's own
// overwatches are left as they are.
[HarmonyPatch(typeof(AiBrainHelper), nameof(AiBrainHelper.GatherThreatsData))]
internal static class EnemyAi_OverwatchThreats
{
    private static void Postfix(BaseUnitEntity unit, Dictionary<GraphNode, AiBrainHelper.ThreatsInfo> __result)
    {
        try
        {
            if (unit == null || __result == null)
                return;
            MarkPinned(unit, __result);
            var ignored = new HashSet<BaseUnitEntity>();
            foreach (var info in __result.Values)
                foreach (var overwatcher in info.overwatchUnits)
                    if (!ignored.Contains(overwatcher) && IsOurs(overwatcher))
                        ignored.Add(overwatcher);
            if (ignored.Count == 0)
                return;
            foreach (var info in __result.Values)
                info.overwatchUnits.RemoveWhere(ignored.Contains);
        }
        catch (Exception ex)
        {
            EnemyAi.Log(ex, "filtering overwatch areas from the AI's threat map");
        }
    }

    // A Suppressed unit knows it's pinned (RTO-T90): leaving its square draws a shot from each suppressor, the same as
    // walking out of a melee enemy's reach. The path cost already charges for that (a unit in a node's aooUnits that
    // isn't in the next node's), so its own squares list its suppressors there: it weighs moving like provoking an
    // attack of opportunity, and mostly stays put and shoots back from where it stands.
    private static void MarkPinned(BaseUnitEntity unit, Dictionary<GraphNode, AiBrainHelper.ThreatsInfo> threats)
    {
        var suppressors = Suppression.Suppressors(unit).ToList();
        if (suppressors.Count == 0)
            return;
        foreach (var node in unit.GetOccupiedNodes())
        {
            if (!threats.TryGetValue(node, out var info))
                threats[node] = info = new AiBrainHelper.ThreatsInfo();
            foreach (var suppressor in suppressors)
                info.aooUnits.Add(suppressor);
        }
    }

    private static bool IsOurs(BaseUnitEntity overwatcher) =>
        overwatcher.GetOptional<PartOverwatch>() is { IsStopped: false } part && Abilities.IsOurs(part.Ability);
}

[HarmonyPatch(typeof(DecisionContext), nameof(DecisionContext.IsUsableAbility))]
internal static class EnemyAi_HideFromNormalSearch
{
    private static void Postfix(DecisionContext __instance, Ability ability, ref bool __result)
    {
        try
        {
            if (__result && EnemyAi.IsOurs(ability?.Data) && EnemyAi.IsAiUnit(__instance.Unit))
                __result = false;
        }
        catch (Exception ex)
        {
            EnemyAi.Log(ex, "hiding overwatch from the AI's ability search");
        }
    }
}

[HarmonyPatch(typeof(TaskNodeSelectAbilityTarget), "TickInternal")]
internal static class EnemyAi_ChooseOverwatch
{
    private static bool Prefix(TaskNodeSelectAbilityTarget __instance, Blackboard blackboard, ref Status __result)
    {
        try
        {
            var context = blackboard?.DecisionContext;
            var (ability, target) = EnemyAi.Decide(context?.Unit, __instance.CastTimepoint);
            if (ability == null)
                return true;
            context.Ability = ability;
            context.AbilityTarget = target;
            context.TryTargetAllInsteadOfHatedOnly = false;
            __result = Status.Success;
            return false;
        }
        catch (Exception ex)
        {
            EnemyAi.Log(ex, "choosing an enemy overwatch");
            return true;
        }
    }
}
