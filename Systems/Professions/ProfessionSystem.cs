﻿using Bloodcraft.Services;
using Bloodcraft.Utilities;
using ProjectM;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Bloodcraft.Utilities.Progression;
using Random = System.Random;
using User = ProjectM.Network.User;

namespace Bloodcraft.Systems.Professions;
internal static class ProfessionSystem
{
    static EntityManager EntityManager => Core.EntityManager;
    static ServerGameManager ServerGameManager => Core.ServerGameManager;
    static SystemService SystemService => Core.SystemService;
    static PrefabCollectionSystem PrefabCollectionSystem => SystemService.PrefabCollectionSystem;
    static EndSimulationEntityCommandBufferSystem EndSimulationEntityCommandBufferSystem => SystemService.EndSimulationEntityCommandBufferSystem;

    static readonly Random _random = new();

    static readonly WaitForSeconds _sctDelay = new(0.75f);

    static readonly float _professionMultiplier = ConfigService.ProfessionMultiplier;
    static readonly int _maxProfessionLevel = ConfigService.MaxProfessionLevel;

    static readonly AssetGuid _experienceAssetGuid = AssetGuid.FromString("4210316d-23d4-4274-96f5-d6f0944bd0bb");
    static readonly AssetGuid _yieldAssetGuid = AssetGuid.FromString("5a8b7a32-c3e3-4794-bd62-ace36c10e89e");

