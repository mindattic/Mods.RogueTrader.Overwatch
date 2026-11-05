using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.Projectiles;
using Kingmaker.Controllers.TurnBased;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Pathfinding;
using Kingmaker.PubSubSystem;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using Kingmaker.Visual.Animation.Kingmaker;
using UnityEngine;

namespace RogueTraderOverwatch;

// Suppression: the ability puts a Suppressed buff on one enemy. Leaving the square it stands on, by any means
// (walking, a charge or jump, being pushed), draws one real attack from the buff's caster: once per command, so a
// long move is one shot, not one per square. During its turn the caster also fires cosmetic, always-missing shots.
// Nothing here touches PartOverwatch, so the target's actions never trigger it. The buff's TurnStartOrCombatEnd end
// condition removes it when the caster's next turn starts.
internal sealed class Suppression : IUnitBuffHandler, IEntityPositionChangedHandler, IAreaHandler
{
    public const string BuffGuid = "c6af42a8c37040a49f86559e80959c5f";

    public static readonly Suppression Instance = new Suppression();

    // The square each suppressed target holds, and the command that already drew its shot for leaving it.
    private readonly Dictionary<BaseUnitEntity, CustomGridNodeBase> m_Anchors = new Dictionary<BaseUnitEntity, CustomGridNodeBase>();
    private readonly Dictionary<BaseUnitEntity, object> m_ShotForCommand = new Dictionary<BaseUnitEntity, object>();
    // Debounces an unexplained displacement (no owning command) to one queued shot per Tick, cleared there:
    // Time.time isn't synced between multiplayer peers, so gating a real (hashed-state) shot by wall-clock
    // elapsed time let host and client reach different verdicts and desync (RTO-T105).
    private readonly HashSet<BaseUnitEntity> m_FiredUnexplainedThisTick = new HashSet<BaseUnitEntity>();
    private bool m_NeedsScan = true;
    private readonly List<(BaseUnitEntity Caster, BaseUnitEntity Target)> m_ShotsToFire = new List<(BaseUnitEntity, BaseUnitEntity)>();
    private readonly Dictionary<BaseUnitEntity, float> m_NextCosmeticShot = new Dictionary<BaseUnitEntity, float>();

    private static IEnumerable<Buff> SuppressedBuffs(MechanicEntity unit) =>
        unit?.Buffs?.Enumerable.Where(b => b.Blueprint?.AssetGuid == BuffGuid) ?? Enumerable.Empty<Buff>();

    public static bool IsSuppressed(MechanicEntity unit) => SuppressedBuffs(unit).Any();

    public static IEnumerable<BaseUnitEntity> Suppressors(BaseUnitEntity target) =>
        SuppressedBuffs(target)
            .Select(b => b.Context?.MaybeCaster as BaseUnitEntity)
            .Where(c => c != null && !c.IsDeadOrUnconscious)
            .Distinct();

    private static CustomGridNodeBase Square(BaseUnitEntity unit) => unit.CurrentUnwalkableNode as CustomGridNodeBase;

    private static AbilityData FirstWeaponAbility(BaseUnitEntity unit) => Abilities.RangedAttack(unit);

    // Whether the suppressor's ranged attack has a projectile for the cosmetic fire (AimAndFire) to launch.
    public static bool FiresProjectiles(BaseUnitEntity unit) => FirstWeaponAbility(unit)?.ProjectileVariants?.Any() == true;

