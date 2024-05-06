using System;
using HarmonyLib;
using PP2ProductionStats.Helpers;
using PP2ProductionStats.Structs;
using Object = UnityEngine.Object;

namespace PP2ProductionStats.Patchers;

public static class GarrisonPatcher
{
    private static readonly string[] TextParents =
    {
        "2DCanvasLandscape/GameUI/DialogBackground/DialogsContent/GarrisonDialog/DialogBody/Content/Scroll View/Viewport/Content/Garrison/UnitPanel",
        "2DCanvasPortrait/GameUI/DialogBackground/DialogsContent/GarrisonDialog/Layout/DialogBody/Content/Scroll View/Viewport/Content/Garrison/UnitPanel",
    };

    private static UpdateState _state = new(DateTime.UtcNow, 0, false, false);

    [HarmonyPatch(typeof(UnitData), "UpdateLiveBindings")]
    [HarmonyPostfix]
    private static void UpdateUnitTextPatch(UnitData __instance, ref LiveBindingNode node)
    {
        try
        {
            UpdateUnitText(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(ex);
        }
    }
    
    private static void UpdateUnitText(UnitData instance)
    {
        var unit = instance.Identifier;

        var newState = new UpdateState(
            DateTime.UtcNow,
            (int)unit,
            Singleton<UIManager>.Instance.IsPortrait,
            Plugin.PerHour);

        if (!newState.IsDirty(_state) || Singleton<GarrisonManager>.Instance.SelectedUnit != unit)
        {
            return;
        }

        _state = newState;

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