﻿using Bloodcraft.Services;
using Bloodcraft.Systems.Leveling;
using Bloodcraft.Utilities;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using VampireCommandFramework;
using static Bloodcraft.Utilities.Classes;
using static Bloodcraft.Utilities.Misc.PlayerBoolsManager;

namespace Bloodcraft.Commands;

[CommandGroup(name: "class")]
internal static class ClassCommands
{
    static EntityManager EntityManager => Core.EntityManager;

    static readonly bool _classes = ConfigService.SoftSynergies || ConfigService.HardSynergies;

    [Command(name: "choose", shortHand: "c", adminOnly: false, usage: ".class c [Class]", description: "Choose class.")]
    public static void ClassChoiceCommand(ChatCommandContext ctx, string className)
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        if (!Classes.TryParseClassName(className, out var parsedClassType))
        {
            LocalizationService.HandleReply(ctx, "Invalid class, use .classes to see options.");
            return;
        }

        ulong steamId = ctx.Event.User.PlatformId;

        if (!Classes.HasClass(steamId) && steamId.TryGetPlayerClasses(out var classes)) // retrieval methods here could use improving but this is fine for now
        {
            Classes.UpdateClassData(ctx.Event.SenderCharacterEntity, parsedClassType, classes, steamId);
            LocalizationService.HandleReply(ctx, $"You have chosen <color=white>{parsedClassType}</color>");
        }
        else
        {
            LocalizationService.HandleReply(ctx, "You have already chosen a class.");
        }
    }

    [Command(name: "choosespell", shortHand: "csp", adminOnly: false, usage: ".class csp [#]", description: "Sets shift spell for class if prestige level is high enough.")]
    public static void ChooseClassSpell(ChatCommandContext ctx, int choice)
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        if (!ConfigService.ShiftSlot)
        {
            LocalizationService.HandleReply(ctx, "Shift slots are not enabled for class spells.");
            return;
        }

        Entity character = ctx.Event.SenderCharacterEntity;
        if (!InventoryUtilities.TryGetInventoryEntity(EntityManager, character, out Entity inventoryEntity) || InventoryUtilities.IsInventoryFull(EntityManager, inventoryEntity))
        {
            LocalizationService.HandleReply(ctx, "Can't change or activate class spells when inventory is full, need at least one space to safely handle jewels when switching.");
            return;
        }

        ulong steamId = ctx.Event.User.PlatformId;

        if (Classes.HasClass(steamId) && GetPlayerBool(steamId, "ShiftLock"))
        {
            PlayerClass playerClass = Classes.GetPlayerClass(steamId);

            if (ConfigService.PrestigeSystem && steamId.TryGetPlayerPrestiges(out var prestigeData) && prestigeData.TryGetValue(PrestigeType.Experience, out var prestigeLevel))
            {
                List<int> spells = Configuration.ParseConfigIntegerString(ClassSpellsMap[playerClass]);

                if (spells.Count == 0)
                {
                    LocalizationService.HandleReply(ctx, "No spells found for class.");
                    return;
                }

                if (choice < 0 || choice > spells.Count)
                {
                    LocalizationService.HandleReply(ctx, $"Invalid spell choice. (Use 0-{spells.Count})");
                    return;
                }

                if (choice == 0) // set default for all classes
                {
                    if (prestigeLevel < Configuration.ParseConfigIntegerString(ConfigService.PrestigeLevelsToUnlockClassSpells)[choice])
                    {
                        LocalizationService.HandleReply(ctx, "You do not have the required prestige level for that spell.");
                        return;
                    }

                    if (steamId.TryGetPlayerSpells(out var data))
                    {
                        if (ConfigService.DefaultClassSpell == 0)
                        {
                            LocalizationService.HandleReply(ctx, "No default spell found for classes.");
                            return;
                        }

                        PrefabGUID spellPrefabGUID = new(ConfigService.DefaultClassSpell);
                        data.ClassSpell = ConfigService.DefaultClassSpell;
                        steamId.SetPlayerSpells(data);

                        Classes.UpdateShift(ctx, character, spellPrefabGUID);
                        return;
                    }
                }

                if (prestigeLevel < Configuration.ParseConfigIntegerString(ConfigService.PrestigeLevelsToUnlockClassSpells)[choice])
                {
                    LocalizationService.HandleReply(ctx, "You do not have the required prestige level for that spell.");
                    return;
                }

                if (steamId.TryGetPlayerSpells(out var spellsData))
                {
                    spellsData.ClassSpell = spells[choice - 1];
                    steamId.SetPlayerSpells(spellsData);

                    Classes.UpdateShift(ctx, ctx.Event.SenderCharacterEntity, new(spellsData.ClassSpell));
                }
            }
            else
            {
                List<int> spells = Configuration.ParseConfigIntegerString(ClassSpellsMap[playerClass]);

                if (spells.Count == 0)
                {
                    LocalizationService.HandleReply(ctx, "No spells found for class.");
                    return;
                }

                if (choice < 0 || choice > spells.Count)
                {
                    LocalizationService.HandleReply(ctx, $"Invalid spell choice. (Use 0-{spells.Count})");
                    return;
                }

                if (choice == 0) // set default for all classes
                {
                    if (steamId.TryGetPlayerSpells(out var data))
                    {
                        if (ConfigService.DefaultClassSpell == 0)
                        {
                            LocalizationService.HandleReply(ctx, "No default spell found for classes.");
                            return;
                        }

                        PrefabGUID spellPrefabGUID = new(ConfigService.DefaultClassSpell);
                        data.ClassSpell = ConfigService.DefaultClassSpell;
                        steamId.SetPlayerSpells(data);

                        Classes.UpdateShift(ctx, ctx.Event.SenderCharacterEntity, spellPrefabGUID);
                        return;
                    }
                }

                if (steamId.TryGetPlayerSpells(out var spellsData))
                {
                    spellsData.ClassSpell = spells[choice - 1];
                    steamId.SetPlayerSpells(spellsData);

                    Classes.UpdateShift(ctx, ctx.Event.SenderCharacterEntity, new(spellsData.ClassSpell));
                }
            }
        }
        else
        {
            LocalizationService.HandleReply(ctx, "You haven't chosen a class yet or shift spell isn't enabled (<color=white>.shift</color>)");
        }
    }

    [Command(name: "change", adminOnly: false, usage: ".class change [Class]", description: "Change classes.")]
    public static void ClassChangeCommand(ChatCommandContext ctx, string className)
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        if (!Classes.TryParseClassName(className, out var parsedClassType))
        {
            LocalizationService.HandleReply(ctx, "Invalid class, use .classes to see options.");
            return;
        }

        ulong steamId = ctx.Event.User.PlatformId;
        Entity character = ctx.Event.SenderCharacterEntity;

        if (steamId.TryGetPlayerClasses(out var classes) && !Classes.HasClass(steamId))
        {
            LocalizationService.HandleReply(ctx, "You haven't chosen a class yet.");
            return;
        }

        if (ConfigService.ChangeClassItem != 0 && !Classes.HandleClassChangeItem(ctx))
        {
            return;
        }

        Classes.RemoveClassBuffs(ctx, steamId);
        Classes.UpdateClassData(character, parsedClassType, classes, steamId);

        LocalizationService.HandleReply(ctx, $"Class changed to <color=white>{parsedClassType}</color>!");
    }

    [Command(name: "syncbuffs", shortHand: "sb", adminOnly: false, usage: ".class sb", description: "Applies class buffs appropriately if not present.")]
    public static void SyncClassBuffsCommand(ChatCommandContext ctx)
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        var steamId = ctx.Event.User.PlatformId;

        if (Classes.HasClass(steamId))
        {
            PlayerClass playerClass = Classes.GetPlayerClass(steamId);
            List<int> perks = Classes.GetClassBuffs(steamId);

            if (perks.Count == 0)
            {
                LocalizationService.HandleReply(ctx, "Class buffs not found...");
                return;
            }

            Buffs.ApplyClassBuffs(ctx.Event.SenderCharacterEntity, steamId);
            LocalizationService.HandleReply(ctx, $"Class buffs applied for <color=white>{playerClass}</color>");
        }
        else
        {
            LocalizationService.HandleReply(ctx, "You haven't chosen a class yet.");
        }
    }

    [Command(name: "list", shortHand: "l", adminOnly: false, usage: ".class l", description: "Lists classes.")]
    public static void ListClasses(ChatCommandContext ctx)
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        string classTypes = string.Join(", ", Enum.GetNames(typeof(PlayerClass)));
        LocalizationService.HandleReply(ctx, $"Available Classes: <color=white>{classTypes}</color>");
    }

    [Command(name: "listbuffs", shortHand: "lb", adminOnly: false, usage: ".class lb [ClassType]", description: "Shows perks that can be gained from class.")]
    public static void ClassPerks(ChatCommandContext ctx, string classType = "")
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        ulong steamId = ctx.Event.User.PlatformId;

        if (Classes.HasClass(steamId))
        {
            PlayerClass playerClass = Classes.GetPlayerClass(steamId);

            if (!string.IsNullOrEmpty(classType) && Classes.TryParseClass(classType, out PlayerClass requestedClass))
            {
                playerClass = requestedClass;
            }

            Classes.ReplyClassBuffs(ctx, playerClass);
        }
        else
        {
            if (!string.IsNullOrEmpty(classType) && Classes.TryParseClass(classType, out PlayerClass requestedClass))
            {
                Classes.ReplyClassBuffs(ctx, requestedClass);
            }
            else
            {
                LocalizationService.HandleReply(ctx, "Invalid class type. Use '.class l' to see options.");
            }
        }
    }

    [Command(name: "listspells", shortHand: "lsp", adminOnly: false, usage: ".class lsp [ClassType]", description: "Shows spells that can be gained from class.")]
    public static void ListClassSpells(ChatCommandContext ctx, string classType = "")
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        ulong steamId = ctx.Event.User.PlatformId;

        if (Classes.HasClass(steamId))
        {
            PlayerClass playerClass = Classes.GetPlayerClass(steamId);

            if (!string.IsNullOrEmpty(classType) && Classes.TryParseClass(classType, out PlayerClass requestedClass))
            {
                playerClass = requestedClass;
            }

            Classes.ReplyClassSpells(ctx, playerClass);
        }
        else
        {
            if (!string.IsNullOrEmpty(classType) && Classes.TryParseClass(classType, out PlayerClass requestedClass))
            {
                Classes.ReplyClassSpells(ctx, requestedClass);
            }
            else
            {
                LocalizationService.HandleReply(ctx, "Invalid class type. Use '.class l' to see options.");
            }
        }
    }

    [Command(name: "liststats", shortHand: "lst", adminOnly: false, usage: ".class lst [Class]", description: "Shows weapon and blood stat synergies for a class.")]
    public static void ListClassStats(ChatCommandContext ctx, string classType = "")
    {
        if (!_classes)
        {
            LocalizationService.HandleReply(ctx, "Classes are not enabled.");
            return;
        }

        ulong steamId = ctx.Event.User.PlatformId;

        if (Classes.HasClass(steamId))
        {
            PlayerClass playerClass = Classes.GetPlayerClass(steamId);

            if (!string.IsNullOrEmpty(classType) && Classes.TryParseClass(classType, out PlayerClass requestedClass))
            {
                playerClass = requestedClass;
            }

            Classes.ReplyClassSynergies(ctx, playerClass);
        }
        else
        {
            if (!string.IsNullOrEmpty(classType) && Classes.TryParseClass(classType, out PlayerClass requestedClass))
            {
                Classes.ReplyClassSynergies(ctx, requestedClass);

                /*
                var weaponStats = weaponBloodStats.Item1.Split(',').Select(v => ((WeaponStatType)int.Parse(v)).ToString()).ToList();
                var bloodStats = weaponBloodStats.Item2.Split(',').Select(v => ((BloodStatType)int.Parse(v)).ToString()).ToList();

                if (weaponStats.Count == 0 && bloodStats.Count == 0)
                {
                    LocalizationService.HandleReply(ctx, "No stat synergies found for class.");
                    return;
                }

                var allStats = new List<string>();
                allStats.AddRange(weaponStats.Select(stat => $"<color=white>{stat}</color> (<color=#00FFFF>Weapon</color>)"));
                allStats.AddRange(bloodStats.Select(stat => $"<color=white>{stat}</color> (<color=red>Blood</color>)"));

                for (int i = 0; i < allStats.Count; i += 6)
                {
                    var batch = allStats.Skip(i).Take(6);
                    string replyMessage = string.Join(", ", batch);
                    LocalizationService.HandleReply(ctx, $"{requestedClass} stat synergies[x<color=white>{ConfigService.StatSynergyMultiplier}</color>]: {replyMessage}");
                }
                */
            }
            else
            {
                LocalizationService.HandleReply(ctx, "Invalid class type. Use '.class l' to see options.");
            }
        }
    }
}