    public void HandleBuffDidAdded(Buff buff)
    {
        try
        {
            if (buff?.Blueprint?.AssetGuid == BuffGuid && buff.Owner is BaseUnitEntity target)
            {
                m_Anchors[target] = Square(target);
                PFLog.Mods.Log($"RogueTrader.Overwatch: Suppressed applied to {target.CharacterName} by {(buff.Context?.MaybeCaster as BaseUnitEntity)?.CharacterName ?? "?"}");
            }
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "Suppression: buff added");
        }
    }

    public void HandleBuffDidRemoved(Buff buff)
    {
        try
        {
            if (buff?.Blueprint?.AssetGuid == BuffGuid && buff.Owner is BaseUnitEntity target && !SuppressedBuffs(target).Any(b => b != buff))
            {
                m_Anchors.Remove(target);
                m_ShotForCommand.Remove(target);
                m_FiredUnexplainedThisTick.Remove(target);
            }
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "Suppression: buff removed");
        }
    }

    public void HandleBuffRankIncreased(Buff buff) { }

    public void HandleBuffRankDecreased(Buff buff) { }

    public void HandleBuffIsSuppressedChanged(Buff buff) { }

    // After a load the anchors are rebuilt from the saved buffs (see Tick).
    public void OnAreaDidLoad() => m_NeedsScan = true;

    // Everything keyed by the old area's units goes with it. Held poses are dropped, not released: their unit views
    // are being destroyed. The mod's other per-unit state is cleared here too, as this is its one area handler.
    public void OnAreaBeginUnloading()
    {
        m_Anchors.Clear();
        m_ShotForCommand.Clear();
        m_FiredUnexplainedThisTick.Clear();
        m_ShotsToFire.Clear();
        m_NextCosmeticShot.Clear();
        m_Pose.Clear();
        OverwatchCone.Watches.Clear();
        OverwatchCone.PendingShots.Clear();
        OverwatchDebuff.Pending.Clear();
    }

    public void HandleEntityPositionChanged()
    {
        try
        {
            if (!(EventInvokerExtensions.Entity is BaseUnitEntity target) || !m_Anchors.TryGetValue(target, out var anchor))
                return;
            var square = Square(target);
            if (square == null || square == anchor)
                return;
            m_Anchors[target] = square;
            if (anchor == null || !TurnController.IsInTurnBasedCombat())
                return;
            // Once per command that moves it: its own, or for a push (no command of its own) the command of whoever's
            // turn it is. A displacement neither explains is limited to one shot per Tick (RTO-T105).
            object cause = target.Commands.Current ?? (Game.Instance.TurnController.CurrentUnit as BaseUnitEntity)?.Commands.Current;
            if (cause != null)
            {
                if (m_ShotForCommand.TryGetValue(target, out var shotFor) && ReferenceEquals(shotFor, cause))
                    return;
                m_ShotForCommand[target] = cause;
            }
            else
            {
                if (!m_FiredUnexplainedThisTick.Add(target))
                    return;
            }
            foreach (var caster in Suppressors(target))
                m_ShotsToFire.Add((caster, target));
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "Suppression: position changed");
        }
    }

    private void ScanIfNeeded()
    {
        if (!m_NeedsScan)
            return;
        m_NeedsScan = false;
        foreach (var unit in Game.Instance.State.AllBaseAwakeUnits)
            if (!m_Anchors.ContainsKey(unit) && SuppressedBuffs(unit).Any())
                m_Anchors[unit] = Square(unit);
    }

    // Runs in the same simulation tick as the engine's own overwatch shots.
    public void Tick()
    {
        if (!TurnController.IsInTurnBasedCombat())
        {
            m_ShotsToFire.Clear();
            m_NextCosmeticShot.Clear();
            m_FiredUnexplainedThisTick.Clear();
            foreach (var caster in m_Pose.Keys.ToList())
                ReleasePose(caster);
            return;
        }
        // A fresh debounce window for this tick's own HandleEntityPositionChanged events, not the last one's.
        m_FiredUnexplainedThisTick.Clear();
        ScanIfNeeded();
        FireQueuedShots();
        CosmeticFire();
    }

    private void FireQueuedShots()
    {
        if (m_ShotsToFire.Count == 0)
            return;
        var shots = m_ShotsToFire.ToList();
        m_ShotsToFire.Clear();
        foreach (var (caster, target) in shots)
        {
            try
            {
                if (caster.IsDeadOrUnconscious || target.IsDeadOrUnconscious)
                    continue;
                // The same per-round reaction budget a melee threat or Reflex Shot/Overwatch draws from
                // (RTO-T94): a mook who's already spent its one reaction this round can't also fire Suppression's.
                if (!Abilities.CanMakeAttackOfOpportunity(caster))
                    continue;
                var ability = FirstWeaponAbility(caster);
                if (ability == null || !ability.CanTarget(target, out _))
                    continue;
                // A real clone, not the shared AbilityData the unit also fires its normal attacks with: flagging
                // it IsAttackOfOpportunity so feats/traits that key off that flag (CheckIfAttackOfOpportunityGetter)
                // apply here too, matching the Suppressed description ("a ranged attack of opportunity").
                var aooAbility = ability.Clone(isPreview: false);
                aooAbility.IsAttackOfOpportunity = true;
                caster.Commands.Run(new UnitOverwatchAttackParams(aooAbility, new TargetWrapper(target)));
                Abilities.SpentAttackOfOpportunity(caster);
                m_NextCosmeticShot[caster] = Time.time + UnityEngine.Random.Range(1.5f, 2.5f);
            }
            catch (Exception ex)
            {
                PFLog.Mods.Exception(ex, "Suppression: attack of opportunity");
            }
        }
    }

    // While any unit is Suppressed, its suppressors hold a firing pose trained on it and keep firing at it, on every
    // turn, as a visual cue only (see AimAndFire). Paused while the suppressor is busy (e.g. playing a real attack of
    // opportunity) or can't see the target.
    private void CosmeticFire()
    {
        var suppressing = new HashSet<BaseUnitEntity>();
        foreach (var target in m_Anchors.Keys.ToList())
        {
            if (target.IsDeadOrUnconscious)
                continue;
            foreach (var caster in Suppressors(target))
            {
                suppressing.Add(caster);
                try
                {
                    if (!m_NextCosmeticShot.TryGetValue(caster, out var next))
                    {
                        m_NextCosmeticShot[caster] = Time.time + UnityEngine.Random.Range(0.3f, 0.8f);
                        continue;
                    }
                    if (Time.time < next || !caster.Commands.Empty || !caster.HasLOS(target))
                        continue;
                    ReleasePose(caster);
                    var handle = AimAndFire(caster, target);
                    if (handle != null)
                        m_Pose[caster] = handle;
                    m_NextCosmeticShot[caster] = Time.time + UnityEngine.Random.Range(0.9f, 1.5f);
                }
                catch (Exception ex)
                {
                    PFLog.Mods.Exception(ex, "Suppression: cosmetic fire");
                    m_NextCosmeticShot[caster] = Time.time + 5f;
                }
            }
        }
        HoldPoses(suppressing);
    }

    // The firing pose: each cosmetic shot's attack animation is frozen at HoldAt of its length (weapon raised, just
    // after the shot) until the next shot replaces it, so the suppressor stays aimed at the target the whole time.
    // Released (and its speed restored) when the suppression ends or the unit has a real command to play.
    private const float HoldAt = 0.45f;
    private readonly Dictionary<BaseUnitEntity, UnitAnimationActionHandle> m_Pose = new Dictionary<BaseUnitEntity, UnitAnimationActionHandle>();

    private void HoldPoses(HashSet<BaseUnitEntity> suppressing)
    {
        foreach (var caster in m_Pose.Keys.ToList())
        {
            var handle = m_Pose[caster];
            if (!suppressing.Contains(caster) || !caster.Commands.Empty || handle == null || handle.IsReleased || handle.IsFinished)
            {
                ReleasePose(caster);
                continue;
            }
            Freeze(handle);
        }
    }

    // Every frame (VisualsDriver): the simulation tick is too sparse to catch the hold frame before the animation
    // runs past it or finishes.
    public void FreezePoses()
    {
        foreach (var handle in m_Pose.Values)
            if (handle != null && !handle.IsReleased && !handle.IsFinished)
                Freeze(handle);
    }

    private static void Freeze(UnitAnimationActionHandle handle)
    {
        var animation = handle.ActiveAnimation;
        if (animation != null && animation.GetSpeed() > 0f && animation.GetTime() >= animation.GetDuration() * HoldAt)
            animation.SetSpeed(0f);
    }

    private void ReleasePose(BaseUnitEntity caster)
    {
        if (!m_Pose.TryGetValue(caster, out var handle))
            return;
        m_Pose.Remove(caster);
        if (handle == null || handle.IsReleased)
            return;
        handle.ActiveAnimation?.SetSpeed(1f);
        handle.Release();
    }

    // The weapon's firing animation turned toward the target, plus the weapon's own projectile flying at it with no
    // hit delivered and no impact effect: a visual cue only. Nothing is rolled, logged or spent, and Unity's random
    // is used, so the game state and its hash are untouched.
    private static UnitAnimationActionHandle AimAndFire(BaseUnitEntity caster, BaseUnitEntity target)
    {
        var ability = FirstWeaponAbility(caster);
        var weapon = ability?.Weapon;
        var animation = caster.View?.AnimationManager;
        if (weapon == null || animation == null)
            return null;
        var flat = target.Position - caster.Position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f)
            return null;
        animation.UseAbilityDirection = Quaternion.LookRotation(flat).eulerAngles.y;
        var handle = animation.CreateHandle(UnitAnimationType.MainHandAttack, errorOnEmpty: false);
        if (handle == null)
            return null;
        handle.AttackWeaponStyle = caster.View.HandsEquipment?.GetWeaponStyleForWeapon(weapon) ?? weapon.GetAnimationStyle();
        handle.AttackTargetDistance = flat.magnitude;
        handle.IsBurst = false;
        animation.Execute(handle);

        var projectiles = ability.ProjectileVariants?.ToList();
        if (projectiles == null || projectiles.Count == 0)
            return handle;
        new ProjectileLauncher(projectiles[UnityEngine.Random.Range(0, projectiles.Count)], caster, new TargetWrapper(NearMiss(flat, target)))
            .Ability(ability)
            .SetDoNotDeliverHit(true)
            .SetDoNotPlayHitEffect(true)
            .Launch();
        return handle;
    }

    // Where a cosmetic round ends: past the target and off to its left or right across the line of fire, low enough
    // to strike the ground behind it, so its path always streaks by the target (about a metre clear) and never
    // through it.
    private static Vector3 NearMiss(Vector3 lineOfFire, BaseUnitEntity target)
    {
        var along = lineOfFire.normalized;
        var across = Vector3.Cross(Vector3.up, along) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        return target.Position
               + across * UnityEngine.Random.Range(1.2f, 2f)
               + along * UnityEngine.Random.Range(2f, 5f)
               + Vector3.up * UnityEngine.Random.Range(0f, 0.8f);
    }
}

