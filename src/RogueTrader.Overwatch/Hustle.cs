using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Controllers.Combat;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.CasterCheckers;
using Kingmaker.Utility;

namespace RogueTraderOverwatch;

// Hustle: once per battle, on the caster's own first turn only, move up to the grid-cell distance to whichever
// other party member is currently farthest away, free of AP and MP (RTO-T109). Lets a character who starts a
// fight far from the group catch up without losing their turn to it. Kept out of Abilities.All/IsOurs on
// purpose: that set also scopes Abilities_ActionPointCostByWeaponSize, which would otherwise tax Hustle a
// Heavy-weapon AP surcharge it was never meant to have.
internal static class Hustle
{
    public const string AbilityGuid = "fb73730b0fdb4689939fed3edd2af32a";

    public static bool IsOurs(BlueprintAbility blueprint) => blueprint?.AssetGuid == AbilityGuid;

    public static bool IsOurs(AbilityData ability) => IsOurs(ability?.Blueprint);

    // Units that have had a turn-start before this battle (first turn still open) and those now past it
    // (ineligible). Keyed by UniqueId, cleared on (re)joining combat so a unit who leaves and returns - or a
    // reinforcement who joins mid-fight - gets its own first turn again.
    public static readonly HashSet<string> SeenFirstTurn = new();
    public static readonly HashSet<string> PastFirstTurn = new();

    public static bool IsEligible(MechanicEntity caster) =>
        caster is BaseUnitEntity unit && !PastFirstTurn.Contains(unit.UniqueId);

    // The farthest other current party member, in grid cells: the same CellDistanceTo/CurrentUnwalkableNode
    // pattern OverwatchDebuff.ReachOf uses for a cone's reach.
    public static int RangeFor(BaseUnitEntity caster)
    {
        if (caster?.CurrentUnwalkableNode is not CustomGridNodeBase from)
            return 0;
        int range = 0;
        foreach (var member in Game.Instance.Player.Party)
        {
            if (member == caster || member?.CurrentUnwalkableNode is not CustomGridNodeBase to)
                continue;
            range = Math.Max(range, from.CellDistanceTo(to));
        }
        return range;
    }

    public static void Reset(BaseUnitEntity unit)
    {
        SeenFirstTurn.Remove(unit.UniqueId);
        PastFirstTurn.Remove(unit.UniqueId);
    }
}

// Marks a unit past its first turn the moment a second turn-start happens for it in this battle: PrepareForNewTurn
// fires once per unit's own turn, not once per global round.
[HarmonyPatch(typeof(PartUnitCombatState), nameof(PartUnitCombatState.PrepareForNewTurn))]
internal static class Hustle_TrackTurns
{
    private static void Postfix(PartUnitCombatState __instance)
    {
        try
        {
            if (__instance.Owner is not BaseUnitEntity unit)
                return;
            if (!Hustle.SeenFirstTurn.Add(unit.UniqueId))
                Hustle.PastFirstTurn.Add(unit.UniqueId);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: tracking Hustle's first turn");
        }
    }
}

// A fresh battle (or rejoining one, e.g. a reinforcement, or leaving and coming back) gives a unit its own
// first turn again.
[HarmonyPatch(typeof(PartUnitCombatState), nameof(PartUnitCombatState.JoinCombat))]
internal static class Hustle_ResetOnJoinCombat
{
    private static void Prefix(PartUnitCombatState __instance)
    {
        try
        {
            if (__instance.Owner is BaseUnitEntity unit)
                Hustle.Reset(unit);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: resetting Hustle on join combat");
        }
    }
}

[HarmonyPatch(typeof(PartUnitCombatState), nameof(PartUnitCombatState.LeaveCombat))]
internal static class Hustle_ResetOnLeaveCombat
{
    private static void Postfix(PartUnitCombatState __instance)
    {
        try
        {
            if (__instance.Owner is BaseUnitEntity unit)
                Hustle.Reset(unit);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: resetting Hustle on leave combat");
        }
    }
}

// The blueprint's AbilityCasterHasNoFacts carries an empty fact list (so the stock check would always pass);
// scoped to Hustle's own ability only, this replaces it with the real first-turn-only gate, the same pattern
// Abilities_FixEmptyLimbWeaponCheck and Abilities_RequireAmmoToCast use for Reflex Shot/Overwatch/Suppression.
[HarmonyPatch(typeof(AbilityCasterHasNoFacts), nameof(AbilityCasterHasNoFacts.IsCasterRestrictionPassed))]
internal static class Hustle_FirstTurnOnly
{
    private static bool Prefix(AbilityCasterHasNoFacts __instance, MechanicEntity caster, ref bool __result)
    {
        if (!Hustle.IsOurs(__instance.OwnerBlueprint as BlueprintAbility))
            return true;
        try
        {
            __result = Hustle.IsEligible(caster);
            return false;
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: Hustle first-turn check");
            return true;
        }
    }
}

// Hustle's range is the distance to the farthest other party member, worked out fresh every time it's checked
// (the ability's own Range: Unlimited is just a placeholder), the same seam ReflexShot_RangeFromWeapon uses.
[HarmonyPatch(typeof(RuleCalculateAbilityRange), nameof(RuleCalculateAbilityRange.OnTrigger))]
internal static class Hustle_Range
{
    private static readonly MethodInfo SetResult = AccessTools.PropertySetter(typeof(RuleCalculateAbilityRange), nameof(RuleCalculateAbilityRange.Result));

    private static void Postfix(RuleCalculateAbilityRange __instance)
    {
        try
        {
            if (!Hustle.IsOurs(__instance.Ability) || __instance.Ability.Caster is not BaseUnitEntity caster)
                return;
            int range = Hustle.RangeFor(caster);
            if (range != __instance.Result)
                SetResult.Invoke(__instance, new object[] { range });
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: setting Hustle's range");
        }
    }
}
