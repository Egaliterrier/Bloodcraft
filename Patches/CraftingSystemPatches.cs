using Bloodcraft.Services;
using Bloodcraft.Systems.Professions;
using Bloodcraft.Systems.Quests;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Entities;
using User = ProjectM.Network.User;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class CraftingSystemPatches // ForgeSystem_Update, UpdateCraftingSystem
{
    static EntityManager EntityManager => Core.EntityManager;
    static ServerGameManager ServerGameManager => Core.ServerGameManager;
    static SystemService SystemService => Core.SystemService;
    static PrefabCollectionSystem PrefabCollectionSystem => SystemService.PrefabCollectionSystem;
    static NetworkIdSystem.Singleton NetworkIdSystem => SystemService.NetworkIdSystem;

    static readonly bool _professions = ConfigService.ProfessionSystem;
    static readonly bool _quests = ConfigService.QuestSystem;

    const float CRAFT_THRESHOLD = 0.975f;
    static readonly float _craftRateModifier = SystemService.ServerGameSettingsSystem._Settings.CraftRateModifier;

    static readonly ConcurrentDictionary<ulong, Dictionary<Entity, Dictionary<PrefabGUID, int>>> _playerCraftingJobs = []; // guess I'll just start using these if in doubt about the order of operations, so to speak >_>   
    public static readonly ConcurrentDictionary<ulong, Dictionary<Entity, Dictionary<PrefabGUID, int>>> ValidatedCraftingJobs = [];

    [HarmonyPatch(typeof(ForgeSystem_Update), nameof(ForgeSystem_Update.OnUpdate))]
    [HarmonyPrefix]
    static void Prefix(ForgeSystem_Update __instance)
    {
        if (!Core._initialized) return;
        else if (!_professions && !_quests) return;

        NativeArray<Entity> repairEntities = __instance.__query_1536473549_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in repairEntities)
            {
                Forge_Shared forge_Shared = entity.Read<Forge_Shared>();
                if (forge_Shared.State == ForgeState.Empty) continue;

                UserOwner userOwner = entity.Read<UserOwner>();
                Entity userEntity = userOwner.Owner._Entity;
                User user = userEntity.Read<User>();
                ulong steamId = user.PlatformId;

                Entity itemEntity = forge_Shared.ItemEntity._Entity;
                PrefabGUID itemPrefab = itemEntity.Read<PrefabGUID>();

                if (itemEntity.Has<ShatteredItem>())
                {
                    itemPrefab = itemEntity.Read<ShatteredItem>().OutputItem;
                }
                else if (itemEntity.Has<UpgradeableLegendaryItem>())
                {
                    int tier = itemEntity.Read<UpgradeableLegendaryItem>().CurrentTier;
                    var buffer = itemEntity.ReadBuffer<UpgradeableLegendaryItemTiers>();
                    itemPrefab = buffer[tier].TierPrefab;
                }

                if (forge_Shared.State == ForgeState.Finished)
                {
                    if (steamId.TryGetPlayerQuests(out var quests)) QuestSystem.ProcessQuestProgress(quests, itemPrefab, 1, user);
                    else if (!ConfigService.ProfessionSystem) continue;

                    float ProfessionValue = 50f;
                    ProfessionValue *= ProfessionMappings.GetTierMultiplier(itemPrefab);
                    IProfessionHandler handler = ProfessionHandlerFactory.GetProfessionHandler(itemPrefab, "");

                    if (handler != null)
                    {
                        if (itemEntity.Has<Durability>())
                        {
                            Entity originalItem = PrefabCollectionSystem._PrefabGuidToEntityMap[itemPrefab];

                            Durability durability = itemEntity.Read<Durability>();
                            Durability originalDurability = originalItem.Read<Durability>();

                            if (durability.MaxDurability > originalDurability.MaxDurability) continue; // already handled

                            int level = handler.GetProfessionData(steamId).Key;

                            durability.MaxDurability *= (1 + level / (float)ConfigService.MaxProfessionLevel);
                            durability.Value = durability.MaxDurability;
                            itemEntity.Write(durability);

                            ProfessionSystem.SetProfession(entity, user.LocalCharacter.GetEntityOnServer(), steamId, ProfessionValue, handler);
                        }
                    }
                }
            }
        }
        finally
        {
            repairEntities.Dispose();
        }
    }

    static readonly Dictionary<Entity, bool> _craftFinished = [];

    [HarmonyPatch(typeof(UpdateCraftingSystem), nameof(UpdateCraftingSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(UpdateCraftingSystem __instance)
    {
        if (!Core._initialized) return;
        else if (!_professions && !_quests) return;

        NativeArray<Entity> entities = __instance.__query_1831452865_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.Has<CastleWorkstation>() && ServerGameManager.TryGetBuffer<QueuedWorkstationCraftAction>(entity, out var buffer))
                {
                    if (!buffer.IsEmpty)
                    {
                        if (!_craftFinished.ContainsKey(entity))
                        {
                            _craftFinished[entity] = false;
                        }

                        QueuedWorkstationCraftAction queuedWorkstationCraftAction = buffer[0];
                        float recipeReduction = entity.Read<CastleWorkstation>().WorkstationLevel.HasFlag(WorkstationLevel.MatchingFloor) ? 0.75f : 1f;

                        ProcessQueuedCraftAction(entity, queuedWorkstationCraftAction, recipeReduction);
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(UpdatePrisonSystem), nameof(UpdatePrisonSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(UpdatePrisonSystem __instance)
    {
        if (!Core._initialized) return;
        else if (!_professions) return;

        NativeArray<Entity> entities = __instance.EntityQueries[0].ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.Has<CastleWorkstation>() && ServerGameManager.TryGetBuffer<QueuedWorkstationCraftAction>(entity, out var buffer))
                {
                    if (!buffer.IsEmpty)
                    {
                        if (!_craftFinished.ContainsKey(entity))
                        {
                            _craftFinished[entity] = false;
                        }

                        QueuedWorkstationCraftAction queuedWorkstationCraftAction = buffer[0];

                        ProcessQueuedCraftAction(entity, queuedWorkstationCraftAction, 1f);
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(StartCraftingSystem), nameof(StartCraftingSystem.OnUpdate))]
    [HarmonyPrefix]
    public static void OnUpdatePrefix(StartCraftingSystem __instance)
    {
        if (!Core._initialized) return;
        else if (!_professions && !_quests) return;

        NativeArray<Entity> entities = __instance._StartCraftItemEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.TryGetComponent(out StartCraftItemEvent startCraftEvent) && entity.TryGetComponent(out FromCharacter fromCharacter))
                {
                    Entity craftingStation = NetworkIdSystem._NetworkIdLookupMap.TryGetValue(startCraftEvent.Workstation, out Entity station) ? station : Entity.Null;

                    PrefabGUID recipeGUID = startCraftEvent.RecipeId;
                    Entity recipePrefab = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipeGUID) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipeGUID] : Entity.Null;
                    PrefabGUID itemPrefabGUID = GetItemFromRecipePrefab(recipePrefab);

                    ulong steamId = fromCharacter.User.GetSteamId();

                    if (!_playerCraftingJobs.ContainsKey(steamId))
                    {
                        _playerCraftingJobs.TryAdd(steamId, []);
                    }

                    if (!_playerCraftingJobs[steamId].ContainsKey(craftingStation))
                    {
                        _playerCraftingJobs[steamId].Add(craftingStation, []);
                    }

                    Dictionary<PrefabGUID, int> RecipesCrafting = _playerCraftingJobs[steamId][craftingStation];

                    if (!RecipesCrafting.ContainsKey(itemPrefabGUID))
                    {
                        //Core.Log.LogInfo($"Crafting job added via StartCraftEvent for {itemPrefabGUID.LookupName()}| 1");
                        RecipesCrafting[itemPrefabGUID] = 1;
                    }
                    else
                    {
                        //Core.Log.LogInfo($"Crafting job added via StartCraftEvent for {itemPrefabGUID.LookupName()}| {RecipesCrafting[itemPrefabGUID] + 1}");
                        RecipesCrafting[itemPrefabGUID] = ++RecipesCrafting[itemPrefabGUID];
                    }

                    _playerCraftingJobs[steamId][craftingStation] = RecipesCrafting;
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(StopCraftingSystem), nameof(StopCraftingSystem.OnUpdate))]
    [HarmonyPrefix]
    public static void OnUpdatePrefix(StopCraftingSystem __instance)
    {
        if (!Core._initialized) return;
        else if (!_professions && !_quests) return;

        NativeArray<Entity> entities = __instance._EventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.TryGetComponent(out StopCraftItemEvent stopCraftEvent) && entity.TryGetComponent(out FromCharacter fromCharacter))
                {
                    Entity craftingStation = NetworkIdSystem._NetworkIdLookupMap.TryGetValue(stopCraftEvent.Workstation, out Entity station) ? station : Entity.Null;

                    PrefabGUID recipeGUID = stopCraftEvent.RecipeGuid;
                    Entity recipePrefab = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipeGUID) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipeGUID] : Entity.Null;
                    PrefabGUID itemPrefabGUID = GetItemFromRecipePrefab(recipePrefab);

                    ulong steamId = fromCharacter.User.GetSteamId();

                    if (ValidatedCraftingJobs.TryGetValue(steamId, out var validatedStationJobs) && validatedStationJobs.TryGetValue(craftingStation, out var validatedCraftingJobs))
                    {
                        if (validatedCraftingJobs.ContainsKey(itemPrefabGUID))
                        {
                            int jobs = validatedCraftingJobs[itemPrefabGUID];
                            validatedCraftingJobs[itemPrefabGUID] = --jobs;

                            //Core.Log.LogInfo($"Crafting job removed via StopCraftEvent for {itemPrefabGUID.LookupName()}| {validatedCraftingJobs[itemPrefabGUID]} in ValidatedCraftingJobs");
                            if (validatedCraftingJobs[itemPrefabGUID] <= 0) validatedCraftingJobs.Remove(itemPrefabGUID);
                        }
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(MoveItemBetweenInventoriesSystem), nameof(MoveItemBetweenInventoriesSystem.OnUpdate))]
    [HarmonyPrefix]
    public static void OnUpdatePrefix(MoveItemBetweenInventoriesSystem __instance)
    {
        if (!Core._initialized) return;
        else if (!_professions && !_quests) return;

        NativeArray<Entity> entities = __instance._MoveItemBetweenInventoriesEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.TryGetComponent(out MoveItemBetweenInventoriesEvent moveItemBetweenInventoriesEvent) && entity.TryGetComponent(out FromCharacter fromCharacter))
                {
                    ulong steamId = fromCharacter.Character.GetSteamId();

                    Entity receivingInventory = NetworkIdSystem._NetworkIdLookupMap.TryGetValue(moveItemBetweenInventoriesEvent.ToInventory, out Entity station) ? station : Entity.Null;
                    //Entity inventoryOwner = receivingInventory.Has<InventoryConnection>() ? receivingInventory.Read<InventoryConnection>().InventoryOwner : Entity.Null;
                    int fromSlot = moveItemBetweenInventoriesEvent.FromSlot;

                    PrefabGUID itemPrefabGUID = InventoryUtilities.TryGetInventoryEntity(EntityManager, fromCharacter.Character, out Entity playerInventory)
                        && ServerGameManager.TryGetBuffer<InventoryBuffer>(playerInventory, out var inventoryBuffer) && inventoryBuffer.TryGetAtIndex(fromSlot, out InventoryBuffer item) ? item.ItemType : PrefabGUID.Empty;

                    if (receivingInventory.Has<CastleWorkstation>())
                    {
                        if (_playerCraftingJobs.TryGetValue(steamId, out var stationJobs) && stationJobs.TryGetValue(receivingInventory, out var craftingJobs))
                        {
                            if (craftingJobs.ContainsKey(itemPrefabGUID))
                            {
                                int jobs = craftingJobs[itemPrefabGUID];
                                craftingJobs[itemPrefabGUID] = --jobs;

                                //Core.Log.LogInfo($"Crafting job removed via exploit prevention for {itemPrefabGUID.LookupName()}| {craftingJobs[itemPrefabGUID]} in playerCraftingJobs");
                                if (craftingJobs[itemPrefabGUID] <= 0) craftingJobs.Remove(itemPrefabGUID);
                            }
                        }

                        if (ValidatedCraftingJobs.TryGetValue(steamId, out var validatedStationJobs) && validatedStationJobs.TryGetValue(receivingInventory, out var validatedCraftingJobs))
                        {
                            if (validatedCraftingJobs.ContainsKey(itemPrefabGUID))
                            {
                                int jobs = validatedCraftingJobs[itemPrefabGUID];
                                validatedCraftingJobs[itemPrefabGUID] = --jobs;

                                //Core.Log.LogInfo($"Crafting job removed via exploit prevention for {itemPrefabGUID.LookupName()}| {validatedCraftingJobs[itemPrefabGUID]} in ValidatedCraftingJobs");
                                if (validatedCraftingJobs[itemPrefabGUID] <= 0) validatedCraftingJobs.Remove(itemPrefabGUID);
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
    static void ProcessQueuedCraftAction(Entity entity, QueuedWorkstationCraftAction craftAction, float recipeReduction)
    {
        Entity userEntity = craftAction.InitiateUser;
        ulong steamId = userEntity.GetSteamId();
        bool craftFinished = _craftFinished[entity];

        PrefabGUID recipeGUID = craftAction.RecipeGuid;
        Entity recipePrefab = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipeGUID) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipeGUID] : Entity.Null;
        PrefabGUID itemPrefabGUID = GetItemFromRecipePrefab(recipePrefab);

        //Core.Log.LogInfo($"Processing queued craft action for {itemPrefabGUID.LookupName()}... | {craftAction.ProgressTime}");

        if (recipePrefab.TryGetComponent(out RecipeData recipeData))
        {
            float craftDuration = recipeData.CraftDuration;
            float craftProgress = craftAction.ProgressTime;

            float totalTime = (craftDuration * recipeReduction) / _craftRateModifier;
            if (!craftFinished && craftProgress / totalTime >= CRAFT_THRESHOLD)
            {
                //Core.Log.LogInfo($"Crafting progress finished for {itemPrefabGUID.LookupName()}... | {craftAction.ProgressTime}:{totalTime}");
                _craftFinished[entity] = true;
                ValidateCraftingJob(entity, itemPrefabGUID, steamId);
            }
            else if (craftFinished && craftProgress / totalTime < CRAFT_THRESHOLD)
            {
                //Core.Log.LogInfo($"Crafting progress reset for {itemPrefabGUID.LookupName()}... | {craftAction.ProgressTime}:{totalTime}");
                _craftFinished[entity] = false;
            }
        }
    }
    static PrefabGUID GetItemFromRecipePrefab(Entity recipePrefab)
    {
        if (recipePrefab.Exists() && recipePrefab.Has<RecipeData>())
        {
            var outputBuffer = recipePrefab.ReadBuffer<RecipeOutputBuffer>();

            return outputBuffer[0].Guid;
        }

        return PrefabGUID.Empty;
    }
    static void ValidateCraftingJob(Entity craftingStation, PrefabGUID itemPrefabGUID, ulong steamId)
    {
        if (_playerCraftingJobs.TryGetValue(steamId, out var stationJobs) && stationJobs.TryGetValue(craftingStation, out var craftingJobs) && craftingJobs.ContainsKey(itemPrefabGUID))
        {
            if (craftingJobs[itemPrefabGUID] > 0)
            {
                if (!ValidatedCraftingJobs.ContainsKey(steamId))
                {
                    ValidatedCraftingJobs[steamId] = [];
                }

                if (!ValidatedCraftingJobs[steamId].ContainsKey(craftingStation))
                {
                    ValidatedCraftingJobs[steamId].Add(craftingStation, []);
                }

                Dictionary<PrefabGUID, int> validatedCraftingJobs = ValidatedCraftingJobs[steamId][craftingStation];

                if (!validatedCraftingJobs.ContainsKey(itemPrefabGUID))
                {
                    validatedCraftingJobs[itemPrefabGUID] = 1;
                }
                else
                {
                    validatedCraftingJobs[itemPrefabGUID]++;
                }

                ValidatedCraftingJobs[steamId][craftingStation] = validatedCraftingJobs;

                int jobs = craftingJobs[itemPrefabGUID];
                craftingJobs[itemPrefabGUID] = --jobs;

                //Core.Log.LogInfo($"Crafting job handled via CraftValidation for {itemPrefabGUID.LookupName()}| {craftingJobs[itemPrefabGUID]}");

                if (craftingJobs[itemPrefabGUID] <= 0) craftingJobs.Remove(itemPrefabGUID);
            }
            else if (craftingJobs[itemPrefabGUID] <= 0) craftingJobs.Remove(itemPrefabGUID);
        }
    }
}