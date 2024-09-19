﻿using Bloodcraft.Systems.Quests;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Bloodcraft.Utilities;

internal static class EntityUtilities
{
    static EntityManager EntityManager => Core.EntityManager;

    static readonly HashSet<string> FilteredTargets =
    [
        "Trader",
        "HostileVillager",
        "TombSummon",
        "StatueSpawn",
        "SmiteOrb",
        "CardinalAide",
        "GateBoss",
        "DraculaMinion",
        "Summon",
        "Minion",
        "Chieftain",
        "ConstrainingPole",
        "Horse",
        "EnchantedCross",
        "DivineAngel",
        "FallenAngel",
        "FarbaneSuprise",
        "Withered",
        "Servant",
        "Spider_Melee",
        "Spider_Range",
        "GroundSword",
        "FloatingWeapon",
        "Airborne"
    ];

    static readonly HashSet<string> FilteredCrafts =
    [
        "Item_Cloak",
        "BloodKey_T01",
        "NewBag",
        "Miners",
        "ShadowMatter",
        "T0X",
        "Heart_T",
        "Water_T",
        "FakeItem",
        "PrisonPotion",
        "Dracula",
        "Consumable_Empty",
        "Reaper_T02",
        "Slashers_T02",
        "FishingPole",
        "Disguise",
        "Canister",
        "Trippy",
        "Eat_Rat",
        "Irradiant",
        "Slashers_T01",
        "Reaper_T01",
        "GarlicResistance"
    ];

    static readonly HashSet<string> FilteredResources = [];
    public static IEnumerable<Entity> GetEntitiesEnumerable(EntityQuery entityQuery, int targetType = -1) // not sure if need to actually check for empty buff buffer for quest targets but don't really want to find out
    {
        JobHandle handle = GetEntities(entityQuery, out NativeArray<Entity> entities, Allocator.TempJob);
        handle.Complete();
        try
        {
            foreach (Entity entity in entities)
            {
                if (targetType == 0)
                {
                    if (entity.Has<DestroyOnSpawn>()) continue;
                    else if (entity.TryGetComponent(out PrefabGUID unitPrefab))
                    {
                        string prefabName = unitPrefab.LookupName();
                        if (!FilteredTargets.Any(part => prefabName.Contains(part))) yield return entity;
                    }
                }
                else if (targetType == 1)
                {
                    if (entity.TryGetComponent(out PrefabGUID unitPrefab))
                    {
                        string prefabName = unitPrefab.LookupName();
                        if (!FilteredCrafts.Any(part => prefabName.Contains(part))) yield return entity;
                    }
                }
                else if (EntityManager.Exists(entity))
                {
                    yield return entity;
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
    static JobHandle GetEntities(EntityQuery entityQuery, out NativeArray<Entity> entities, Allocator allocator = Allocator.TempJob)
    {
        entities = entityQuery.ToEntityArray(allocator);
        return default;
    }
}
