using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.Blueprints;
using Kingmaker.Code.Enums.Helper;
using Kingmaker.Controllers.Combat;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Abilities.Components.CasterCheckers;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;

namespace RogueTraderOverwatch;

// The three abilities, shared by heroes (their own facts, Weapon == null) and enemies (weapon slots).
internal static class Abilities
{
    public const string ReflexShot = "27f7f769d0b94ab18fee0cde8a577a4d";
    public const string Overwatch = "b5855687981e4ba9a7dfc8c4af03229f";
    public const string Suppression = "5db3949e7bf544bcb10cc3a2d51eb9ea";

    public static readonly string[] All = { ReflexShot, Overwatch, Suppression };

    // Every ability this mod puts on the party's own ability bar (ActionBar.cs), including ones kept out of
    // All/IsOurs because they don't share All's weapon/ammo/AP-cost rules (Hustle: free of AP, never taxed a
    // Heavy-weapon surcharge).
    public static readonly string[] BarPlaced = { ReflexShot, Overwatch, Suppression, Hustle.AbilityGuid };

    public static bool IsOurs(BlueprintAbility blueprint) => blueprint != null && Array.IndexOf(All, blueprint.AssetGuid) >= 0;

    public static bool IsOurs(AbilityData ability) => IsOurs(ability?.Blueprint);

    // The first ranged weapon in the active set: main hand, off hand, then extra limbs. GetFirstWeapon would
    // return a melee main hand even when the off hand holds a pistol.
    public static ItemEntityWeapon RangedWeapon(MechanicEntity unit)
    {
        if (unit is not BaseUnitEntity owner || owner.Body == null)
            return null;
        var weapons = new List<ItemEntityWeapon> { owner.Body.PrimaryHand.MaybeWeapon, owner.Body.SecondaryHand.MaybeWeapon };
        weapons.AddRange(owner.Body.AdditionalLimbs.Select(l => l.MaybeWeapon));
        return weapons.FirstOrDefault(w => w != null && w.Blueprint.IsRanged);
    }

    // The ranged weapon's own attack: what every overwatch shot fires.
    public static AbilityData RangedAttack(MechanicEntity unit) =>
        RangedWeapon(unit)?.Abilities.FirstOrDefault()?.Data;

    // Whether the weapon Reflex Shot or Overwatch would actually fire still has a round chambered. Weapons that
    // don't track ammo (WarhammerMaxAmmo < 0) and a caster with no ranged weapon at all pass: the weapon-type
    // restriction above already greys the ability out for that case.
    public static bool HasAmmo(MechanicEntity caster)
    {
        var weapon = RangedWeapon(caster);
        return weapon == null || weapon.Blueprint.WarhammerMaxAmmo < 0 || weapon.CurrentAmmo > 0;
    }

    // Whether the shooter can spend a reaction on a shot right now: the same per-round attack-of-opportunity
    // budget a melee threat already draws from when something walks out of its reach
    // (PartUnitCombatState.CanAttackOfOpportunity; AttackOfOpportunityCount, 1 by default, raised by talents
    // and traits the same way Combat Reflexes raises it on the tabletop). Player characters and Elite+ enemies
    // are never capped by it, by the base game's own rule, so this only reins in regular enemy mooks (RTO-T94).
    public static bool CanMakeAttackOfOpportunity(BaseUnitEntity shooter) =>
        shooter?.GetCombatStateOptional()?.CanAttackOfOpportunity(canUseInRange: true, isSpell: false) ?? true;

    // Spends one reaction from that same budget, the same bookkeeping AttackOfOpportunityController does when a
    // melee threat's free swing actually fires.
    public static void SpentAttackOfOpportunity(BaseUnitEntity shooter)
    {
        var combatState = shooter?.GetCombatStateOptional();
        if (combatState != null)
            combatState.AttacksOfOpportunityMadeThisTurnCount++;
    }
}

// A triggered Reflex Shot or Overwatch shot is an attack of opportunity: taking a shot in the spur of the
// moment because the chance presented itself, the same as a melee threat's free swing when something leaves
// its reach (Suppression's real shot already is one, flagged directly in Suppression.cs since it doesn't run
// through PartOverwatch). Flagging it (IsAttackOfOpportunity) lets talents and traits that key off that flag
// (CheckIfAttackOfOpportunityGetter) apply, the same seam Overwatch already used; and it spends from the same
// per-round reaction budget those melee threats draw from, so a unit that already reacted its allotment away
// this round can't also loose a Reflex Shot or an Overwatch volley on top of it (RTO-T94). Runs ahead of
// ReflexShot_RecordTurn and OverwatchCone_ChooseShots (both default priority) so a spent-out shooter never
// reaches their bookkeeping at all.
[HarmonyPatch(typeof(PartOverwatch), nameof(PartOverwatch.TryTriggerAttack))]
internal static class Abilities_AttackOfOpportunity
{
    private static readonly AccessTools.FieldRef<PartOverwatch, AbilityData> TriggerField =
        AccessTools.FieldRefAccess<PartOverwatch, AbilityData>("m_AbilityOnTrigger");

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(PartOverwatch __instance, out AbilityData __state)
    {
        __state = null;
        try
        {
            if (!Abilities.IsOurs(__instance.Ability) || __instance.Owner is not BaseUnitEntity shooter)
                return true;
            if (!Abilities.CanMakeAttackOfOpportunity(shooter))
            {
                __instance.Stop();
                return false;
            }
            var trigger = __instance.AbilityOnTrigger;
            if (trigger != null && !trigger.IsAttackOfOpportunity)
            {
                var aoo = trigger.Clone(isPreview: false);
                aoo.IsAttackOfOpportunity = true;
                __state = trigger;
                TriggerField(__instance) = aoo;
            }
            return true;
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: attack-of-opportunity gate");
            return true;
        }
    }