// Pinned down: a Suppressed unit's own ranged attacks keep HitChancePercent of their chance (RTO-T86, eased to 75% in
// RTO-T87: a pin, not a disable, for an ability that can be a turn's last spare AP). The final, capped chance after
// every modifier is scaled at the end of the game's ranged calculation, so a very accurate shooter loses the same
// share as anyone. Melee and scatter attacks use the game's border chance and are left alone.
[HarmonyPatch(typeof(RuleCalculateHitChances), "OnTriggerRanged")]
internal static class Suppression_ReduceHitChance
{
    public const int HitChancePercent = 75;

    [HarmonyPriority(Priority.Low)]
    private static void Postfix(RuleCalculateHitChances __instance)
    {
        try
        {
            if (__instance.IsScatter || __instance.Initiator is not BaseUnitEntity shooter || !Suppression.IsSuppressed(shooter))
                return;
            // Destructibles get the border chance from the game; leave that as it is.
            if (!__instance.Ability.IsBurstAttack && __instance.Target is DestructibleEntity)
                return;
            ReflexShot.SetRawHitChance(__instance, Mathf.RoundToInt(__instance.ResultHitChance * HitChancePercent / 100f));
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "Suppression: reducing a suppressed unit's hit chance");
        }
    }
}

[HarmonyPatch(typeof(OverwatchController), nameof(OverwatchController.Tick))]
internal static class SuppressionTickPatch
{
    private static void Postfix()
    {
        try
        {
            Suppression.Instance.Tick();
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "Suppression: tick");
        }
    }
}
