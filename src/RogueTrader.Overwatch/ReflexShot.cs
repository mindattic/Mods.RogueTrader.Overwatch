using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Kingmaker.Enums;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.RuleSystem.Rules.Modifiers;
using HarmonyLib;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Abilities.Components.PatternAttack;
using Kingmaker.UnitLogic.Abilities.Components.Patterns;
using Kingmaker.PubSubSystem;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using RogueTrader.Shared;
using UnityEngine;

namespace RogueTraderOverwatch;

// Reflex Shot: a circle around the shooter, all directions, aimed toward a facing the player picks. Its shot keeps
// the shooter's own hit chance (Ballistic Skill, range, cover, talents, the works), scaled down by how far the
// muzzle actually was from the target when the shot broke: literally the cosine of the angle between the facing the
// player picked and the direction the shot had to go, the same fraction of "still pointed that way" a dot product
// of the two directions gives. All of it dead ahead, none of the geometry's own math left for a shot thrown from
// more than a quarter-turn off, floored at MinRetainedFraction instead of zero so it's still a sliver of a chance
// from pure luck (RTO-T96, replacing the old flat 33% base and linear +10/-30 ramp, which ignored the shooter's own
// skill and made Reflex Shot relatively weaker on better shots the higher that skill got).
internal static class ReflexShot
{
    public const float MinRetainedFraction = 0.10f;

    // A reflex shot is still a reaction, not a fully settled aimed shot: it deals DamagePercent of the weapon's
    // damage regardless of angle (damage modifiers still apply) - a rushed trigger pull places the hit less
    // precisely even when it lands, which has nothing to do with which way the muzzle was pointed.
    public const int DamagePercent = 33;

    public static bool IsOurs(AbilityData ability) => ability?.Blueprint?.AssetGuid == Abilities.ReflexShot;

    // The dot product of the muzzle's heading and the direction the shot needed: 1 dead ahead, 0 at a quarter-turn,
    // negative beyond that (clamped to the floor rather than going negative or hitting zero outright).
    public static float RetainedFraction(float degrees) =>
        Mathf.Max(MinRetainedFraction, Mathf.Cos(Mathf.Clamp(degrees, 0f, 180f) * Mathf.Deg2Rad));

    // The fraction of this shooter's own hit chance a Reflex Shot facing facingPoint would keep, firing at target.
    public static float RetainedFractionFor(BaseUnitEntity shooter, Vector3 facingPoint, Vector3 targetPosition) =>
        RetainedFraction(TurnAngle(shooter, facingPoint, targetPosition));

    // The real attack rolls a Reflex Shot made, and their hit-chance rules, each mapped to the fact and retained-aim
    // fraction that should scale this one shot: the hit-chance and damage patches below key off them. Weak, so
    // finished rules are collected as usual.
    public static readonly ConditionalWeakTable<object, AimedShot> Rolls = new();

    // A class, not a struct: ConditionalWeakTable's value type must be a reference type.
    public sealed class AimedShot
    {
        public readonly EntityFact Source;
        public readonly float Fraction;

        public AimedShot(EntityFact source, float fraction)
        {
            Source = source;
            Fraction = fraction;
        }
    }

    public static void Mark(object rule, EntityFact source, float fraction)
    {
        Rolls.Remove(rule);
        Rolls.Add(rule, new AimedShot(source, fraction));
    }

    // The Reflex Shot fact and retained-aim fraction a hit-chance or damage rule belongs to, or null: a marked real
    // roll, or the hit-chance preview working out a Reflex Shot for this shooter.
    public static AimedShot AimedShotOf(object rule, MechanicEntity initiator)
    {
        if (rule != null && Rolls.TryGetValue(rule, out var shot))
            return shot;
        return HitChancePreview.Pending is { Reflex: true } pending && pending.Shooter == initiator
            ? new AimedShot(pending.Source, pending.ReflexFraction)
            : null;
    }

