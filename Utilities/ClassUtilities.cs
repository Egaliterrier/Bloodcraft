﻿using Bloodcraft.Services;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;
using VampireCommandFramework;
using static Bloodcraft.Systems.Leveling.LevelingSystem;

namespace Bloodcraft.Utilities;

internal static class ClassUtilities
{
    static EntityManager EntityManager => Core.EntityManager;
    static ServerGameManager ServerGameManager => Core.ServerGameManager;
    static SystemService SystemService => Core.SystemService;
    static EntityCommandBufferSystem EntityCommandBufferSystem => SystemService.EntityCommandBufferSystem;

    static readonly ComponentType[] JewelComponents =
    [
        ComponentType.ReadOnly(Il2CppType.Of<JewelInstance>()),
        ComponentType.ReadOnly(Il2CppType.Of<JewelLevelSource>())
    ];

    //static EntityQuery JewelQuery;
    public static List<int> GetClassBuffs(ulong steamId)
    {
        if (steamId.TryGetPlayerClasses(out var classes) && classes.Keys.Count > 0)
        {
            var playerClass = classes.Keys.FirstOrDefault();
            return ConfigUtilities.ParseConfigString(ClassPrestigeBuffsMap[playerClass]);
        }
        return [];
    }
    public static PlayerClasses GetPlayerClass(ulong steamId)
    {
        if (steamId.TryGetPlayerClasses(out var classes))
        {
            return classes.First().Key;
        }
        throw new Exception("Player does not have a class.");
    }
    public static bool HandleClassChangeItem(ChatCommandContext ctx, ulong steamId)
    {
        PrefabGUID item = new(ConfigService.ChangeClassItem);
        int quantity = ConfigService.ChangeClassQuantity;

        if (!InventoryUtilities.TryGetInventoryEntity(EntityManager, ctx.User.LocalCharacter._Entity, out var inventoryEntity) ||
            ServerGameManager.GetInventoryItemCount(inventoryEntity, item) < quantity)
        {
            LocalizationService.HandleReply(ctx, $"You do not have the required item to change classes ({item.GetPrefabName()}x{quantity})");
            return false;
        }

        if (!ServerGameManager.TryRemoveInventoryItem(inventoryEntity, item, quantity))
        {
            LocalizationService.HandleReply(ctx, $"Failed to remove the required item ({item.GetPrefabName()}x{quantity})");
            return false;
        }

        RemoveClassBuffs(ctx, steamId);
        return true;
    }
    public static bool HasClass(ulong steamId)
    {
        return steamId.TryGetPlayerClasses(out var classes) && classes.Keys.Count > 0;
    }
    public static void RemoveClassBuffs(ChatCommandContext ctx, ulong steamId)
    {
        List<int> buffs = GetClassBuffs(steamId);
        var buffSpawner = BuffUtility.BuffSpawner.Create(ServerGameManager);
        var entityCommandBuffer = EntityCommandBufferSystem.CreateCommandBuffer();

        if (buffs.Count == 0) return;

        for (int i = 0; i < buffs.Count; i++)
        {
            if (buffs[i] == 0) continue;
            PrefabGUID buffPrefab = new(buffs[i]);
            if (ServerGameManager.HasBuff(ctx.Event.SenderCharacterEntity, buffPrefab.ToIdentifier()))
            {
                BuffUtility.TryRemoveBuff(ref buffSpawner, entityCommandBuffer, buffPrefab.ToIdentifier(), ctx.Event.SenderCharacterEntity);
            }
        }
    }
    public static void ReplyClassBuffs(ChatCommandContext ctx, PlayerClasses playerClass)
    {
        List<int> perks = ConfigUtilities.ParseConfigString(ClassPrestigeBuffsMap[playerClass]);

        if (perks.Count == 0)
        {
            LocalizationService.HandleReply(ctx, $"{playerClass} buffs not found.");
            return;
        }

        int step = ConfigService.MaxLevel / perks.Count;

        var classBuffs = perks.Select((perk, index) =>
        {
            int level = (index + 1) * step;
            string prefab = new PrefabGUID(perk).LookupName();
            int prefabIndex = prefab.IndexOf("Prefab");
            if (prefabIndex != -1)
            {
                prefab = prefab[..prefabIndex].TrimEnd();
            }
            return $"<color=white>{prefab}</color> at level <color=yellow>{level}</color>";
        }).ToList();

        for (int i = 0; i < classBuffs.Count; i += 6)
        {
            var batch = classBuffs.Skip(i).Take(6);
            string replyMessage = string.Join(", ", batch);
            LocalizationService.HandleReply(ctx, $"{playerClass} buffs: {replyMessage}");
        }
    }
    public static void ReplyClassSpells(ChatCommandContext ctx, PlayerClasses playerClass)
    {
        List<int> perks = ConfigUtilities.ParseConfigString(ClassSpellsMap[playerClass]);

        if (perks.Count == 0)
        {
            LocalizationService.HandleReply(ctx, $"{playerClass} spells not found.");
            return;
        }

        var classSpells = perks.Select(perk =>
        {
            string prefab = new PrefabGUID(perk).LookupName();
            int prefabIndex = prefab.IndexOf("Prefab");
            if (prefabIndex != -1)
            {
                prefab = prefab[..prefabIndex].TrimEnd();
            }
            return $"<color=white>{prefab}</color>";
        }).ToList();

        for (int i = 0; i < classSpells.Count; i += 6)
        {
            var batch = classSpells.Skip(i).Take(6);
            string replyMessage = string.Join(", ", batch);
            LocalizationService.HandleReply(ctx, $"{playerClass} spells: {replyMessage}");
        }
    }
    public static bool TryParseClass(string classType, out PlayerClasses parsedClassType)
    {
        if (Enum.TryParse(classType, true, out parsedClassType))
        {
            return true;
        }

        parsedClassType = Enum.GetValues(typeof(PlayerClasses))
                              .Cast<PlayerClasses>()
                              .FirstOrDefault(pc => pc.ToString().Contains(classType, StringComparison.OrdinalIgnoreCase));

        if (!parsedClassType.Equals(default(PlayerClasses)))
        {
            return true;
        }

        parsedClassType = default;
        return false;
    }
    public static void UpdateClassData(Entity character, PlayerClasses parsedClassType, Dictionary<PlayerClasses, (List<int>, List<int>)> classes, ulong steamId)
    {
        var weaponConfigEntry = ClassWeaponBloodMap[parsedClassType].Item1;
        var bloodConfigEntry = ClassWeaponBloodMap[parsedClassType].Item2;
        var classWeaponStats = ConfigUtilities.ParseConfigString(weaponConfigEntry);
        var classBloodStats = ConfigUtilities.ParseConfigString(bloodConfigEntry);

        classes[parsedClassType] = (classWeaponStats, classBloodStats);
        steamId.SetPlayerClasses(classes);

        FromCharacter fromCharacter = new()
        {
            Character = character,
            User = character.Read<PlayerCharacter>().UserEntity,
        };

        BuffUtilities.ApplyClassBuffs(character, steamId, fromCharacter);
    }
    /*
    public static void GenerateAbilityJewelMap()
    {
        JewelQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = JewelComponents,
            Options = EntityQueryOptions.IncludeAll
        });

        try
        {
            IEnumerable<Entity> jewelEntities = EntityUtilities.GetEntitiesEnumerable(JewelQuery);
            foreach (Entity entity in jewelEntities)
            {
                if (!entity.TryGetComponent(out PrefabGUID prefab)) continue;
                else if (entity.TryGetComponent(out JewelInstance jewelInstance) && jewelInstance.OverrideAbilityType.HasValue())
                {
                    if (!AbilityJewelMap.ContainsKey(jewelInstance.OverrideAbilityType))
                    {
                        AbilityJewelMap.Add(jewelInstance.OverrideAbilityType, []);
                    }

                    string prefabName = entity.Read<PrefabGUID>().LookupName().Split(" ", 2)[0];
                    if (prefabName.EndsWith("T01") || prefabName.EndsWith("T02") || prefabName.EndsWith("T03") || prefabName.EndsWith("T04")) continue;
                    else AbilityJewelMap[jewelInstance.OverrideAbilityType].Add(entity);
                }
            }

            foreach(var kvp in AbilityJewelMap)
            {
                //Core.Log.LogInfo($"Ability {kvp.Key.LookupName()} has {kvp.Value.Count} jewel(s): {string.Join(", ", kvp.Value.Select(e => e.Read<PrefabGUID>().LookupName()))}");
            }
        }
        finally
        {
            JewelQuery.Dispose();
        }
    }
    */
}