    static readonly PrefabGUID _resourceGainSCT = new(1876501183); // SCT resource gain prefabguid
    static readonly float3 _bonusYieldColor = new(0.6f, 0.8f, 1.0f);
    public static void UpdateProfessions(Entity playerCharacter, Entity target)
    {
        Entity userEntity = playerCharacter.GetUserEntity();
        User user = userEntity.GetUser();

        ulong steamId = user.PlatformId;

        PrefabGUID itemPrefabGuid = PrefabGUID.Empty;
        if (target.Has<YieldResourcesOnDamageTaken>() && target.Has<EntityCategory>())
        {
            var yield = target.ReadBuffer<YieldResourcesOnDamageTaken>();

            if (yield.IsCreated && !yield.IsEmpty)
            {
                itemPrefabGuid = yield[0].ItemType;
            }
        }
        else
        {
            return;
        }

        float professionValue = target.TryGetComponent(out EntityCategory entityCategory) ? entityCategory.ResourceLevel._Value : 0f;
        PrefabGUID targetPrefabGuid = target.GetPrefabGuid();

        if (!PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(itemPrefabGuid, out Entity prefabEntity)) return;

        if (prefabEntity.TryGetComponent(out entityCategory) && entityCategory.ResourceLevel._Value > professionValue)
        {
            professionValue = prefabEntity.Read<EntityCategory>().ResourceLevel._Value;
        }

        if (target.GetUnitLevel() > professionValue && !targetPrefabGuid.GetPrefabName().Contains("iron", StringComparison.OrdinalIgnoreCase))
        {
            professionValue = target.Read<UnitLevel>().Level;
        }

        if (professionValue.Equals(0))
        {
            professionValue = 10;
        }

        professionValue = (int)(professionValue * _professionMultiplier);
        IProfessionHandler handler = ProfessionHandlerFactory.GetProfessionHandler(itemPrefabGuid);

        if (handler != null)
        {
            if (handler.GetProfessionName().Contains("Woodcutting"))
            {
                professionValue *= ProfessionMappings.GetWoodcuttingModifier(itemPrefabGuid);
            }

            SetProfession(target, playerCharacter, steamId, professionValue, handler);
            GiveProfessionBonus(target, targetPrefabGuid, playerCharacter, userEntity, user, steamId, handler);
        }
    }
    public static void GiveProfessionBonus(Entity target, PrefabGUID prefabGuid, Entity playerCharacter, Entity userEntity, User user, ulong steamId, IProfessionHandler handler)
    {
        if (!PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(prefabGuid, out Entity prefabEntity)) return;

        int level = GetLevel(steamId, handler);
        string prefabName = handler.GetProfessionName();

        if (prefabName.Contains("Fishing"))
        {
            List<PrefabGUID> fishDrops = ProfessionMappings.GetFishingAreaDrops(prefabGuid);
            int bonusYield = level / 20;

            if (bonusYield.Equals(0)) return;

            int index = _random.Next(fishDrops.Count);
            PrefabGUID fish = fishDrops[index];

            if (ServerGameManager.TryAddInventoryItem(playerCharacter, fish, bonusYield))
            {
                if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ProfessionLogging")) LocalizationService.HandleServerReply(EntityManager, user, $"Bonus <color=green>{fishDrops[index].GetLocalizedName()}</color>x<color=white>{bonusYield}</color> received from {handler.GetProfessionName()}");
                if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ScrollingText"))
                {
                    float3 position = target.GetPosition();

                    ScrollingCombatTextMessage.Create(EntityManager, EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
                        _yieldAssetGuid, position, _bonusYieldColor, playerCharacter, bonusYield, _resourceGainSCT, userEntity);
                }
            }
            else
            {
                InventoryUtilitiesServer.CreateDropItem(EntityManager, playerCharacter, fish, bonusYield, new Entity());
                if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ProfessionLogging")) LocalizationService.HandleServerReply(EntityManager, user, $"Bonus <color=green>{fishDrops[index].GetLocalizedName()}</color>x<color=white>{bonusYield}</color> received from {handler.GetProfessionName()}, but it dropped on the ground since your inventory was full.");
                if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ScrollingText"))
                {
                    float3 position = target.GetPosition();

                    ScrollingCombatTextMessage.Create(EntityManager, EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
                        _yieldAssetGuid, position, _bonusYieldColor, playerCharacter, bonusYield, _resourceGainSCT, userEntity);
                }
            }
        }
        else if (prefabEntity.Has<DropTableBuffer>())
        {
            var dropTableBuffer = prefabEntity.ReadBuffer<DropTableBuffer>();

            foreach (var drop in dropTableBuffer)
            {
                switch (drop.DropTrigger)
                {
                    case DropTriggerType.YieldResourceOnDamageTaken:
                        Entity dropTable = PrefabCollectionSystem._PrefabGuidToEntityMap[drop.DropTableGuid];
                        var dropTableDataBuffer = dropTable.ReadBuffer<DropTableDataBuffer>();
                        foreach (var dropTableData in dropTableDataBuffer)
                        {
                            string itemName = dropTableData.ItemGuid.GetPrefabName();
                            // string localizedItemName = dropTableData.ItemGuid.GetLocalizedName();

                            if (itemName.Contains("ingredient", StringComparison.OrdinalIgnoreCase) || itemName.Contains("trippyshroom", StringComparison.OrdinalIgnoreCase))
                            {
                                int bonusYield = 0;

                                if (itemName.Contains("plant", StringComparison.OrdinalIgnoreCase) || itemName.Contains("trippyshroom", StringComparison.OrdinalIgnoreCase))
                                {
                                    bonusYield = level / 10;
                                }
                                else
                                {
                                    bonusYield = level / 2;
                                }

                                if (bonusYield <= 0) return;
                                else if (ServerGameManager.TryAddInventoryItem(playerCharacter, dropTableData.ItemGuid, bonusYield))
                                {
                                    if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ProfessionLogging")) LocalizationService.HandleServerReply(EntityManager, user, $"Bonus <color=green>{dropTableData.ItemGuid.GetLocalizedName()}</color>x<color=white>{bonusYield}</color> received from {handler.GetProfessionName()}");
                                    if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ScrollingText"))
                                    {
                                        float3 targetPosition = target.GetPosition();
                                        // float3 sctPosition = new(targetPosition.x + 5f, targetPosition.y, targetPosition.z);

                                        ScrollingCombatTextMessage.Create(EntityManager, EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(), 
                                            _yieldAssetGuid, targetPosition, _bonusYieldColor, playerCharacter, bonusYield, _resourceGainSCT, userEntity);
                                    }

                                    break;
                                }
                                else
                                {
                                    InventoryUtilitiesServer.CreateDropItem(EntityManager, playerCharacter, dropTableData.ItemGuid, bonusYield, new Entity());
                                    
                                    if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ProfessionLogging")) LocalizationService.HandleServerReply(EntityManager, user, $"Bonus <color=green>{dropTableData.ItemGuid.GetLocalizedName()}</color>x<color=white>{bonusYield}</color> received from {handler.GetProfessionName()}, but it dropped on the ground since your inventory was full.");
                                    if (Misc.PlayerBoolsManager.GetPlayerBool(steamId, "ScrollingText"))
                                    {
                                        float3 targetPosition = target.GetPosition();
                                        // float3 sctPosition = new(targetPosition.x + 5f, targetPosition.y, targetPosition.z);

                                        ScrollingCombatTextMessage.Create(EntityManager, EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
                                            _yieldAssetGuid, targetPosition, _bonusYieldColor, playerCharacter, bonusYield, _resourceGainSCT, userEntity);
                                    }

                                    break;
                                }
                            }
                        }
                        break;
                    /*
                    case DropTriggerType.OnDeath:
                        WIP
                        dropTable = prefabCollectionSystem._PrefabGuidToEntityMap[drop.DropTableGuid];
                        dropTableDataBuffer = dropTable.ReadBuffer<DropTableDataBuffer>();
                        foreach (var dropTableData in dropTableDataBuffer)
                        {
                            prefabEntity = prefabCollectionSystem._PrefabGuidToEntityMap[dropTableData.ItemGuid];
                            if (!prefabEntity.Has<ItemDataDropGroupBuffer>()) continue;
                            var itemDataDropGroupBuffer = prefabEntity.ReadBuffer<ItemDataDropGroupBuffer>();
                            foreach (var itemDataDropGroup in itemDataDropGroupBuffer)
                            {
                                Core.Log.LogInfo($"{itemDataDropGroup.DropItemPrefab.GetPrefabName()} | {itemDataDropGroup.Quantity} | {itemDataDropGroup.Weight}");
                            }
                        }
                        break;
                    */
                    default:
                        break;
                }
            }
        }
    }
    public static void SetProfession(Entity target, Entity source, ulong steamID, float value, IProfessionHandler handler)
    {
        var xpData = handler.GetProfessionData(steamID);

        if (xpData.Key >= _maxProfessionLevel) return;

        UpdateProfessionExperience(target, source, steamID, xpData, value, handler);
    }
    static void UpdateProfessionExperience(Entity target, Entity source, ulong steamID, KeyValuePair<int, float> xpData, float gainedXP, IProfessionHandler handler)
    {
		gainedXP *= ProfessionMappings.GetProfessionFactor(handler.GetProfessionName());
        float newExperience = xpData.Value + gainedXP;
        int newLevel = ConvertXpToLevel(newExperience);
        bool leveledUp = false;

        if (newLevel > xpData.Key)
        {
            leveledUp = true;
            if (newLevel > _maxProfessionLevel)
            {
                newLevel = _maxProfessionLevel;
                newExperience = ConvertLevelToXp(_maxProfessionLevel);
            }
        }

        var updatedXPData = new KeyValuePair<int, float>(newLevel, newExperience);
        handler.SetProfessionData(steamID, updatedXPData);

        NotifyPlayer(target, source, steamID, gainedXP, leveledUp, handler);
    }
    static void NotifyPlayer(Entity target, Entity source, ulong steamID, float gainedXP, bool leveledUp, IProfessionHandler handler)
    {
        Entity userEntity = source.Read<PlayerCharacter>().UserEntity;
        User user = userEntity.Read<User>();

        string professionName = handler.GetProfessionName();

        if (leveledUp)
        {
            int newLevel = ConvertXpToLevel(handler.GetProfessionData(steamID).Value);
            if (newLevel < _maxProfessionLevel) LocalizationService.HandleServerReply(EntityManager, user, $"{professionName} improved to [<color=white>{newLevel}</color>]");
        }

        if (Misc.PlayerBoolsManager.GetPlayerBool(steamID, "ProfessionLogging"))
        {
            int levelProgress = GetLevelProgress(steamID, handler);
            LocalizationService.HandleServerReply(EntityManager, user, $"+<color=yellow>{(int)gainedXP}</color> <color=#FFC0CB>proficiency</color> in {professionName.ToLower()} (<color=white>{levelProgress}%</color>)");
        }

        if (Misc.PlayerBoolsManager.GetPlayerBool(steamID, "ScrollingText"))
        {
            float3 targetPosition = target.GetPosition();
            float3 professionColor = handler.GetProfessionColor();

            ProfessionSCTDelayRoutine(user.LocalCharacter.GetEntityOnServer(), userEntity, targetPosition, professionColor, gainedXP).Start();
        }
    }
    static IEnumerator ProfessionSCTDelayRoutine(Entity character, Entity userEntity, float3 position, float3 color, float gainedXP)
    {
        yield return _sctDelay;
        
        ScrollingCombatTextMessage.Create(EntityManager, EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(), _experienceAssetGuid, position, color, character, gainedXP, _resourceGainSCT, userEntity);
    }
    static float GetXp(ulong steamID, IProfessionHandler handler)
    {
        var xpData = handler.GetProfessionData(steamID);
        return xpData.Value;
    }
    static int GetLevel(ulong steamID, IProfessionHandler handler)
    {
        return ConvertXpToLevel(GetXp(steamID, handler));
    }
    public static int GetLevelProgress(ulong steamID, IProfessionHandler handler)
    {
        float currentXP = GetXp(steamID, handler);
        int currentLevelXP = ConvertLevelToXp(GetLevel(steamID, handler));
        int nextLevelXP = ConvertLevelToXp(GetLevel(steamID, handler) + 1);

        double neededXP = nextLevelXP - currentLevelXP;
        double earnedXP = nextLevelXP - currentXP;
        return 100 - (int)Math.Ceiling(earnedXP / neededXP * 100);
    }
}
internal static class ProfessionMappings
{
	
