using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components.Patterns;
using Kingmaker.Utility;
using UnityEngine;

namespace RogueTraderOverwatch;

// While the player aims Overwatch, the game's own hit-chance overtip above each enemy in the area (the round
// gauge and damage range a normal attack shows) carries the shot that enemy would take: the ranged weapon's
// attack, worked out by the game itself (range falloff, cover, dodge, talents, burst, damage), with Overwatch's
// own reach penalty added the same way the real roll applies it. Left alone, an area ability counts as "always
// hits" and the overtip shows just its icon. GatherAffectedTargetsData builds the per-target data the overtips
// read, each time the aimed area changes. Reflex Shot has no aiming step to preview (RTO-T111: it fires at a
// random target the instant it's cast).
[HarmonyPatch(typeof(AbilityDataHelper), nameof(AbilityDataHelper.GatherAffectedTargetsData))]
internal static class HitChancePreview
{
    // Set only while one target's preview is being worked out: whose hit-chance rules get Overwatch's reach
    // penalty, and under which fact's name.
    internal static (MechanicEntity Shooter, int Modifier, EntityFact Source)? Pending;

    private static void Postfix(AbilityData ability, OrientedPatternData pattern, Vector3 casterPosition, TargetWrapper clickedTarget, in List<AbilityTargetUIData> listToFill)
    {
        try
        {
            if (!OverwatchCone.IsOurs(ability) || ability.Caster is not BaseUnitEntity aimer || listToFill == null)
                return;
            var attack = Abilities.RangedAttack(aimer);
            if (attack == null)
                return;
            // Same measure as the real roll (OverwatchDebuff.PenaltyOf): the farthest cell of the aimed cone.
            var cells = new List<CustomGridNodeBase>();
            foreach (var node in pattern.Nodes)
                cells.Add(node);
            int penalty = OverwatchDebuff.PenaltyFor(OverwatchDebuff.ReachOf(aimer, cells), attack.RangeCells);
            for (int i = 0; i < listToFill.Count; i++)
            {
                if (listToFill[i].Target is not BaseUnitEntity target || !aimer.IsEnemy(target))
                    continue;
                listToFill[i] = ShotAt(ability, attack, aimer, target, casterPosition, -penalty);
            }
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: hit-chance preview");
        }
    }

    // The weapon attack's own preview data, under our ability (the overtip only shows data for the ability being aimed).
    private static AbilityTargetUIData ShotAt(AbilityData ability, AbilityData attack, BaseUnitEntity aimer, BaseUnitEntity target, Vector3 casterPosition, int modifier)
    {
        Pending = (aimer, modifier, ability.Fact);
        try
        {
            var overpenetration = new OverpenetrationUIData { CountOverpenetration = false, OverpenetrationDamagePercent = 100, OverpenetrationHitChance = 100f };
            return new AbilityTargetUIData(attack, target, casterPosition, ref overpenetration).CopyWithOverride(ability);
        }
        finally
        {
            Pending = null;
        }
    }
}

// Overwatch's own additive reach penalty, for the preview only.
[HarmonyPatch(typeof(RuleCalculateHitChances), nameof(RuleCalculateHitChances.OnTrigger))]
internal static class HitChancePreview_AddModifier
{
    private static void Prefix(RuleCalculateHitChances __instance)
    {
        try
        {
            if (HitChancePreview.Pending is { } pending && pending.Modifier != 0 && pending.Source != null && __instance.Initiator == pending.Shooter)
                __instance.HitChanceValueModifiers.Add(pending.Modifier, pending.Source);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: hit-chance preview modifier");
        }
    }
}
