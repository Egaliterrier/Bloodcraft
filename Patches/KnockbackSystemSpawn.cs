﻿using Bloodcraft.Services;
using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class KnockbackSystemSpawnPatch
{
    static EntityManager EntityManager => Core.EntityManager;
    static ServerGameManager ServerGameManger => Core.ServerGameManager;
    static SystemService SystemService => Core.SystemService;

    static readonly GameModeType _gameMode = SystemService.ServerGameSettingsSystem._Settings.GameModeType;

    static readonly bool _familiars = ConfigService.FamiliarSystem;

    static readonly PrefabGUID _pvpProtectionBuff = new(1111481396);
    static readonly PrefabGUID _allyKnockbackBuff = new(-2099203048);

    // KnockbackSystem
    // KnockbackEvent

    [HarmonyPatch(typeof(KnockbackSystemSpawn), nameof(KnockbackSystemSpawn.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(KnockbackSystemSpawn __instance)
    {
        if (!Core._initialized) return;
        else if (!_familiars) return;
        
        NativeArray<Entity> entities = __instance.__query_1729431709_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (!entity.TryGetComponent(out EntityOwner entityOwner) || !entityOwner.Owner.Exists() || !entity.TryGetComponent(out PrefabGUID prefabGUID)) continue;
                else if (prefabGUID == _allyKnockbackBuff) continue;

                Entity buffTarget = entity.GetBuffTarget();
                Entity owner = entityOwner.Owner;

                // Core.Log.LogInfo($"KnockbackSystemSpawn - {owner.GetPrefabGuid().GetPrefabName()} | {entity.GetPrefabGuid().GetPrefabName()}"); // octavian stuff does go through here but should be already handled if that was all there was to it?
                // maybe try destroying with main EntityManager destroyEntity?

                if (owner.IsFollowingPlayer() && buffTarget.TryGetPlayer(out Entity player))
                {
                    if (ServerGameManger.IsAllies(buffTarget, owner))
                    {
                        PreventKnockback(entity);
                    }
                    else if (_gameMode.Equals(GameModeType.PvE))
                    {
                        PreventKnockback(entity);
                    }
                    else if (player.HasBuff(_pvpProtectionBuff))
                    {
                        PreventKnockback(entity);
                    }
                }
                else if (owner.IsPlayer() && !owner.Equals(buffTarget) && buffTarget.TryGetPlayer(out player))
                {
                    if (_gameMode.Equals(GameModeType.PvP) && player.HasBuff(_pvpProtectionBuff))
                    {
                        PreventKnockback(entity);
                    }
                    else if (_gameMode.Equals(GameModeType.PvE))
                    {
                        PreventKnockback(entity);
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
    static void PreventKnockback(Entity knockbackBuff)
    {
        if (knockbackBuff.TryGetComponent(out Buff buff) && buff.BuffEffectType.Equals(BuffEffectType.Debuff))
        {
            knockbackBuff.Destroy();
        }
    }
}