	static readonly Dictionary<string, float> _professionFactors = new()
    {
        { "woodcutting", ConfigService.WoodcuttingFactor },
        { "mining", ConfigService.MiningFactor },
        { "blacksmithing", ConfigService.BlacksmithingFactor },
        { "tailoring", ConfigService.TailoringFactor },
        { "fishing", ConfigService.FishingFactor },
        { "alchemy", ConfigService.AlchemyFactor },
        { "harvesting", ConfigService.HarvestingFactor },
        { "enchanting", ConfigService.EnchantingFactor }
    };
	
    static readonly Dictionary<string, int> _fishingMultipliers = new()
    {
        { "farbane", 1 },
        { "dunley", 2 },
        { "gloomrot", 3 },
        { "cursed", 4 },
        { "silverlight", 4 }
    };

    static readonly List<PrefabGUID> _farbaneFishDrops = new()
    {
        { new(-1642545082)} //goby
    };

    static readonly List<PrefabGUID> _dunleyFishDrops = new()
    {
        { new(-1642545082) }, //goby
        { new(447901086) }, //stinger
        { new(-149778795) } //rainbow
    };

    static readonly List<PrefabGUID> _gloomrotFishDrops = new()
    {
        { new(-1642545082) }, //goby
        { new(447901086) }, //stinger
        { new(-149778795) }, //rainbow
        { new(736318803) }, //sagefish
        { new(-1779269313) } //bloodsnapper
    };

