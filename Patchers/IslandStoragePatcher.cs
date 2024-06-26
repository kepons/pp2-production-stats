﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PP2ProductionStats.Helpers;
using PP2ProductionStats.Structs;
using Object = UnityEngine.Object;

namespace PP2ProductionStats.Patchers;

public static class IslandStoragePatcher
{
    private static readonly string[] TextParents =
    {
        "2DCanvasLandscape/GameUI/DialogBackground/DialogsContent/IslandStorageDialog/DialogBody/Content/RightColumn/PreStaticContent/SelectedGood",
        "2DCanvasPortrait/GameUI/DialogBackground/DialogsContent/IslandStorageDialog/Layout/DialogBody/Content/PostStaticContent/SelectedGood"
    };

    private static UpdateState _state = new(DateTime.UtcNow, 0, false, false);

    [HarmonyPatch(typeof(ResourceStorageItem), "UpdateHistoryLiveBindings")]
    [HarmonyPostfix]
    private static void UpdateResourceTextPatch(ResourceStorageItem __instance, ref LiveBindingNode node)
    {
        try
        {
            UpdateResourceText(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(ex);
        }
    }
    
    private static void UpdateResourceText(ResourceStorageItem instance)
    {
        var resource = instance.Type;

        var newState = new UpdateState(
            DateTime.UtcNow,
            (int)resource,
            Singleton<UIManager>.Instance.IsPortrait,
            Plugin.PerHour);

        if (!newState.IsDirty(_state))
        {
            return;
        }

        _state = newState;

        var label = TextParents.GetGameObjectFromPaths()?.GetOrCreateLabel("ResourceFlow");

        if (label == null)
        {
            return;
        }

        var producerBuildingCounts = new CountDictionary<string>();
        var consumerBuildingCounts = new CountDictionary<string>();

        var (totalProduced, totalConsumed) =
            GetRatesFromFactories(resource, producerBuildingCounts, consumerBuildingCounts);

        totalProduced += GetProductionFromGatherers(resource, producerBuildingCounts);
        totalProduced += GetProductionFromHarvesters(resource, producerBuildingCounts);
        totalConsumed += GetConsumptionFromEffectEmitters(resource, consumerBuildingCounts);
        totalConsumed += GetConsumptionFromBarracks(resource, consumerBuildingCounts);
        totalConsumed += GetConsumptionFromPopulation(resource, consumerBuildingCounts);
        totalConsumed += GetConsumptionFromShipyards(resource, consumerBuildingCounts);
        
        label.text = TextHelper.BuildText(totalProduced, totalConsumed, producerBuildingCounts, consumerBuildingCounts);
    }

    private static Rational GetConsumptionFromShipyards(
        EnumResource resource,
        CountDictionary<string> consumers)
    {
        var shipyards = Object.FindObjectsOfType<Shipyard>()
            .Where(s => !s.IsPausedByHand
                        && s.IsProducing
                        && Enumerable.Any(
                            s.GetComponent<InternalStorage>().InputResourceList, r => r.Type == resource));

        var total = Rational.zero;

        // All shipyards of the same tier have the same internal storage items and cap regardless of the ship being
        // built. Resources from the internal storage are spent every `Cooldown` seconds to progress the ship.
        // Therefore, the max. consumption rate of ship materials for every resource is `Slot Cap / Cooldown`.
        foreach (var consumer in shipyards)
        {
            if (consumer.ProducedShip == null)
            {
                continue;
            }
            
            var cooldown = consumer.Cooldown;
            
            foreach (var internalSlot in consumer.GetComponent<InternalStorage>()
                         .InputResourceList.Where(s => s.Type == resource))
            {
                total += new Rational(internalSlot.Cap, cooldown);
            }

            consumers.Inc(consumer.GameEntityData.DisplayName());
        }

        return total * 60;
    }

    private static Rational GetConsumptionFromBarracks(
        EnumResource resource,
        CountDictionary<string> consumers)
    {
        var barracks = Object.FindObjectsOfType<Barrack>()
            .Where(g => !g.IsPausedByHand && g.ConsumedResource.Resource == resource);

        var totalConsumed = Rational.zero;

        foreach (var consumer in barracks)
        {
            totalConsumed += new Rational(consumer.ConsumedResource.Amount, consumer.Cooldown);

            consumers.Inc(consumer.GameEntityData.DisplayName());
        }

        return totalConsumed * 60;
    }

    // This won't guarantee precise production rates if two buildings are competing for a single resource where one
    // building's production rate is doubled but the other one's isn't, but should be otherwise accurate.
    private static Rational GetProductionFromHarvesters(
        EnumResource resource,
        CountDictionary<string> producers)
    {
        var harvesters = Object.FindObjectsOfType<Harvester>()
            .Where(h => !h.IsPausedByHand && h.HarvestedResource == resource);

        var harvestersWithResources = new List<(Harvester harvester, List<ResourceScore> resources)>();
        var reservedResources = new Dictionary<int, bool>();
        var total = Rational.zero;

        foreach (var harvester in harvesters)
        {
            harvestersWithResources.Add((harvester, GetAvailableResources(harvester).OrderBy(r => r.Score).ToList()));
        }

        foreach (var harvesterWithResources in harvestersWithResources.OrderBy(hr => hr.resources.Count))
        {
            var harvester = harvesterWithResources.harvester;
            var resources = harvesterWithResources.resources;
            
            producers.Inc(harvester.GameEntityData.DisplayName());

            if (!resources.Any())
            {
                continue;
            }

            var assignedResources = 0;
            var harvestCap = harvester.VirtualHarvesterCap;

            foreach (var resourceScore in resources)
            {
                if (assignedResources >= harvestCap)
                {
                    break;
                }

                var resourceId = resourceScore.Resource.GetInstanceID();

                if (reservedResources.ContainsKey(resourceId))
                {
                    continue;
                }

                reservedResources.Add(resourceId, true);
                assignedResources++;
            }

            var increase = new Rational(
                assignedResources * (harvester.IsProductionDoubled ? 2 : 1), harvester.Cooldown * harvestCap);

            if (increase <= 0)
            {
                continue;
            }

            total += increase;
        }

        return total * 60;
    }

    private static List<ResourceScore> GetAvailableResources(Harvester harvester)
    {
        var t = new Traverse(harvester);

        var cache = t.Field("NearbyResourceFieldCache").GetValue();

        if (cache is not List<ResourceField> cacheList)
        {
            cacheList = harvester.Field
                .GetComponents<ResourceField>(harvester.Range, harvester.RangeCornerCutting, false);
        }

        var resources = new List<ResourceScore>();

        foreach (var resourceField in cacheList.Where(c => c.ResourceType == harvester.HarvestedResource))
        {
            var nearbyHarvesters = resourceField.Field.EffectorCount(EnumFieldFlag.UsedByHarvester);

            foreach (var resource in resourceField.ResourceSpots)
            {
                resources.Add(new ResourceScore
                {
                    Resource = resource,
                    Score = nearbyHarvesters,
                });
            }
        }

        return resources;
    }

    private static Rational GetConsumptionFromPopulation(
        EnumResource resource,
        CountDictionary<string> consumers)
    {
        var currentIsland = Singleton<IslandManager>.Instance.ActiveIsland;

        var pop = currentIsland.Population.PopulationTiers;

        var total = Rational.zero;

        foreach (var tier in pop)
        {
            var prevTotal = total;

            var relevantNeeds = tier.BasicNeeds.ConsumedResources.Where(r => r.Data.ConsumedResource == resource)
                .Concat(tier.LuxuryNeeds.ConsumedResources.Where(r => r.Data.ConsumedResource == resource));

            foreach (var need in relevantNeeds)
            {
                total += need.CurrentConsumedAmount * tier.CurrentPopulation;
            }

            if (prevTotal != total)
            {
                consumers.Inc(tier.Data.DisplayName, tier.CurrentPopulation);
            }
        }

        return total * 60;
    }

    private static Rational GetConsumptionFromEffectEmitters(
        EnumResource resource,
        CountDictionary<string> consumers)
    {
        var emitters = Object.FindObjectsOfType<EffectEmitterWithCost>();

        var totalConsumed = Rational.zero;

        foreach (var emitter in emitters.Where(g => Enumerable.Any(g.Consumes, c => c.Resource == resource)))
        {
            var cooldown = emitter.Cooldown;

            if (emitter.IsPausedByHand)
            {
                continue;
            }

            foreach (var consumable in emitter.Consumes)
            {
                if (consumable.Resource != resource)
                {
                    continue;
                }

                totalConsumed += new Rational(consumable.Amount, cooldown);
            }

            consumers.Inc(emitter.GameEntityData.DisplayName());
        }

        return totalConsumed * 60;
    }

    private static Rational GetProductionFromGatherers(
        EnumResource resource,
        CountDictionary<string> producers)
    {
        var gatherers = Object.FindObjectsOfType<Gatherer>();

        var totalProduced = Rational.zero;

        foreach (var gatherer in gatherers.Where(g => g.GatheredResource == resource))
        {
            var cooldown = gatherer.Cooldown;

            if (gatherer.IsPausedByHand)
            {
                continue;
            }

            var gathererTiles = gatherer.GatherCount(gatherer.Field);
            var gatherAmount = gathererTiles * new Rational(1, gatherer.VirtualGatherCap);

            totalProduced += gatherAmount * (gatherer.IsProductionDoubled ? 2 : 1) / cooldown;

            producers.Inc(gatherer.GameEntityData.DisplayName());
        }

        return totalProduced * 60;
    }

    private static (Rational produced, Rational consumed) GetRatesFromFactories(
        EnumResource resource,
        CountDictionary<string> producers,
        CountDictionary<string> consumers)
    {
        var factories = Object.FindObjectsOfType<Factory>();

        var totalProduced = Rational.zero;
        var totalConsumed = Rational.zero;

        foreach (var factory in factories)
        {
            var cooldown = factory.Cooldown;

            if (factory.IsPausedByHand)
            {
                continue;
            }

            foreach (var input in factory.Consumes.Where(input => input.Resource == resource))
            {
                totalConsumed += new Rational(input.Amount, cooldown);
                consumers.Inc(factory.GameEntityData.DisplayName());
            }

            foreach (var output in factory.Produces.Where(output => output.Resource == resource))
            {
                totalProduced += new Rational(output.Amount, cooldown);
                producers.Inc(factory.GameEntityData.DisplayName());
            }
        }

        return (totalProduced * 60, totalConsumed * 60);
    }

    private sealed class ResourceScore
    {
        public Resource Resource { get; set; }
        public int Score { get; set; }
    }
}