using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.RuleSystem.Rules.Modifiers;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.Utility;
using Kingmaker.Utility.DotNetExtensions;
using Kingmaker.Utility.Random;
using RogueTrader.Shared;
using UnityEngine;

namespace RogueTraderOverwatch;

// Reflex Shot (RTO-T111): ends the turn, then at once fires one real shot at a randomly chosen enemy within
// weapon range and line of sight - no facing, no waiting as a reaction. "No matter the target": the chaos comes
// from who gets shot, not from a worse chance to hit them, so the shooter's own hit chance applies in full
// (Ballistic Skill, range falloff, cover, talents - the game's ranged calculation runs untouched). It still
// deals 33% of the weapon's damage (RTO-T82), the one piece of the old facing-based design this keeps.
// Replaces the old XCOM-2-style armed circle (RTO-T96 and earlier): no more PartOverwatch, no more retained-aim
// scaling by turn angle.
internal static class ReflexShot
{
    public const int DamagePercent = 33;

    public static bool IsOurs(AbilityData ability) => ability?.Blueprint?.AssetGuid == Abilities.ReflexShot;

    // The real attack a Reflex Shot fired, mapped to the Reflex Shot ability's own fact: ReflexShot_Damage keys
    // off this (there's no facing or retained-aim fraction left to carry it).
    internal static readonly ConditionalWeakTable<AbilityData, EntityFact> Shots = new();

    // Every other current enemy within the weapon's range and line of sight. The seeded random
    // (PFStatefulRandom.Mechanics, the same source the engine's own ContextRandomTargetWithinCellsFromCaster-
    // PositionEvaluator uses), not UnityEngine.Random: this pick decides real damage, so it must stay in sync
    // between multiplayer peers, unlike the mod's purely cosmetic random picks (RTO-T105).
    public static BaseUnitEntity RandomTargetFor(BaseUnitEntity caster, int range) =>
        Game.Instance.State.AllBaseAwakeUnits
            .Where(u => u != caster && caster.IsEnemy(u) && !u.LifeState.IsDeadOrUnconscious
                        && caster.InRangeInCells(u, range) && caster.HasLOS(u))
            .ToList()
            .Random(PFStatefulRandom.Mechanics);

    // Fires the shot: a fresh clone of the weapon's own attack (not the shared instance normal shots use),
    // through the same out-of-turn attack path Suppression's real shot uses (UnitOverwatchAttackParams), so hit
    // chance, damage and the visuals are all the normal ones for that weapon.
    public static void Fire(BaseUnitEntity caster, AbilityData reflexShot)
    {
        var attack = Abilities.RangedAttack(caster);
        if (attack == null)
            return;
        var target = RandomTargetFor(caster, attack.RangeCells);
        if (target == null || !attack.CanTarget(target, out _))
            return;
        var shot = attack.Clone(isPreview: false);
        if (reflexShot?.Fact != null)
            Shots.Add(shot, reflexShot.Fact);
        caster.Commands.Run(new UnitOverwatchAttackParams(shot, new TargetWrapper(target)));
    }

    private static readonly MethodInfo SetRaw = AccessTools.PropertySetter(typeof(RuleCalculateHitChances), nameof(RuleCalculateHitChances.RawResult));
    private static readonly MethodInfo SetNoLimit = AccessTools.PropertySetter(typeof(RuleCalculateHitChances), nameof(RuleCalculateHitChances.ResultHitChanceNoUpperLimit));
    private static readonly MethodInfo SetResult = AccessTools.PropertySetter(typeof(RuleCalculateHitChances), nameof(RuleCalculateHitChances.ResultHitChance));

    // Replaces a ranged hit-chance rule's raw result the way the game's own OnTriggerRanged sets it: the capped
    // chance, and the overkill crit bonus (the part over the cap), which the raw value decides. Reflex Shot no
    // longer rescales hit chance itself (RTO-T111), but Suppression's own cut (Suppression_ReduceHitChance)
    // still needs this, so it stayed here rather than being duplicated.
    public static void SetRawHitChance(RuleCalculateHitChances rule, int raw)
    {
        int border = rule.HitChanceBorderRule.ResultOverkillBorder;
        SetRaw.Invoke(rule, new object[] { raw });
        SetNoLimit.Invoke(rule, new object[] { Math.Max(0, raw) });
        SetResult.Invoke(rule, new object[] { Mathf.Clamp(raw, 0, border) });
        var fury = rule.RighteousFuryChanceRule.ChanceModifiers;
        fury.RemoveAll(m => m.Descriptor == ModifierDescriptor.HitChanceOverkill);
        int overkill = Mathf.RoundToInt(Mathf.Max(0, raw - border) * rule.CritChanceOverkillPercentMultipliers.Value);
        fury.Add(overkill, rule, ModifierDescriptor.HitChanceOverkill);
    }
}

// Fires Reflex Shot's own shot right when it's cast: the blueprint's AbilityEffectRunAction carries no actions
// of its own on purpose, so this is the whole effect.
[HarmonyPatch(typeof(AbilityEffectRunAction), nameof(AbilityEffectRunAction.Apply))]
internal static class ReflexShot_FireOnCast
{
    private static void Postfix(AbilityExecutionContext context)
    {
        try
        {
            if (!ReflexShot.IsOurs(context?.Ability) || context.MaybeCaster is not BaseUnitEntity caster)
                return;
            ReflexShot.Fire(caster, context.Ability);
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "firing Reflex Shot");
        }
    }
}

// A Reflex Shot deals DamagePercent of the weapon's damage, under the Reflex Shot ability's name, alongside the
// game's other damage multipliers.
[HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
internal static class ReflexShot_Damage
{
    private static void Prefix(RuleCalculateDamage __instance)
    {
        try
        {
            var ability = __instance.RollPerformAttackRule?.Ability;
            if (ability != null && ReflexShot.Shots.TryGetValue(ability, out var source))
                __instance.ValueModifiers.Add(ModifierType.PctMul_Extra, ReflexShot.DamagePercent, source);
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "reducing Reflex Shot's damage");
        }
    }
}
