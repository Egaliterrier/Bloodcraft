﻿using Bloodcraft.Services;
using Bloodcraft.Systems.Leveling;
using Bloodcraft.Utilities;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Entities;
using static Bloodcraft.Patches.DeathEventListenerSystemPatch;
using static Bloodcraft.Utilities.Progression;

namespace Bloodcraft.Systems.Expertise;
internal static class WeaponSystem
{
    static EntityManager EntityManager => Core.EntityManager;

    static readonly int MaxExpertiseLevel = ConfigService.MaxExpertiseLevel;
    static readonly int ExpertiseStatChoices = ConfigService.ExpertiseStatChoices;
    static readonly float UnitExpertiseMultiplier = ConfigService.UnitExpertiseMultiplier;
    static readonly float VBloodExpertiseMultiplier = ConfigService.VBloodExpertiseMultiplier;
    static readonly float PrestigeRatesReducer = ConfigService.PrestigeRatesReducer;
    static readonly float PrestigeRateMultiplier = ConfigService.PrestigeRateMultiplier;
    static readonly float UnitSpawnerExpertiseFactor = ConfigService.UnitSpawnerExpertiseFactor;

    public static readonly Dictionary<WeaponType, Func<ulong, (bool Success, KeyValuePair<int, float> Data)>> TryGetExtensionMap = new()
    {
        { WeaponType.Sword, steamID =>
            {
                if (steamID.TryGetPlayerSwordExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Axe, steamID =>
            {
                if (steamID.TryGetPlayerAxeExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Mace, steamID =>
            {
                if (steamID.TryGetPlayerMaceExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Spear, steamID =>
            {
                if (steamID.TryGetPlayerSpearExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Crossbow, steamID =>
            {
                if (steamID.TryGetPlayerCrossbowExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.GreatSword, steamID =>
            {
                if (steamID.TryGetPlayerGreatSwordExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Slashers, steamID =>
            {
                if (steamID.TryGetPlayerSlashersExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Pistols, steamID =>
            {
                if (steamID.TryGetPlayerPistolsExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Reaper, steamID =>
            {
                if (steamID.TryGetPlayerReaperExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Longbow, steamID =>
            {
                if (steamID.TryGetPlayerLongbowExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Whip, steamID =>
            {
                if (steamID.TryGetPlayerWhipExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.Unarmed, steamID =>
            {
                if (steamID.TryGetPlayerUnarmedExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        },
        { WeaponType.FishingPole, steamID =>
            {
                if (steamID.TryGetPlayerFishingPoleExpertise(out var data))
                {
                    return (true, data);
                }
                return (false, default);
            }
        }
    };
    public static readonly Dictionary<WeaponType, Action<ulong, KeyValuePair<int, float>>> SetExtensionMap = new()
    {
        { WeaponType.Sword, (steamID, data) => steamID.SetPlayerSwordExpertise(data) },
        { WeaponType.Axe, (steamID, data) => steamID.SetPlayerAxeExpertise(data) },
        { WeaponType.Mace, (steamID, data) => steamID.SetPlayerMaceExpertise(data) },
        { WeaponType.Spear, (steamID, data) => steamID.SetPlayerSpearExpertise(data) },
        { WeaponType.Crossbow, (steamID, data) => steamID.SetPlayerCrossbowExpertise(data) },
        { WeaponType.GreatSword, (steamID, data) => steamID.SetPlayerGreatSwordExpertise(data) },
        { WeaponType.Slashers, (steamID, data) => steamID.SetPlayerSlashersExpertise(data) },
        { WeaponType.Pistols, (steamID, data) => steamID.SetPlayerPistolsExpertise(data) },
        { WeaponType.Reaper, (steamID, data) => steamID.SetPlayerReaperExpertise(data) },
        { WeaponType.Longbow, (steamID, data) => steamID.SetPlayerLongbowExpertise(data) },
        { WeaponType.Whip, (steamID, data) => steamID.SetPlayerWhipExpertise(data) },
        { WeaponType.Unarmed, (steamID, data) => steamID.SetPlayerUnarmedExpertise(data) },
        { WeaponType.FishingPole, (steamID, data) => steamID.SetPlayerFishingPoleExpertise(data) }
    };
    public static readonly Dictionary<WeaponType, PrestigeType> WeaponPrestigeMap = new()
    {
        { WeaponType.Sword, PrestigeType.SwordExpertise },
        { WeaponType.Axe, PrestigeType.AxeExpertise },
        { WeaponType.Mace, PrestigeType.MaceExpertise },
        { WeaponType.Spear, PrestigeType.SpearExpertise },
        { WeaponType.Crossbow, PrestigeType.CrossbowExpertise },
        { WeaponType.GreatSword, PrestigeType.GreatSwordExpertise },
        { WeaponType.Slashers, PrestigeType.SlashersExpertise },
        { WeaponType.Pistols, PrestigeType.PistolsExpertise },
        { WeaponType.Reaper, PrestigeType.ReaperExpertise },
        { WeaponType.Longbow, PrestigeType.LongbowExpertise },
        { WeaponType.Whip, PrestigeType.WhipExpertise },
        { WeaponType.Unarmed, PrestigeType.UnarmedExpertise },
        { WeaponType.FishingPole, PrestigeType.FishingPoleExpertise }
    };
    public static void OnUpdate(object sender, DeathEventArgs deathEvent)
    {
        ProcessExpertise(deathEvent.Source, deathEvent.Target);
    }
    public static void ProcessExpertise(Entity source, Entity target, float groupMultiplier = 1f)
    {
        if (target.Has<Minion>()) return;

        Entity userEntity = source.Read<PlayerCharacter>().UserEntity;
        User user = userEntity.Read<User>();
        ulong steamID = user.PlatformId;
        WeaponType weaponType = WeaponManager.GetCurrentWeaponType(source);

        if (target.TryGetComponent(out UnitStats unitStats))
        {
            float expertiseValue = CalculateExpertiseValue(unitStats, target.Has<VBloodConsumeSource>());
            float changeFactor = 1f;

            if (UnitSpawnerExpertiseFactor < 1 && target.TryGetComponent(out IsMinion isMinion) && isMinion.Value)
            {
                expertiseValue *= UnitSpawnerExpertiseFactor;
                if (expertiseValue == 0) return;
            }

            if (steamID.TryGetPlayerPrestiges(out var prestiges))
            {
                if (prestiges.TryGetValue(WeaponPrestigeMap[weaponType], out var expertisePrestige))
                {
                    changeFactor -= (PrestigeRatesReducer * expertisePrestige);
                }

                if (prestiges.TryGetValue(PrestigeType.Experience, out var xpPrestige))
                {
                    changeFactor += (PrestigeRateMultiplier * xpPrestige);
                }
            }

            expertiseValue *= changeFactor * groupMultiplier;

            IWeaponHandler handler = ExpertiseHandlerFactory.GetExpertiseHandler(weaponType);
            if (handler != null)
            {
                /*
                // Check if the player leveled up
                var xpData = handler.GetExpertiseData(steamID);

                if (xpData.Key >= MaxExpertiseLevel) return;

                float newExperience = xpData.Value + expertiseValue;
                int newLevel = ConvertXpToLevel(newExperience);
                bool leveledUp = false;

                if (newLevel > xpData.Key)
                {
                    leveledUp = true;
                    if (newLevel > MaxExpertiseLevel)
                    {
                        newLevel = MaxExpertiseLevel;
                        newExperience = ConvertLevelToXp(MaxExpertiseLevel);
                    }
                }

                var updatedXPData = new KeyValuePair<int, float>(newLevel, newExperience);
                handler.SetExpertiseData(steamID, updatedXPData);
                */
                SaveWeaponExperience(steamID, handler, expertiseValue, out bool leveledUp, out int newLevel);
                NotifyPlayer(user, weaponType, expertiseValue, leveledUp, newLevel, handler);
            }
        }
    }
    static float CalculateExpertiseValue(UnitStats unitStats, bool isVBlood)
    {
        float ExpertiseValue = unitStats.SpellPower + unitStats.PhysicalPower;

        if (isVBlood) return ExpertiseValue * VBloodExpertiseMultiplier;
        else return ExpertiseValue * UnitExpertiseMultiplier;
    }
    public static void SaveWeaponExperience(ulong steamID, IWeaponHandler handler, float gainedXP, out bool leveledUp, out int newLevel)
    {
        var xpData = handler.GetExpertiseData(steamID);
        int currentLevel = xpData.Key;
        float currentXP = xpData.Value;

        if (currentLevel >= MaxExpertiseLevel)
        {
            // Already at max level
            leveledUp = false;
            newLevel = currentLevel;
            return;
        }

        float newExperience = currentXP + gainedXP;
        newLevel = ConvertXpToLevel(newExperience);
        leveledUp = false;

        if (newLevel > currentLevel)
        {
            leveledUp = true;
            if (newLevel > MaxExpertiseLevel)
            {
                newLevel = MaxExpertiseLevel;
                newExperience = ConvertLevelToXp(MaxExpertiseLevel);
            }
        }

        handler.SetExpertiseData(steamID, new KeyValuePair<int, float>(newLevel, newExperience));
    }
    public static void NotifyPlayer(User user, WeaponType weaponType, float gainedXP, bool leveledUp, int newLevel, IWeaponHandler handler)
    {
        ulong steamID = user.PlatformId;

        int gainedIntXP = (int)gainedXP;
        int levelProgress = GetLevelProgress(steamID, handler);

        if (leveledUp)
        {
            HandleWeaponLevelUp(user, weaponType, newLevel, steamID);
        }

        if (Misc.GetPlayerBool(steamID, "ExpertiseLogging"))
        {
            LocalizationService.HandleServerReply(EntityManager, user,
                $"+<color=yellow>{gainedIntXP}</color> <color=#c0c0c0>{weaponType.ToString().ToLower()}</color> <color=#FFC0CB>expertise</color> (<color=white>{levelProgress}%</color>)");
        }
    }

    /*
    static void NotifyPlayer(User user, WeaponType weaponType, float gainedXP, bool leveledUp, int newLevel, IWeaponHandler handler)
    {
        ulong steamID = user.PlatformId;
        gainedXP = (int)gainedXP;
        int levelProgress = GetLevelProgress(steamID, handler);

        if (leveledUp)
        {
            if (newLevel <= MaxExpertiseLevel) LocalizationService.HandleServerReply(EntityManager, user, $"<color=#c0c0c0>{weaponType}</color> improved to [<color=white>{newLevel}</color>]");
            if (Misc.GetPlayerBool(steamID, "Reminders"))
            {
                if (steamID.TryGetPlayerWeaponStats(out var weaponStats) && weaponStats.TryGetValue(weaponType, out var Stats))
                {
                    if (Stats.Count < ExpertiseStatChoices)
                    {
                        int choices = ExpertiseStatChoices - Stats.Count;
                        string bonusString = choices > 1 ? "bonuses" : "bonus";

                        LocalizationService.HandleServerReply(EntityManager, user, $"{choices} <color=white>stat</color> <color=#00FFFF>{bonusString}</color> available for <color=#c0c0c0>{weaponType.ToString().ToLower()}</color>; use '<color=white>.wep cst {weaponType} [Stat]</color>' to make your choice and <color=white>'.wep lst'</color> to view expertise stat options. (toggle reminders with <color=white>'.remindme'</color>)");
                    }
                }
            }
        }

        if (Misc.GetPlayerBool(steamID, "ExpertiseLogging"))
        {
            LocalizationService.HandleServerReply(EntityManager, user, $"+<color=yellow>{gainedXP}</color> <color=#c0c0c0>{weaponType.ToString().ToLower()}</color> <color=#FFC0CB>expertise</color> (<color=white>{levelProgress}%</color>)");
        }
    }
    */
    static void HandleWeaponLevelUp(User user, WeaponType weaponType, int newLevel, ulong steamID)
    {
        if (newLevel <= MaxExpertiseLevel)
        {
            LocalizationService.HandleServerReply(EntityManager, user,
                $"<color=#c0c0c0>{weaponType}</color> improved to [<color=white>{newLevel}</color>]");
        }

        if (Misc.GetPlayerBool(steamID, "Reminders"))
        {
            if (steamID.TryGetPlayerWeaponStats(out var weaponStats) && weaponStats.TryGetValue(weaponType, out var stats))
            {
                int currentStatCount = stats.Count;
                if (currentStatCount < ExpertiseStatChoices)
                {
                    int choicesLeft = ExpertiseStatChoices - currentStatCount;
                    string bonusString = choicesLeft > 1 ? "bonuses" : "bonus";

                    LocalizationService.HandleServerReply(EntityManager, user,
                        $"{choicesLeft} <color=white>stat</color> <color=#00FFFF>{bonusString}</color> available for <color=#c0c0c0>{weaponType.ToString().ToLower()}</color>; use '<color=white>.wep cst {weaponType} [Stat]</color>' to choose and '<color=white>.wep lst'</color> to view expertise stat options. (toggle reminders with <color=white>'.remindme'</color>)");
                }
            }
        }
    }
    public static int GetLevelProgress(ulong steamID, IWeaponHandler handler)
    {
        int currentLevel = GetLevel(steamID, handler);
        float currentXP = GetXp(steamID, handler);

        int currentLevelXP = ConvertLevelToXp(currentLevel);
        int nextLevelXP = ConvertLevelToXp(++currentLevel);

        double neededXP = nextLevelXP - currentLevelXP;
        double earnedXP = nextLevelXP - currentXP;

        return 100 - (int)Math.Ceiling(earnedXP / neededXP * 100);
    }
    static float GetXp(ulong steamID, IWeaponHandler handler)
    {
        var xpData = handler.GetExpertiseData(steamID);
        return xpData.Value;
    }
    public static int GetLevel(ulong steamID, IWeaponHandler handler)
    {
        var xpData = handler.GetExpertiseData(steamID);
        return xpData.Key;
    }
    public static WeaponType GetWeaponTypeFromWeaponEntity(Entity weaponEntity)
    {
        if (weaponEntity == Entity.Null) return WeaponType.Unarmed;
        string weaponCheck = weaponEntity.Read<PrefabGUID>().LookupName();

        return Enum.GetValues(typeof(WeaponType))
            .Cast<WeaponType>()
            .FirstOrDefault(type =>
            weaponCheck.Contains(type.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !(type == WeaponType.Sword && weaponCheck.Contains("GreatSword", StringComparison.OrdinalIgnoreCase))
            );
    }
}