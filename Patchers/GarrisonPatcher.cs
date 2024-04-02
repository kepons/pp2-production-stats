using System;
using HarmonyLib;
using PP2ProductionStats.Helpers;
using Object = UnityEngine.Object;

namespace PP2ProductionStats.Patchers;

public static class GarrisonPatcher
{
    private static readonly string[] TextParents =
    {
        "2DCanvasLandscape/GameUI/DialogBackground/DialogsContent/GarrisonDialog/DialogBody/Content/Scroll View/Viewport/Content/Garrison/UnitPanel",
        "2DCanvasPortrait/GameUI/DialogBackground/DialogsContent/GarrisonDialog/Layout/DialogBody/Content/Scroll View/Viewport/Content/Garrison/UnitPanel",
    };

    private static (DateTime time, EnumUnit unit, bool isPortrait) _lastUpdate = (DateTime.UtcNow,
        EnumUnit.None, false);

    [HarmonyPatch(typeof(UnitData), "UpdateLiveBindings")]
    [HarmonyPostfix]
    private static void UpdateUnitText(UnitData __instance, ref LiveBindingNode node)
    {
        var unit = __instance.Identifier;

        if (!ShouldUpdate(unit) || Singleton<GarrisonManager>.Instance.SelectedUnit != unit)
        {
            return;
        }
        
        Plugin.Log.LogDebug($"Updating {unit} data...");

        _lastUpdate = (DateTime.UtcNow, unit, Singleton<UIManager>.Instance.IsPortrait);

        var label = TextParents.GetGameObjectFromPaths()?.GetOrCreateLabel("UnitFlow");

        if (label == null)
        {
            return;
        }

        var producerBuildingCounts = new CountDictionary<string>();
        var consumerBuildingCounts = new CountDictionary<string>();

        var (totalProduced, totalConsumed) = GetRatesFromBarracks(unit, producerBuildingCounts, consumerBuildingCounts);

        totalProduced += GetProductionFromPopulation(unit, producerBuildingCounts);
        
        label.text = TextHelper.BuildText(totalProduced, totalConsumed, producerBuildingCounts, consumerBuildingCounts);
    }

    private static bool ShouldUpdate(EnumUnit unit)
    {
        return DateTime.UtcNow - _lastUpdate.time >= TimeSpan.FromSeconds(3)
               || unit != _lastUpdate.unit
               || Singleton<UIManager>.Instance.IsPortrait != _lastUpdate.isPortrait;
    }

    private static Rational GetProductionFromPopulation(
        EnumUnit unit,
        CountDictionary<string> producers)
    {
        var currentIsland = Singleton<IslandManager>.Instance.ActiveIsland;

        var pop = currentIsland.Population.PopulationTiers;

        var total = Rational.zero;

        foreach (var tier in pop)
        {
            if (GetUnitFromResource(tier.Data.ProducedResource) != unit)
            {
                continue;
            }

            var productionAmount = tier.CurrentProductionAmount;

            if (productionAmount == 0)
            {
                continue;
            }
            
            total += productionAmount;
            
            producers.Inc(tier.Data.DisplayName, tier.CurrentPopulation);
        }

        return total * 60;
    }

    private static (Rational produced, Rational consumed) GetRatesFromBarracks(
        EnumUnit unit,
        CountDictionary<string> producers,
        CountDictionary<string> consumers)
    {
        var barracks = Object.FindObjectsOfType<Barrack>();

        var totalProduced = Rational.zero;
        var totalConsumed = Rational.zero;

        foreach (var barrack in barracks)
        {
            var cooldown = barrack.Cooldown;

            if (barrack.IsPausedByHand)
            {
                continue;
            }

            if (barrack.ConsumedUnit == unit)
            {
                totalConsumed += new Rational(barrack.ConsumedUnitCount, cooldown);
                consumers.Inc(barrack.GameEntityData.DisplayName());
            }

            if (barrack.ProducedUnit == unit)
            {
                totalProduced += new Rational(1, cooldown);
                producers.Inc(barrack.GameEntityData.DisplayName());
            }
        }

        return (totalProduced * 60, totalConsumed * 60);
    }
    
    // The game's extension class that has a similar function is internal
    private static EnumUnit GetUnitFromResource(EnumResource resource) => resource switch
    {
        EnumResource.Militia => EnumUnit.Militia,
        EnumResource.RangedMilitia => EnumUnit.RangedMilitia,
        _ => EnumUnit.None,
    };
}