    // __runOriginal reflects every prefix on this method, OverwatchCone_ChooseShots's own veto (out of shots,
    // too soon to re-shoot) included, so a shot that never actually queued never spends a reaction either.
    private static void Postfix(PartOverwatch __instance, AbilityData __state, bool __runOriginal)
    {
        try
        {
            if (__state != null)
                TriggerField(__instance) = __state;
            if (__runOriginal && Abilities.IsOurs(__instance.Ability) && __instance.Owner is BaseUnitEntity shooter)
                Abilities.SpentAttackOfOpportunity(shooter);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: attack-of-opportunity gate (restoring trigger)");
        }
    }
}

// A hero's copy has no weapon, so Range: Weapon would resolve to nothing; use the active ranged weapon's range.
[HarmonyPatch(typeof(RuleCalculateAbilityRange), nameof(RuleCalculateAbilityRange.OnTrigger))]
internal static class Abilities_RangeFromActiveWeapon
{
    private static readonly MethodInfo SetResult = AccessTools.PropertySetter(typeof(RuleCalculateAbilityRange), nameof(RuleCalculateAbilityRange.Result));

    private static void Postfix(RuleCalculateAbilityRange __instance)
    {
        try
        {
            var ability = __instance.Ability;
            if (!Abilities.IsOurs(ability) || ability.Weapon != null || ability.Blueprint.Range != AbilityRange.Weapon)
                return;
            var attack = Abilities.RangedAttack(ability.Caster);
            if (attack != null)
                SetResult.Invoke(__instance, new object[] { attack.RangeCells });
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: ability range");
        }
    }
}

// Weapon size costs more to commit to: a Heavy ranged weapon (WeaponHeaviness.Heavy, the same flag the base
// game's own AP-cost components key off for Heavy-only surcharges) costs 1 extra AP to cast Overwatch or Suppression
// (Reflex Shot stays 1 AP, RTO-T82),
// on top of the normal 1 AP, so a heavy weapon's wielder can't set up a cone or pin a target as cheaply as a
// pistol or basic weapon can. Same lookup as the range fix (Abilities.RangedWeapon), so heroes and enemies share
// the rule: an enemy's weapon-slot ability already carries its own weapon, a hero's reads the active set's.
[HarmonyPatch(typeof(RuleCalculateAbilityActionPointCost), nameof(RuleCalculateAbilityActionPointCost.OnTrigger))]
internal static class Abilities_ActionPointCostByWeaponSize
{
    private const int HeavyWeaponCostBonus = 1;

    private static void Prefix(RuleCalculateAbilityActionPointCost __instance)
    {
        try
        {
            var ability = __instance.AbilityData;
            // Reflex Shot is a quick reaction: 1 AP whatever the weapon.
            if (!Abilities.IsOurs(ability) || ability.Blueprint.AssetGuid == Abilities.ReflexShot)
                return;
            var weapon = ability.Weapon ?? Abilities.RangedWeapon(ability.Caster);
            if (weapon?.Blueprint.Heaviness == WeaponHeaviness.Heavy)
                __instance.CostBonus += HeavyWeaponCostBonus;
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: ability AP cost");
        }
    }
}

// The pattern delivery applies the ability's effects once per unit inside the area: an empty zone would never
// start overwatch, and several enemies would restart it once each. Deliver once, to the clicked target.
[HarmonyPatch(typeof(WarhammerAbilityAttackDelivery), nameof(WarhammerAbilityAttackDelivery.Deliver))]
internal static class Abilities_DeliverOnce
{
    private static bool Prefix(AbilityExecutionContext context, TargetWrapper target, ref IEnumerator<AbilityDeliveryTarget> __result)
    {
        try
        {
            if (!Abilities.IsOurs(context?.Ability))
                return true;
            __result = Once(target);
            return false;
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: delivering overwatch");
            return true;
        }
    }

