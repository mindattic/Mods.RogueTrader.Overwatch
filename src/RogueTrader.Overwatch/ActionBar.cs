using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.UI.Models.UnitSettings;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Progression.Features;

namespace RogueTraderOverwatch;

// Puts a party member's granted abilities (Reflex Shot, Overwatch, Suppression, Hustle, ...) on the ability
// bar: the last slots of row 2 (RTO-T112; sized to the whole set, so it's slots 17-20 for today's 4
// abilities, growing toward row 1 as more are added), else the first free slot anywhere. Runs whether or
// not the game's "auto-fill action bar" setting is on, and leaves off any the player removed from the bar.
[HarmonyPatch(typeof(PartUnitUISettings), nameof(PartUnitUISettings.TryToInitialize))]
internal static class ActionBar
{
    private const int RowSize = 10;
    private const int TargetRow = 1;

    private static readonly FieldInfo RemovedField = AccessTools.Field(typeof(PartUnitUISettings), "m_RemovedFromActionBar");
    private static readonly MethodInfo ContainsMethod = AccessTools.Method(RemovedField.FieldType, "Contains");

    // The bar VM calls TryToInitialize when the selected unit changes and whenever its abilities change, just
    // before it reads the slots, so placing here shows up without any extra refresh.
    private static void Prefix(PartUnitUISettings __instance)
    {
        try
        {
            var feature = ResourcesLibrary.TryGetBlueprint<BlueprintFeature>(Main.PartyFeatureGuid);
            if (__instance.Owner is BaseUnitEntity unit && feature != null && unit.Facts.Contains(feature))
                Place(unit);
        }
        catch (Exception ex)
        {
            PFLog.Mods.Exception(ex, "RogueTrader.Overwatch: placing abilities on the action bar");
        }
    }

    public static void Place(BaseUnitEntity unit)
    {
        var settings = unit.UISettings;
        var missing = new List<Kingmaker.UnitLogic.Abilities.Ability>();
        foreach (var guid in Abilities.BarPlaced)
        {
            var blueprint = ResourcesLibrary.TryGetBlueprint<BlueprintAbility>(guid);
            var ability = blueprint == null ? null : unit.Abilities.GetAbility(blueprint);
            if (ability == null || ability.SourceItem != null || IsOnBar(unit, blueprint) || WasRemoved(settings, blueprint))
                continue;
            missing.Add(ability);
        }
        if (missing.Count == 0)
            return;

        // The very end of row 2, sized to the whole granted set, not just what's missing right now: a
        // character who already has some of these (an earlier save, or an earlier grant) keeps the same
        // window reserved rather than it drifting with how many are left to place.
        var free = new List<int>();
        int windowEnd = (TargetRow + 1) * RowSize;
        int windowStart = windowEnd - Abilities.BarPlaced.Length;
        for (int i = windowStart; i < windowEnd && free.Count < missing.Count; i++)
            if (IsEmpty(settings, i))
                free.Add(i);
        for (int i = 0; free.Count < missing.Count && i < 100; i++)
            if (!free.Contains(i) && IsEmpty(settings, i))
                free.Add(i);

        foreach (var (ability, index) in missing.Zip(free, (a, i) => (a, i)))
            settings.SetSlot(unit, ability, index);
    }

    // Without GetSlot, which would grow the slot list (and the bar) just by looking.
    private static bool IsEmpty(PartUnitUISettings settings, int index)
    {
        if (index >= settings.Slots.Count)
            return true;
        var slot = settings.Slots[index];
        return slot == null || slot is MechanicActionBarSlotEmpty || slot.IsBad();
    }

    private static bool IsOnBar(BaseUnitEntity unit, BlueprintAbility blueprint) =>
        unit.UISettings.Slots.OfType<MechanicActionBarSlotAbility>().Any(s => s.Ability?.Blueprint == blueprint);

    private static bool WasRemoved(PartUnitUISettings settings, BlueprintAbility blueprint)
    {
        var removed = RemovedField.GetValue(settings);
        return removed != null && (bool)ContainsMethod.Invoke(removed, new object[] { blueprint, null });
    }
}
