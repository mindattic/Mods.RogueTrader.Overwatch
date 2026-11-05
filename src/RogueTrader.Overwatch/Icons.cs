using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Modding;
using Kingmaker.UnitLogic.Mechanics.Blueprints;
using RogueTrader.Shared;
using UnityEngine;

namespace RogueTraderOverwatch;

// Loads Icons/<name>.png from the mod folder onto our blueprints, so icons need no Unity asset bundle.
[HarmonyPatch(typeof(OwlcatModification), nameof(OwlcatModification.ApplyContent))]
internal static class Icons
{
    private static readonly Dictionary<string, string> Files = new()
    {
        ["ReflexShot"] = "27f7f769d0b94ab18fee0cde8a577a4d",
        ["Overwatch"] = "b5855687981e4ba9a7dfc8c4af03229f",
        ["OverwatchShots"] = "e5ce47452a6d47d78e88178f4ee7bba5",
        ["Suppression"] = "5db3949e7bf544bcb10cc3a2d51eb9ea",
        ["Suppressed"] = "c6af42a8c37040a49f86559e80959c5f",
        ["Hustle"] = "fb73730b0fdb4689939fed3edd2af32a",
    };

    private static readonly AccessTools.FieldRef<BlueprintMechanicEntityFact, Sprite> IconField =
        AccessTools.FieldRefAccess<BlueprintMechanicEntityFact, Sprite>("m_Icon");

    // ApplyContent loads the mod's blueprints, after the entry point has run.
    private static void Postfix(OwlcatModification __instance)
    {
        if (__instance.Manifest?.UniqueName != "RogueTrader.Overwatch")
            return;
        foreach (var pair in Files)
        {
            string name = pair.Key;
            try
            {
                var blueprint = ResourcesLibrary.TryGetBlueprint<BlueprintMechanicEntityFact>(pair.Value);
                var sprite = Sprites.FromPng(Path.Combine(__instance.Path, "Icons", name + ".png"));
                if (blueprint != null && sprite != null)
                    IconField(blueprint) = sprite;
            }
            catch (Exception ex)
            {
                ModLog.Exception(ex, "loading icon " + name);
            }
        }
    }
}