    // The angle between the facing (shooter toward the clicked point) and the shooter-to-target direction, on the ground.
    public static float TurnAngle(BaseUnitEntity shooter, Vector3 facingPoint, Vector3 targetPosition)
    {
        var facing = Flat(facingPoint - shooter.Position);
        if (facing.sqrMagnitude < 0.01f)
            facing = Flat(shooter.OrientationDirection);
        var toTarget = Flat(targetPosition - shooter.Position);
        return toTarget.sqrMagnitude < 0.01f ? 0f : Vector3.Angle(facing, toTarget);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    internal readonly struct PendingShot
    {
        public readonly string TargetId;
        public readonly float Fraction;
        public readonly EntityFact Source;
        public readonly float At;
        // The ability the shot actually fires (Abilities_AttackOfOpportunity's per-shot clone): every round of that
        // one attack rolls with it, any later attack by the shooter with another instance.
        public readonly AbilityData Shot;

        public PendingShot(string targetId, float fraction, EntityFact source, float at, AbilityData shot)
        {
            TargetId = targetId;
            Fraction = fraction;
            Source = source;
            At = at;
            Shot = shot;
        }
    }

    // Overwatch mode stops the part as soon as the shot is queued, before the attack rolls, so the modifier is worked out
    // at the trigger and kept here (by shooter) until that attack rolls. Every roll at that target within PendingSeconds
    // uses it (a burst weapon rolls once per round); after that it lapses, so no later attack picks it up.
    public const float PendingSeconds = 5f;
    public static readonly Dictionary<string, PendingShot> Pending = new();

    public static readonly AccessTools.FieldRef<PartOverwatch, TargetWrapper> TargetField =
        AccessTools.FieldRefAccess<PartOverwatch, TargetWrapper>("m_Target");
}

// The circle stays centred on the shooter wherever the player clicks; the click only sets the facing. Same seam as the
// cone, so the preview and AbilityEffectOverwatch.GetOverwatchArea agree.
[HarmonyPatch(typeof(AbilityAoEPatternSettings), nameof(AbilityAoEPatternSettings.GetOrientedPattern))]
internal static class ReflexShot_CentreOnShooter
{
    private static bool Prefix(AbilityAoEPatternSettings __instance, IAbilityDataProviderForPattern ability, CustomGridNodeBase casterNode, CustomGridNodeBase targetNode, bool coveredTargetsOnly, ref OrientedPatternData __result)
    {
        try
        {
            if (!ReflexShot.IsOurs(ability?.Data) || casterNode == null)
                return true;
            var pattern = ((IAbilityAoEPatternProvider)__instance).Pattern;
            // The circle's size comes from the pattern, not the ability range: it's the weapon's full range.
            var attack = Abilities.RangedAttack(ability.Caster);
            if (attack != null && attack.RangeCells > 0 && attack.RangeCells != pattern.Radius)
                pattern = AoEPattern.CopyAndOverrideRadius(pattern, attack.RangeCells);
            __result = AoEPatternHelper.GetOrientedPattern(ability, ability.Caster, pattern, __instance, casterNode, casterNode, __instance.CastOnSameLevel, false, coveredTargetsOnly, out _);
            if (ability.Caster is BaseUnitEntity caster)
                AbilityOutline.ShowPreview(caster, __result, AbilityOutline.ReflexColor);
            return false;
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "centring Reflex Shot on the shooter");
            return true;
        }
    }
}

[HarmonyPatch(typeof(PartOverwatch), nameof(PartOverwatch.TryTriggerAttack))]
internal static class ReflexShot_RecordTurn
{
    private static void Prefix(PartOverwatch __instance, BaseUnitEntity target)
    {
        try
        {
            if (!ReflexShot.IsOurs(__instance.Ability) || __instance.Owner is not BaseUnitEntity shooter || target == null)
                return;
            var facing = ReflexShot.TargetField(__instance);
            if (facing == null)
                return;
            float fraction = ReflexShot.RetainedFractionFor(shooter, facing.Point, target.Position);
            // AbilityOnTrigger is already the per-shot clone here (Abilities_AttackOfOpportunity's prefix runs first);
            // never the shared weapon attack the shooter also fires its normal shots with.
            var trigger = __instance.AbilityOnTrigger;
            var shot = trigger is { IsAttackOfOpportunity: true } ? trigger : null;
            ReflexShot.Pending[shooter.UniqueId] = new ReflexShot.PendingShot(target.UniqueId, fraction, __instance.Ability.Fact, Time.time, shot);
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "working out the Reflex Shot turn");
        }
    }
}

// Marks the real roll and its hit-chance rule with the retained-aim fraction worked out at the trigger, under the
// Reflex Shot ability's name. Hooked on the real roll (RulePerformAttackRoll builds its own hit-chance rule and
// triggers it first thing), so UI hit-chance previews never see or use it up, and it holds even when the attack
// command has already ended by the time the roll happens.
[HarmonyPatch(typeof(RulePerformAttackRoll), nameof(RulePerformAttackRoll.OnTrigger))]
internal static class ReflexShot_AimModifier
{
    private static void Prefix(RulePerformAttackRoll __instance)
    {
        try
        {
            if (ReflexShot.Pending.Count == 0 || __instance.Initiator is not BaseUnitEntity shooter || __instance.Target is not BaseUnitEntity target)
                return;
            if (!ReflexShot.Pending.TryGetValue(shooter.UniqueId, out var shot) || shot.TargetId != target.UniqueId)
                return;
            if (Time.time - shot.At > ReflexShot.PendingSeconds || shot.Source == null)
            {
                ReflexShot.Pending.Remove(shooter.UniqueId);
                return;
            }
            // Every round of the Reflex Shot's own attack gets it (a burst rolls once per round, RTO-T82), but no other
            // attack does: a later unrelated attack at the same target fires a different AbilityData (RTO-T73). Without
            // a recorded shot ability, the entry is used up by this first roll instead.
            if (shot.Shot != null && __instance.Ability != shot.Shot)
                return;
            if (shot.Shot == null)
                ReflexShot.Pending.Remove(shooter.UniqueId);
            ReflexShot.Mark(__instance, shot.Source, shot.Fraction);
            ReflexShot.Mark(__instance.HitChanceRule, shot.Source, shot.Fraction);
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "adding the Reflex Shot aim modifier");
        }
    }
}