    static readonly List<PrefabGUID> _cursedFishDrops = new()
    {
        { new(-1642545082) }, //goby
        { new(447901086) }, //stinger
        { new(-149778795) }, //rainbow
        { new(736318803) }, //sagefish
        { new(-1779269313) }, //bloodsnapper
        { new(177845365) } //swampdweller
    };

    static readonly List<PrefabGUID> _silverlightFishDrops = new()
    {
        { new(-1642545082) }, //goby
        { new(447901086) }, //stinger
        { new(-149778795) }, //rainbow
        { new(736318803) }, //sagefish
        { new(-1779269313) }, //bloodsnapper
        { new(67930804) } //goldenbassriver
    };

    static readonly Dictionary<string, List<PrefabGUID>> _fishingAreaDrops = new()
    {
        { "farbane", _farbaneFishDrops},
        { "dunley", _dunleyFishDrops},
        { "gloomrot", _gloomrotFishDrops},
        { "cursed", _cursedFishDrops},
        { "silverlight", _silverlightFishDrops}
    };

    static readonly Dictionary<string, int> _woodcuttingMultipliers = new()
    {
        { "hallow", 2 },
        { "gloom", 3 },
        { "cursed", 4 }
    };

    static readonly Dictionary<string, int> _tierMultiplier = new()
    {
        { "t01", 1 },
        { "t02", 2 },
        { "t03", 3 },
        { "t04", 4 },
        { "t05", 5 },
        { "t06", 6 },
        { "t07", 7 },
        { "t08", 8 },
        { "t09", 9 },
    };
    public static int GetFishingModifier(PrefabGUID prefab)
    {
        foreach (KeyValuePair<string, int> location in _fishingMultipliers)
        {
            if (prefab.GetPrefabName().ToLower().Contains(location.Key))
            {
                return location.Value;
            }
        }
        return 1;
    }
	
	public static float GetProfessionFactor(string name)
    {
        foreach (KeyValuePair<string, float> profession in _professionFactors)
        {
            if (name.ToLower().Contains(profession.Key))
            {
                return profession.Value;
            }
        }
        return 1;
    }
	
    public static List<PrefabGUID> GetFishingAreaDrops(PrefabGUID prefab)
    {
        foreach (KeyValuePair<string, List<PrefabGUID>> location in _fishingAreaDrops)
        {
            if (prefab.GetPrefabName().ToLower().Contains(location.Key))
            {
                return location.Value;
            }
            else if (prefab.GetPrefabName().ToLower().Contains("general"))
            {
                return _farbaneFishDrops;
            }
        }
        throw new InvalidOperationException("Unrecognized fishing area");
    }
    public static int GetWoodcuttingModifier(PrefabGUID prefab)
    {
        foreach (KeyValuePair<string, int> location in _woodcuttingMultipliers)
        {
            if (prefab.GetPrefabName().ToLower().Contains(location.Key))
            {
                return location.Value;
            }
        }

        return 1;
    }
    public static int GetTierMultiplier(PrefabGUID prefab)
    {
        foreach (KeyValuePair<string, int> tier in _tierMultiplier)
        {
            if (prefab.GetPrefabName().ToLower().Contains(tier.Key))
            {
                return tier.Value;
            }
        }

        return 1;
    }
}