    private static IEnumerator<AbilityDeliveryTarget> Once(TargetWrapper target)
    {
        yield return new AbilityDeliveryTarget(target);
    }
}

// The engine's own weapon-check crashes (and so greys the button out) for any caster with an empty additional
// limb: its loop reads additionalLimb.Weapon even when additionalLimb.HasWeapon is false, throwing a
// NullReferenceException instead of returning whatever Primary/SecondaryHand already decided (documented as an
// untested quirk in DESIGN.md; confirmed in game on Pasqal, who has mechadendrites). Scoped to our three
// abilities only: everything else in the game keeps using the original, unpatched check.
[HarmonyPatch(typeof(AbilityCasterHasWeaponWithRangeType), nameof(AbilityCasterHasWeaponWithRangeType.IsCasterRestrictionPassed))]
internal static class Abilities_FixEmptyLimbWeaponCheck
{
    private static bool HasBurstAbility(ItemEntityWeapon weapon) =>
        weapon != null && weapon.Abilities.Any(p =>
            p.Blueprint.GetComponent<WarhammerAbilityAttackDelivery>()?.Special == WarhammerAbilityAttackDelivery.SpecialType.Burst);

    private static bool Suitable(AbilityCasterHasWeaponWithRangeType check, ItemEntityWeapon weapon) =>
        weapon != null && check.RangeType.IsSuitableAttackType(weapon.Blueprint.AttackType)
        && (!check.MustHaveBurst || weapon.GetWeaponStats().ResultRateOfFire > 1);

    private static bool Prefix(AbilityCasterHasWeaponWithRangeType __instance, MechanicEntity caster, ref bool __result)
    {
        if (__instance.OnlyMainHand || !Abilities.IsOurs(__instance.OwnerBlueprint as BlueprintAbility))
            return true;
        try
        {
            var body = caster.GetBodyOptional();
            if (body == null)
            {
                __result = false;
                return false;
            }
            bool flag = (body.PrimaryHand.HasWeapon && (Suitable(__instance, body.PrimaryHand.Weapon) || HasBurstAbility(body.PrimaryHand.Weapon)))
                     || (body.SecondaryHand.HasWeapon && (Suitable(__instance, body.SecondaryHand.Weapon) || HasBurstAbility(body.SecondaryHand.Weapon)));
            foreach (var limb in body.AdditionalLimbs)
            {
                if (!limb.HasWeapon)
                    continue;
                flag = flag || (__instance.RangeType.IsSuitableWeapon(limb.Weapon) && (!__instance.MustHaveBurst || limb.Weapon.GetWeaponStats().ResultRateOfFire > 1)) || HasBurstAbility(limb.Weapon);
            }
            __result = flag;
            return false;
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: weapon-range caster check");
            return true;
        }
    }
}

// Reflex Shot and Overwatch can't be cast once the active ranged weapon is out of rounds: the stock
// AbilityCasterMainWeaponHasAmmo only reads the caster's main (or, with SecondWeapon, off) hand, not whichever
// slot actually holds the weapon our abilities fire with (RTO-T93); redone the same way as the weapon-type
// check above, scoped to the two ability blueprints that carry the component (ReflexShot_Ability,
// Overwatch_Ability; Suppression doesn't).
[HarmonyPatch(typeof(AbilityCasterMainWeaponHasAmmo), nameof(AbilityCasterMainWeaponHasAmmo.IsCasterRestrictionPassed))]
internal static class Abilities_RequireAmmoToCast
{
    private static bool Prefix(AbilityCasterMainWeaponHasAmmo __instance, MechanicEntity caster, ref bool __result)
    {
        if (!Abilities.IsOurs(__instance.OwnerBlueprint as BlueprintAbility))
            return true;
        try
        {
            __result = Abilities.HasAmmo(caster);
            return false;
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: ammo caster check");
            return true;
        }
    }
}

// UseFirstWeaponAbility fires GetFirstWeapon's attack, which is the melee weapon when a sword is in the main hand
// and a pistol in the off hand. Restart the overwatch on the ranged weapon's attack, before the shot budget is set.
[HarmonyPatch(typeof(AbilityEffectOverwatch), nameof(AbilityEffectOverwatch.Apply))]
internal static class Abilities_ShootWithRangedWeapon
{
    [HarmonyPriority(Priority.First)]
    private static void Postfix(AbilityExecutionContext context, TargetWrapper target)
    {
        try
        {
            if (!Abilities.IsOurs(context?.Ability) || context.MaybeCaster is not BaseUnitEntity caster)
                return;
            var part = caster.GetOptional<PartOverwatch>();
            var attack = Abilities.RangedAttack(caster);
            if (part == null || part.IsStopped || attack == null || part.AbilityOnTrigger?.Weapon == attack.Weapon)
                return;
            part.Start(context.Ability, attack, target, part.Mode, part.HitsPerTarget);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: choosing the ranged weapon");
        }
    }
}