// A Reflex Shot keeps the shooter's own hit chance (Ballistic Skill, range falloff, cover, talents: the game's own
// ranged calculation, OnTriggerRanged, left untouched), scaled by the retained-aim fraction worked out at the
// trigger (RTO-T96). Its overkill crit bonus is redone too, since the scaled chance decides it the same way the old
// flat base used to.
[HarmonyPatch(typeof(RuleCalculateHitChances), "OnTriggerRanged")]
internal static class ReflexShot_BaseHitChance
{
    private static readonly MethodInfo SetRaw = AccessTools.PropertySetter(typeof(RuleCalculateHitChances), nameof(RuleCalculateHitChances.RawResult));
    private static readonly MethodInfo SetNoLimit = AccessTools.PropertySetter(typeof(RuleCalculateHitChances), nameof(RuleCalculateHitChances.ResultHitChanceNoUpperLimit));
    private static readonly MethodInfo SetResult = AccessTools.PropertySetter(typeof(RuleCalculateHitChances), nameof(RuleCalculateHitChances.ResultHitChance));

    private static void Postfix(RuleCalculateHitChances __instance)
    {
        try
        {
            if (ReflexShot.AimedShotOf(__instance, (MechanicEntity)__instance.Initiator) is not { } shot || __instance.IsScatter)
                return;
            // Destructibles get the border chance from the game; leave that as it is.
            if (!__instance.Ability.IsBurstAttack && __instance.Target is DestructibleEntity)
                return;
            SetRawResult(__instance, Mathf.RoundToInt(__instance.ResultHitChance * shot.Fraction));
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "setting Reflex Shot's base hit chance");
        }
    }

    // Replaces a ranged hit-chance rule's raw result the way OnTriggerRanged sets it: the capped chance, and the
    // overkill crit bonus (the part over the cap), which the old raw value decided.
    public static void SetRawResult(RuleCalculateHitChances rule, int raw)
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

// A Reflex Shot deals DamagePercent of the weapon's damage, under the Reflex Shot ability's name, alongside the game's
// own damage multipliers. Added to the rule's modifier holder before it runs, which the result copies.
[HarmonyPatch(typeof(RuleCalculateDamage), nameof(RuleCalculateDamage.OnTrigger))]
internal static class ReflexShot_Damage
{
    private static void Prefix(RuleCalculateDamage __instance)
    {
        try
        {
            var shot = ReflexShot.AimedShotOf(__instance.RollPerformAttackRule, __instance.ConcreteInitiator);
            if (shot is { } s)
                __instance.ValueModifiers.Add(ModifierType.PctMul_Extra, ReflexShot.DamagePercent, s.Source);
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "reducing Reflex Shot's damage");
        }
    }
}

// A new overwatch of any kind replaces whatever Reflex Shot was waiting to roll.
[HarmonyPatch(typeof(AbilityEffectOverwatch), nameof(AbilityEffectOverwatch.Apply))]
internal static class ReflexShot_ForgetOnNewOverwatch
{
    private static void Prefix(AbilityExecutionContext context)
    {
        try
        {
            if (context?.MaybeCaster is BaseUnitEntity caster)
                ReflexShot.Pending.Remove(caster.UniqueId);
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "clearing a Reflex Shot");
        }
    }
}

// The facing point can be picked anywhere within the weapon's own range, matching the circle's radius.
[HarmonyPatch(typeof(RuleCalculateAbilityRange), nameof(RuleCalculateAbilityRange.OnTrigger))]
internal static class ReflexShot_RangeFromWeapon
{
    private static readonly MethodInfo SetResult = AccessTools.PropertySetter(typeof(RuleCalculateAbilityRange), nameof(RuleCalculateAbilityRange.Result));

    private static void Postfix(RuleCalculateAbilityRange __instance)
    {
        try
        {
            var ability = __instance.Ability;
            if (!ReflexShot.IsOurs(ability))
                return;
            var attack = Abilities.RangedAttack(ability.Caster);
            if (attack != null && attack.RangeCells > 0 && attack.RangeCells != __instance.Result)
                SetResult.Invoke(__instance, new object[] { attack.RangeCells });
        }
        catch (Exception ex)
        {
            ModLog.Exception(ex, "setting Reflex Shot's range from the weapon");
        }
    }
}
