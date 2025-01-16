﻿using Bloodcraft.Services;
using Bloodcraft.Utilities;
using HarmonyLib;
using ProjectM;
using ProjectM.Shared.Systems;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class SpawnTransformSystemOnSpawnPatch
{
    static EntityManager EntityManager => Core.EntityManager;
    static SystemService SystemService => Core.SystemService;

    static readonly bool _eliteShardBearers = ConfigService.EliteShardBearers;
    static readonly bool _familiars = ConfigService.FamiliarSystem;

    static readonly int _shardBearerLevel = ConfigService.ShardBearerLevel;

    const int UNIT_TEAM = 2;

    static readonly PrefabGUID _manticore = new(-393555055);
    static readonly PrefabGUID _dracula = new(-327335305);
    static readonly PrefabGUID _monster = new(1233988687);
    static readonly PrefabGUID _solarus = new(-740796338);
    static readonly PrefabGUID _divineAngel = new(-1737346940);
    static readonly PrefabGUID _fallenAngel = new(-76116724);

    static readonly PrefabGUID _manticoreVisual = new(1670636401);
    static readonly PrefabGUID _draculaVisual = new(-1923843097);
    static readonly PrefabGUID _monsterVisual = new(-2067402784);
    static readonly PrefabGUID _solarusVisual = new(178225731);

    public static readonly List<PrefabGUID> UnitPrefabGuidsToModify = [_manticore, _dracula, _monster, _solarus, _divineAngel, _fallenAngel];

    static readonly GameModeType _gameMode = SystemService.ServerGameSettingsSystem.Settings.GameModeType;

    public static readonly HashSet<Entity> FamiliarsToSkip = [];

    [HarmonyPatch(typeof(SpawnTransformSystem_OnSpawn), nameof(SpawnTransformSystem_OnSpawn.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(SpawnTransformSystem_OnSpawn __instance)
    {
        if (!Core._initialized) return;
        else if (!_eliteShardBearers) return;

        NativeArray<Entity> entities = __instance.__query_565030732_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (!entity.TryGetComponent(out PrefabGUID prefabGuid)) continue;
                /*
                else if (FamiliarsToSkip.Contains(entity))
                {
                    FamiliarsToSkip.Remove(entity);

                    continue;
                }
                */
                else if (entity.IsPlayerOwned()) continue;

                /* deprecated but leaving for notes until completely refactored elsewhere
                int level = unitLevel.Level._Value;
                int famKey = prefabGUID.GuidHash;
                bool summon = false;

                if (_familiars && level == 1 && !_prefabGuidsToIgnore.Contains(prefabGUID))
                {
                    ulong matchedKey = PlayerBindingValidation
                        .Where(kv => kv.Value == prefabGUID)
                        .Select(kv => kv.Key) // Cast to nullable to handle "not found" case
                        .FirstOrDefault();

                    if (matchedKey == 0)
                    {
                        ulong steamId = PlayerFamiliarBattleGroups
                            .FirstOrDefault(kvp => kvp.Value.Contains(prefabGUID)).Key;

                        if (PlayerSummoningForBattle.TryGetValue(steamId, out bool isSummoning) && isSummoning)
                        {
                            if (steamId.TryGetPlayerInfo(out PlayerInfo playerInfo))
                            {
                                int factionIndex = 0;

                                if (!PlayerBattleFamiliars.ContainsKey(steamId)) PlayerBattleFamiliars[steamId] = [];

                                PlayerFamiliarBattleGroups[steamId].Remove(prefabGUID);
                                PlayerBattleFamiliars[steamId].Add(entity);

                                if (SetDirectionAndFaction.Contains(steamId))
                                {
                                    if (BattleService.Matchmaker.MatchPairs.TryGetMatch(steamId, out var matchPair))
                                    {
                                        factionIndex = 1;
                                        ulong pairedId = matchPair.Item1 == steamId ? matchPair.Item2 : matchPair.Item1;

                                        int pairedIndex = PlayerBattleFamiliars[pairedId].Count - 1;
                                        Familiars.FaceYourEnemy(entity, PlayerBattleFamiliars[pairedId][pairedIndex]);
                                    }
                                    else
                                    {
                                        Core.Log.LogWarning($"SetDirectionAndFaction contained {steamId} but couldn't find MatchPair for battle!");
                                    }

                                    SetDirectionAndFaction.Remove(steamId);
                                }

                                User user = playerInfo.User;
                                summon = true;

                                if (FamiliarSummonSystem.HandleFamiliarForBattle(playerInfo.CharEntity, entity, factionIndex))
                                {
                                    if (PlayerFamiliarBattleGroups[steamId].Count == 0)
                                    {
                                        PlayerSummoningForBattle[steamId] = false;

                                        if (BattleService.Matchmaker.MatchPairs.TryGetMatch(steamId, out var matchPair))
                                        {
                                            ulong pairedId = matchPair.Item1 == steamId ? matchPair.Item2 : matchPair.Item1;

                                            if (PlayerSummoningForBattle.TryGetValue(pairedId, out bool summoning) && !summoning)
                                            {
                                                Core.StartCoroutine(BattleService.BattleCountdownRoutine((steamId, pairedId)));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    DestroyUtility.Destroy(EntityManager, entity);
                                    LocalizationService.HandleServerReply(EntityManager, user, $"Failed to summon familiar...");

                                    continue;
                                }
                            }
                        }
                    }
                    else if (matchedKey.TryGetPlayerInfo(out PlayerInfo playerInfo))
                    {
                        User user = playerInfo.User;
                        ulong steamId = user.PlatformId;
                        summon = true;

                        PlayerBindingValidation.TryRemove(steamId, out _);

                        if (FamiliarSummonSystem.HandleFamiliar(playerInfo.CharEntity, entity))
                        {
                            // SetPlayerBool(steamId, "Binding", false);

                            string colorCode = "<color=#FF69B4>";
                            FamiliarBuffsData buffsData = FamiliarBuffsManager.LoadFamiliarBuffs(steamId);

                            if (buffsData.FamiliarBuffs.ContainsKey(famKey))
                            {
                                if (FamiliarUnlockSystem.ShinyBuffColorHexMap.TryGetValue(new(buffsData.FamiliarBuffs[famKey].First()), out var hexColor))
                                {
                                    colorCode = $"<color={hexColor}>";
                                }
                            }

                            string message = buffsData.FamiliarBuffs.ContainsKey(famKey) ? $"<color=green>{prefabGUID.GetLocalizedName()}</color>{colorCode}*</color> <color=#00FFFF>bound</color>!" : $"<color=green>{prefabGUID.GetLocalizedName()}</color> <color=#00FFFF>bound</color>!";
                            LocalizationService.HandleServerReply(EntityManager, user, message);
                        }
                        else
                        {
                            DestroyUtility.Destroy(EntityManager, entity);
                            LocalizationService.HandleServerReply(EntityManager, user, $"Failed to summon familiar...");

                            continue;
                        }
                    }

                    if (summon) continue;
                */

                if (UnitPrefabGuidsToModify.Contains(prefabGuid))
                {
                    if (prefabGuid.Equals(_manticore))
                    {
                        HandleManticore(entity);
                    }
                    else if (prefabGuid.Equals(_dracula))
                    {
                        HandleDracula(entity);
                    }
                    else if (prefabGuid.Equals(_monster))
                    {
                        HandleMonster(entity);
                    }
                    else if (prefabGuid.Equals(_solarus))
                    {
                        HandleSolarus(entity);
                    }
                    else if (prefabGuid.Equals(_divineAngel))
                    {
                        HandleAngel(entity);
                    }
                    else if (prefabGuid.Equals(_fallenAngel))
                    {
                        HandleFallenAngel(entity);
                    }
                }

            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"SpawnTransformSystem error: {ex}");
        }
        finally
        {
            entities.Dispose();
        }
    }
    static void HandleManticore(Entity entity)
    {
        entity.Remove<DynamicallyWeakenAttackers>();

        SetLevel(entity);
        SetAttackSpeed(entity);
        SetHealth(entity);
        SetPower(entity);
        SetMoveSpeed(entity, 5f, 6.5f);

        Buffs.HandleShinyBuff(entity, _manticoreVisual);
    }
    static void HandleMonster(Entity entity)
    {
        entity.Remove<DynamicallyWeakenAttackers>();

        SetLevel(entity);
        SetAttackSpeed(entity);
        SetHealth(entity);
        SetPower(entity);
        SetMoveSpeed(entity, 2.5f, 5.5f);

        Buffs.HandleShinyBuff(entity, _monsterVisual);
    }
    static void HandleSolarus(Entity entity)
    {
        entity.Remove<DynamicallyWeakenAttackers>();

        SetLevel(entity);
        SetAttackSpeed(entity);
        SetHealth(entity);
        SetPower(entity);
        SetMoveSpeed(entity, 4f);

        Buffs.HandleShinyBuff(entity, _solarusVisual);
    }
    static void HandleDracula(Entity entity)
    {
        entity.Remove<DynamicallyWeakenAttackers>();

        SetLevel(entity);
        SetAttackSpeed(entity);
        SetHealth(entity);
        SetPower(entity);
        SetMoveSpeed(entity, 2.5f, 3.5f);

        Buffs.HandleShinyBuff(entity, _draculaVisual);
    }
    static void HandleAngel(Entity entity)
    {
        SetAttackSpeed(entity);
        SetHealth(entity);
        SetPower(entity);
        SetMoveSpeed(entity, 5f, 7.5f);

        Buffs.HandleShinyBuff(entity, _solarusVisual);
    }
    static void HandleFallenAngel(Entity entity)
    {
        SetHealth(entity);
    }
    static void SetLevel(Entity entity)
    {
        if (_shardBearerLevel > 0)
        {
            entity.With((ref UnitLevel unitLevel) =>
            {
                unitLevel.Level._Value = _shardBearerLevel;
            });
        }
    }
    static void SetAttackSpeed(Entity entity)
    {
        entity.With((ref AbilityBar_Shared abilityBarShared) =>
        {
            abilityBarShared.AttackSpeed._Value = 2f;
            abilityBarShared.PrimaryAttackSpeed._Value = 2f;
        });
    }
    static void SetHealth(Entity entity)
    {
        entity.With((ref Health health) =>
        {
            health.MaxHealth._Value *= 5;
            health.Value = health.MaxHealth._Value;
        });
    }
    static void SetPower(Entity entity)
    {
        entity.With((ref UnitStats unitStats) =>
        {
            unitStats.PhysicalPower._Value *= 1.5f;
            unitStats.SpellPower._Value *= 1.5f;
        });
    }
    static void SetMoveSpeed(Entity entity, float walk = float.NaN, float run = float.NaN)
    {
        entity.With((ref AiMoveSpeeds aiMoveSpeeds) =>
        {
            if (!float.IsNaN(walk)) aiMoveSpeeds.Walk._Value = walk;
            if (!float.IsNaN(run)) aiMoveSpeeds.Run._Value = run;
        });
    }
}
