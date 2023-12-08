using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using MySqlConnector;

namespace LiteVip;

[MinimumApiVersion(110)]
public class LiteVip : BasePlugin
{
    public override string ModuleAuthor => "thesamefabius";
    public override string ModuleName => "Lite VIP";
    public override string ModuleVersion => "v1.0.7";

    private static string _dbConnectionString = string.Empty;
    private static readonly User?[] Users = new User[Server.MaxPlayers];
    private static Config _config = null!;
    private static readonly int?[] Jumps = new int?[Server.MaxPlayers];
    private static readonly int?[] Respawn = new int?[Server.MaxPlayers];

    //private short _offsetRender;
    private static readonly UserSettings?[] UsersSettings = new UserSettings?[Server.MaxPlayers];

    public override void Load(bool hotReload)
    {
        _dbConnectionString = BuildConnectionString();
        Task.Run(CreateTable);
        Task.Run(CreateKeysTable);
        Task.Run(CreateVipTestTable);
        //_offsetRender = Schema.GetSchemaOffset("CBaseModelEntity", "m_clrRender");
        _config = LoadConfig();

        RegisterEventHandler<EventDecoyFiring>(EventDecoyFiring);
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(EventRoundStart);

        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        RegisterListener<Listeners.OnClientConnected>((slot) =>
        {
            UsersSettings[slot + 1] = new UserSettings
            {
                IsGravity = false, IsHealth = true, IsArmor = true,
                IsHealthshot = true, IsDecoy = true, IsJumps = true,
                IsItems = true, IsRainbow = true, DecoyCount = 0,
                JumpsCount = 0, LastButtons = 0, LastFlags = 0
            };

            Jumps[slot + 1] = 0;
            Respawn[slot + 1] = 0;
        });

        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player.IsBot) return;

            Task.Run(() => OnClientAuthorizedAsync(player, slot, id));
        });

        RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                var index = player.Index;

                OnTick(player);
            }
        });

        RegisterListener<Listeners.OnClientDisconnectPost>(slot =>
        {
            Users[slot + 1] = null;
            UsersSettings[slot + 1]!.RainbowTimer?.Kill();
            UsersSettings[slot + 1] = null;
            Jumps[slot + 1] = null;
            Respawn[slot + 1] = null;
        });

        CreateMenu();

        AddTimer(300, () => Task.Run(RemoveExpiredUsers), TimerFlags.REPEAT);
    }

    private async Task OnClientAuthorizedAsync(CCSPlayerController player, int slot, SteamID steamId)
    {
        var msg = await RemoveExpiredUsers();
        PrintToServer(msg, ConsoleColor.DarkGreen);

        var user = await GetUserFromDb(steamId);

        if (user == null) return;

        Users[slot + 1] = new User
        {
            SteamId = user.SteamId,
            VipGroup = user.VipGroup,
            StartVipTime = user.StartVipTime,
            EndVipTime = user.EndVipTime
        };

        var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(user.EndVipTime) - DateTimeOffset.UtcNow;
        var timeRemainingFormatted =
            $"{timeRemaining.Days}d {timeRemaining.Hours:D2}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";
        Server.NextFrame(() =>
        {
            PrintToChat(player,
                $"Welcome to the server! You are a VIP player. Group: '\x0C{user.VipGroup}\x08'{(user.EndVipTime == 0 ? "" : $", Expires in: \x06{timeRemainingFormatted}")}.");
        });
    }

    [ConsoleCommand("css_vips")]
    public void OnCommandVips(CCSPlayerController? controller, CommandInfo info)
    {
        if (controller == null) return;

        var id = 1;
        foreach (var playersVips in Utilities.GetPlayers().Where(u => u.IsValid))
        {
            if (Users[playersVips.Index] == null) continue;

            PrintToChat(controller, $"{id ++}. {playersVips.PlayerName} - {Users[playersVips.Index]!.VipGroup}");
        }
    }

    [ConsoleCommand("css_vip_respawn")]
    public void OnCommandRespawn(CCSPlayerController? controller, CommandInfo info)
    {
        if (controller == null) return;
        var entityIndex = controller.Index;

        var group = GetUserVipGroup(controller);

        if (group == null) return;

        if (Respawn[entityIndex] >= group.Respawn)
        {
            PrintToChat(controller, "You've already used all your respawns");
            return;
        }

        if (controller.PawnIsAlive)
        {
            PrintToChat(controller, "You should be dead!");
            return;
        }

        if (controller.PlayerPawn.Value != null) controller.PlayerPawn.Value.Respawn();
        Respawn[entityIndex] ++;
    }

    [ConsoleCommand("css_vip_createkey")]
    public void OnCommandCreateKey(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var splitCmdArgs = ParseCommandArguments(command.ArgString);

        if (splitCmdArgs.Length is > 2 or < 2)
        {
            PrintToServer("Usage: css_vip_createkey <vipgroup> <time_seconds>", ConsoleColor.Red);
            return;
        }

        var vipGroup = ExtractValueInQuotes(splitCmdArgs[0]);
        var time = ExtractValueInQuotes(splitCmdArgs[1]);

        if (!_config.Groups.ContainsKey(vipGroup))
        {
            PrintToServer("This VIP group was not found!", ConsoleColor.DarkRed);
            return;
        }

        var key = $"key-{DateTime.Now.ToString("yyyyMMddHHmmssfff") + new Random().Next(100, 999)}";

        Task.Run(() => AddKeyToDb(key, vipGroup, int.Parse(time)));
    }

    [ConsoleCommand("css_vip_adduser")]
    public void OnCmdAddUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var splitCmdArgs = ParseCommandArguments(command.ArgString);

        if (splitCmdArgs.Length is > 3 or < 3)
        {
            PrintToServer("Usage: css_vip_adduser <steamid> <vipgroup> <time_second>", ConsoleColor.Red);
            return;
        }

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);
        var vipGroup = ExtractValueInQuotes(splitCmdArgs[1]);
        var endVipTime = Convert.ToInt32(ExtractValueInQuotes(splitCmdArgs[2]));

        if (!_config.Groups.ContainsKey(vipGroup))
        {
            PrintToServer("This VIP group was not found!", ConsoleColor.DarkRed);
            return;
        }

        Task.Run(() => AddUserToDb(new User
        {
            SteamId = steamId,
            VipGroup = vipGroup,
            StartVipTime = DateTime.UtcNow.GetUnixEpoch(),
            EndVipTime = endVipTime == 0 ? 0 : DateTime.UtcNow.AddSeconds(endVipTime).GetUnixEpoch()
        }));
    }

    [ConsoleCommand("css_vip_deleteuser")]
    public void OnCmdDeleteVipUser(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null) return;

        var splitCmdArgs = ParseCommandArguments(command.ArgString);

        if (splitCmdArgs.Length is < 1 or > 1)
        {
            ReplyToCommand(controller, "Using: css_vip_deleteuser <steamid>");
            return;
        }

        var steamId = ExtractValueInQuotes(splitCmdArgs[0]);

        Task.Run(() => RemoveUserFromDb(steamId));
    }

    [ConsoleCommand("css_vip_key")]
    public void OnCommandKey(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        if (IsUserVip(controller.Index))
        {
            PrintToChat(controller, "You already have VIP privileges.");
            return;
        }

        if (command.ArgCount is < 2 or > 2)
        {
            ReplyToCommand(controller, "Using: css_vip_key <key>");
            return;
        }

        var key = ParseCommandArguments(command.ArgString);

        var steamId = new SteamID(controller.SteamID).SteamId2;
        Task.Run(() => GivePlayerVipByKey(controller, steamId, key[0]));
    }

    private async void GivePlayerVipByKey(CCSPlayerController player, string steamId, string key)
    {
        var vipGroupAndTime = await GetVipGroupAndTimeByKey(key);

        if (!string.IsNullOrEmpty(vipGroupAndTime.VipGroup))
        {
            await AddUserToDb(new User
            {
                SteamId = steamId,
                VipGroup = vipGroupAndTime.VipGroup,
                StartVipTime = DateTime.UtcNow.GetUnixEpoch(),
                EndVipTime = vipGroupAndTime.Time == 0
                    ? 0
                    : DateTime.UtcNow.AddSeconds(vipGroupAndTime.Time).GetUnixEpoch()
            });

            await RemoveKeyFromDb(key);

            Server.NextFrame(() =>
            {
                Users[player.Index] = new User
                {
                    SteamId = steamId,
                    VipGroup = vipGroupAndTime.VipGroup,
                    StartVipTime = DateTime.UtcNow.GetUnixEpoch(),
                    EndVipTime = vipGroupAndTime.Time == 0
                        ? 0
                        : DateTime.UtcNow.AddSeconds(vipGroupAndTime.Time).GetUnixEpoch()
                };
            });

            Server.NextFrame(() => PrintToChat(player,
                $"Key '{key}' has been successfully activated! You are now a member of the VIP group '{vipGroupAndTime.VipGroup}'"));
        }
        else
            Server.NextFrame(() =>
                PrintToChat(player, $"Failed to activate key '{key}'. Please check if the key is valid."));
    }

    [ConsoleCommand("css_viptest")]
    public void OnCommandVipTest(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;

        var vipTestSettings = _config.VipTestSettings;

        if (!vipTestSettings.VipTestEnabled) return;

        if (IsUserVip(controller.Index))
        {
            PrintToChat(controller, "You already have VIP privileges.");
            return;
        }

        Task.Run(() => GivePlayerVipTest(controller, new SteamID(controller.SteamID), vipTestSettings));
    }

    private async void GivePlayerVipTest(CCSPlayerController player, SteamID steamId,
        VipTestSettings vipTestSettings)
    {
        var vipTest = new VipTest(_dbConnectionString);

        var vipTestCount = await vipTest.GetVipTestCount(steamId.SteamId2);

        if (vipTestCount.Count >= vipTestSettings.VipTestCount)
        {
            Server.NextFrame(() => PrintToChat(player, "You can no longer take the VIP Test"));
            return;
        }

        if (vipTestCount.EndTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(vipTestCount.EndTime) -
                       DateTimeOffset.UtcNow;
            var timeRemainingFormatted =
                $"{(time.Days == 0 ? "" : $"{time.Days}d")} {time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";

            Server.NextFrame(() =>
                PrintToChat(player, $"The VIP test can only be retaken through: {timeRemainingFormatted}"));
            return;
        }

        var endTime = DateTime.UtcNow.AddSeconds(vipTestSettings.VipTestTime).GetUnixEpoch();
        await AddUserToDb(new User
        {
            SteamId = steamId.SteamId2,
            VipGroup = vipTestSettings.VipTestGroup,
            StartVipTime = DateTime.UtcNow.GetUnixEpoch(),
            EndVipTime = endTime
        });

        var vipTestCooldown = DateTime.UtcNow.AddSeconds(vipTestSettings.VipTestCooldown).GetUnixEpoch();

        await AddUserOrUpdateVipTestAsync(steamId.SteamId2, vipTestCooldown, vipTest);

        Server.NextFrame(() => Users[player.Index] = new User
        {
            SteamId = steamId.SteamId2,
            VipGroup = vipTestSettings.VipTestGroup,
            StartVipTime = DateTime.UtcNow.GetUnixEpoch(),
            EndVipTime = endTime
        });

        var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(endTime) - DateTimeOffset.UtcNow;

        Server.NextFrame(() => PrintToChat(player,
            $"You have successfully received the 'VIP Test'! Ends in {timeRemaining.ToString(timeRemaining.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss")}"));
    }

    private async Task AddUserOrUpdateVipTestAsync(string steamId, int endTime, VipTest vipTest)
    {
        var userInVipTest = await vipTest.IsUserInVipTest(steamId);

        if (userInVipTest)
        {
            await vipTest.UpdateUserVipTestCount(steamId, 1, endTime);
            return;
        }

        await vipTest.AddUserToVipTest(steamId, 1, endTime);
    }

    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null)
            if (!_config.Admins.Contains(controller.SteamID))
            {
                PrintToChat(controller, "You do not have access to this command");
                return;
            }

        _config = LoadConfig();

        const string msg = "configuration successfully rebooted!";

        ReplyToCommand(controller, msg);
    }

    private string[] ParseCommandArguments(string argString)
    {
        var parse = Regex.Matches(argString, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value.Trim('"'))
            .ToArray();

        return parse;
    }

    private HookResult EventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        foreach (var userSettings in UsersSettings)
        {
            if (userSettings != null)
                userSettings.DecoyCount = 0;
        }

        foreach (var player in Utilities.GetPlayers())
        {
            var entityIndex = player.Index;
            if (Respawn[entityIndex] != null)
                Respawn[entityIndex] = 0;
        }

        return HookResult.Continue;
    }

    private HookResult EventDecoyFiring(EventDecoyFiring @event, GameEventInfo info)
    {
        if (@event.Userid == null) return HookResult.Continue;

        var controller = @event.Userid;
        var entityIndex = controller.Index;

        var user = Users[entityIndex];
        if (user == null) return HookResult.Continue;

        if (!_config.Groups.TryGetValue(Users[controller.Index]!.VipGroup, out var group))
            return HookResult.Continue;

        if (group.DecoySettings == null) return HookResult.Continue;
        if (!group.DecoySettings.DecoyTeleport) return HookResult.Continue;

        if (!UsersSettings[entityIndex]!.IsDecoy) return HookResult.Continue;

        var pDecoyFiring = @event;
        var bodyComponent = @event.Userid.PlayerPawn.Value?.CBodyComponent?.SceneNode;

        if (bodyComponent == null) return HookResult.Continue;

        if (UsersSettings[entityIndex]!.DecoyCount >= group.DecoySettings.DecoyCountInOneRound)
            return HookResult.Continue;

        bodyComponent.AbsOrigin.X = pDecoyFiring.X;
        bodyComponent.AbsOrigin.Y = pDecoyFiring.Y;
        bodyComponent.AbsOrigin.Z = pDecoyFiring.Z;

        var decoyIndex = NativeAPI.GetEntityFromIndex(pDecoyFiring.Entityid);

        if (decoyIndex == IntPtr.Zero) return HookResult.Continue;

        new CBaseCSGrenadeProjectile(decoyIndex).Remove();

        UsersSettings[entityIndex]!.DecoyCount ++;

        return HookResult.Continue;
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid.Handle == IntPtr.Zero || @event.Userid.UserId == null) return HookResult.Continue;

        var controller = @event.Userid;

        if (controller.IsBot || !controller.IsValid) return HookResult.Continue;

        AddTimer(_config.Delay, () => Timer_Give(controller));

        return HookResult.Continue;
    }

    private void Timer_Give(CCSPlayerController controller)
    {
        if (controller.TeamNum is not ((int)CsTeam.Terrorist or (int)CsTeam.CounterTerrorist) ||
            !controller.IsValid || controller.SteamID == 0) return;

        var group = GetUserVipGroup(controller);

        if (group == null) return;

        var userSettings = UsersSettings[controller.Index]!;

        var playerPawnValue = controller.PlayerPawn.Value;

        if (playerPawnValue == null) return;

        var moneyServices = controller.InGameMoneyServices;

        var gamerules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
        var halftime = ConVar.Find("mp_halftime")!.GetPrimitiveValue<bool>();
        var maxrounds = ConVar.Find("mp_maxrounds")!.GetPrimitiveValue<int>();

        var disableAllOrPartial = -1;
        if (gamerules != null)
            if (gamerules.TotalRoundsPlayed == 0 || (halftime && maxrounds / 2 == gamerules.TotalRoundsPlayed))
                disableAllOrPartial = _config.DisableAllOrPartial;

        if (disableAllOrPartial != 2)
        {
            if (group.Health != null)
                if (userSettings.IsHealth)
                {
                    playerPawnValue.Health = group.Health.Value;
                    playerPawnValue.MaxHealth = group.Health.Value;
                }

            if (group.Armor != null)
                if (userSettings.IsArmor)
                    playerPawnValue.ArmorValue = group.Armor.Value;

            if (group.Gravity != null)
                if (userSettings.IsGravity)
                    playerPawnValue.GravityScale = group.Gravity.Value;
        }

        if (disableAllOrPartial is not (1 or 2))
        {
            if (group.Money != null)
            {
                if (group.Money.Value != -1)
                {
                    if (moneyServices != null)
                        moneyServices.Account += group.Money.Value;
                }
            }

            if (playerPawnValue.ItemServices != null)
            {
                if (group.DecoySettings is { DecoyTeleport: true } && userSettings.IsDecoy &&
                    group.DecoySettings.DecoyCountToBeIssued > 0)
                {
                    const string decoy = "weapon_decoy";
                    for (var i = 0; i < group.DecoySettings.DecoyCountToBeIssued; i ++)
                    {
                        if (!HasItem(controller, decoy))
                            GiveItem(controller, decoy);
                    }
                }

                if (group.Healthshot != null && userSettings.IsHealthshot && group.Healthshot.Value > 0)
                {
                    const string healthShot = "weapon_healthshot";
                    var countHealthshot =
                        playerPawnValue.WeaponServices?.MyWeapons.Count(w => w.Value?.DesignerName == healthShot);

                    for (var i = 0; i < group.Healthshot.Value - countHealthshot; i ++)
                        GiveItem(controller, healthShot);
                }

                if (group.Items != null && userSettings.IsItems && group.Items.Count > 0)
                {
                    foreach (var item in group.Items)
                    {
                        if (!HasItem(controller, item))
                            GiveItem(controller, item);
                    }
                }
            }

            if (controller is { TeamNum: (int)CsTeam.CounterTerrorist, PawnHasDefuser: false })
                GiveItem(controller, "item_defuser");
        }

        if (group.RainbowModel != null)
        {
            if (group.RainbowModel.Value)
            {
                if (userSettings.IsRainbow)
                {
                    userSettings.RainbowTimer?.Kill();
                    userSettings.RainbowTimer = AddTimer(1.4f,
                        () =>
                        {
                            Timer_SetRainbowModel(playerPawnValue, Random.Shared.Next(0, 255),
                                Random.Shared.Next(0, 255), Random.Shared.Next(0, 255));
                        },
                        TimerFlags.REPEAT);
                }
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Player {controller.PlayerName} has gained Health: {group.Health} | Armor: {group.Armor}");
        Console.ResetColor();
    }

    // private int GetPlayerItemsCount(CCSPlayerController player, string item)
    // {
    //     return player.PlayerPawn.Value?.WeaponServices?.MyWeapons.Count(w =>
    //         w.Value != null && w.Value.DesignerName == item) ?? 0;
    // }

    private void GiveItem(CCSPlayerController handle, string item)
    {
        handle.GiveNamedItem(item);
    }

    private bool HasItem(CCSPlayerController player, string item)
    {
        foreach (var weapon in player.PlayerPawn.Value?.WeaponServices?.MyWeapons!)
        {
            if (weapon.Value != null && !weapon.Value.DesignerName.Contains(item)) continue;

            return true;
        }

        return false;
    }

    private VipGroup? GetUserVipGroup(CCSPlayerController player)
    {
        var user = Users[player.Index];

        if (user == null) return null;
        if (!IsUserInDatabase(new SteamID(player.SteamID).SteamId2, user.VipGroup)) return null;

        return !_config.Groups.TryGetValue(user.VipGroup, out var group) ? null : group;
    }

    private static void OnTick(CCSPlayerController player)
    {
        var client = player.Index;
        var user = Users[client];

        if (user == null)
            Jumps[client] = _config.JumpsNoVip;
        else
        {
            if (_config.Groups.TryGetValue(user.VipGroup, out var group))
            {
                if (group.JumpsCount == null || Users[client] == null)
                    Jumps[client] = _config.JumpsNoVip;
                else
                    Jumps[client] = group.JumpsCount;
            }
        }

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn != null)
        {
            var flags = (PlayerFlags)playerPawn.Flags;
            var buttons = player.Buttons;

            if (UsersSettings[client] == null) return;
            if (!UsersSettings[client]!.IsJumps) return;

            if ((UsersSettings[client]!.LastFlags & PlayerFlags.FL_ONGROUND) != 0 &&
                (flags & PlayerFlags.FL_ONGROUND) == 0 &&
                (UsersSettings[client]!.LastButtons & PlayerButtons.Jump) == 0 && (buttons & PlayerButtons.Jump) != 0)
                UsersSettings[client]!.JumpsCount ++;
            else if ((flags & PlayerFlags.FL_ONGROUND) != 0)
                UsersSettings[client]!.JumpsCount = 0;
            else if ((UsersSettings[client]!.LastButtons & PlayerButtons.Jump) == 0 &&
                     (buttons & PlayerButtons.Jump) != 0 &&
                     UsersSettings[client]!.JumpsCount < Jumps[client])
            {
                UsersSettings[client]!.JumpsCount ++;
                playerPawn.AbsVelocity.Z = 300;
            }

            UsersSettings[client]!.LastFlags = flags;
            UsersSettings[client]!.LastButtons = buttons;
        }
    }


    private void Timer_SetRainbowModel(CCSPlayerPawn pawn, int r = 255, int g = 255, int b = 255)
    {
        pawn.Render = Color.FromArgb(255, r, g, b);
    }

    private void CreateMenu()
    {
        var menu = new ChatMenu("\x08--[ \x0CVIP MENU \x08]--");
        menu.AddMenuOption("Health", (player, option) =>
            TogglePlayerFunction(player, UsersSettings[player.Index]!.IsHealth ^= true,
                option.Text));
        menu.AddMenuOption("Armor", (player, option) =>
            TogglePlayerFunction(player, UsersSettings[player.Index]!.IsArmor ^= true, option.Text));
        menu.AddMenuOption("Gravity", (player, _) => AdjustPlayerGravity(player));
        menu.AddMenuOption("Healthshot", (player, option) =>
            TogglePlayerFunction(player, UsersSettings[player.Index]!.IsHealthshot ^= true,
                option.Text));
        menu.AddMenuOption("Decoy Teleport", (player, option) =>
            TogglePlayerFunction(player, UsersSettings[player.Index]!.IsDecoy ^= true, option.Text));
        menu.AddMenuOption("Jumps", (player, option) =>
            TogglePlayerFunction(player, UsersSettings[player.Index]!.IsJumps ^= true, option.Text));
        menu.AddMenuOption("Items", (player, option) =>
            TogglePlayerFunction(player, UsersSettings[player.Index]!.IsItems ^= true, option.Text));
        menu.AddMenuOption("Rainbow Model", (player, option) =>
        {
            var entityIndex = player.Index;

            if (UsersSettings[entityIndex]!.IsRainbow)
                if (player.PlayerPawn.Value != null)
                    Timer_SetRainbowModel(player.PlayerPawn.Value);
            UsersSettings[entityIndex]!.RainbowTimer?.Kill();

            TogglePlayerFunction(player, UsersSettings[entityIndex]!.IsRainbow ^= true, option.Text);
        });
        AddCommand("css_vip", "command that opens the VIP MENU", (player, _) =>
        {
            if (player == null) return;

            if (!IsUserVip(player.Index))
            {
                PrintToChat(player, "You do not have access to this command!");
                return;
            }

            ChatMenus.OpenMenu(player, menu);
        });
        AddCommand("css_vip_gravity", "command allows you to turn gravity on and off.",
            (player, _) =>
            {
                if (player == null) return;

                if (!IsUserVip(player.Index))
                {
                    PrintToChat(player, "You do not have access to this command!");
                    return;
                }

                AdjustPlayerGravity(player);
            });
    }

    private void TogglePlayerFunction(CCSPlayerController player, bool func, string name)
    {
        PrintToChat(player, !func ? $"{name}: \x02Off" : $"{name}: \x06On");
    }

    private void AdjustPlayerGravity(CCSPlayerController? controller)
    {
        if (controller == null) return;

        if (Users[controller.Index] == null) return;

        if (!_config.Groups.TryGetValue(Users[controller.Index]!.VipGroup, out var group)) return;

        var gravity = UsersSettings[controller.Index]!.IsGravity ^= true;

        if (!gravity)
        {
            PrintToChat(controller, "Gravity: \x02Off");
            if (controller.PlayerPawn.Value != null) controller.PlayerPawn.Value.GravityScale = 1.0f;
            return;
        }

        PrintToChat(controller, "Gravity: \x06On");
        if (group.Gravity != null)
            if (controller.PlayerPawn.Value != null)
                controller.PlayerPawn.Value.GravityScale = group.Gravity.Value;
    }

    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity.DesignerName != "smokegrenade_projectile") return;

        var smokeGrenade = new CSmokeGrenadeProjectile(entity.Handle);
        if (smokeGrenade.Handle == IntPtr.Zero) return;

        Server.NextFrame(() =>
        {
            if (smokeGrenade.Thrower.Value == null) return;
            if (smokeGrenade.Thrower.Value.Controller.Value == null) return;

            var entityIndex = smokeGrenade.Thrower.Value.Controller.Value.Index;

            var user = Users[entityIndex];
            if (user == null) return;
            if (!_config.Groups.TryGetValue(user.VipGroup, out var group)) return;

            if (group.SmokeColor == null) return;

            var split = group.SmokeColor.Split(" ");

            smokeGrenade.SmokeColor.X = group.SmokeColor == "random"
                ? Random.Shared.NextSingle() * 255.0f
                : float.Parse(split[0]);
            smokeGrenade.SmokeColor.Y = group.SmokeColor == "random"
                ? Random.Shared.NextSingle() * 255.0f
                : float.Parse(split[1]);
            smokeGrenade.SmokeColor.Z = group.SmokeColor == "random"
                ? Random.Shared.NextSingle() * 255.0f
                : float.Parse(split[2]);
        });
    }

    private string BuildConnectionString()
    {
        var dbConfig = LoadConfig();

        Console.WriteLine("Building connection string");
        var builder = new MySqlConnectionStringBuilder
        {
            Database = dbConfig.Connection.Database,
            UserID = dbConfig.Connection.User,
            Password = dbConfig.Connection.Password,
            Server = dbConfig.Connection.Host,
            Port = 3306
        };

        Console.WriteLine("OK!");
        return builder.ConnectionString;
    }

    static async Task CreateTable()
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var createVipUsersTable = @"
            CREATE TABLE IF NOT EXISTS `litevip_users` (
                `SteamId` VARCHAR(255)  NOT NULL PRIMARY KEY,
                `VipGroup` VARCHAR(255) NOT NULL,
                `StartVipTime` BIGINT NOT NULL,
                `EndVipTime` BIGINT NOT NULL
            );";

            await dbConnection.ExecuteAsync(createVipUsersTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    static async Task CreateKeysTable()
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var createKeysTable = @"
            CREATE TABLE IF NOT EXISTS `litevip_keys` (
                `Key` VARCHAR(255) NOT NULL PRIMARY KEY,
                `VipGroup` VARCHAR(255) NOT NULL,
                `Time` BIGINT NOT NULL
            );";

            await dbConnection.ExecuteAsync(createKeysTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    static async Task CreateVipTestTable()
    {
        try
        {
            await using var dbConnection = new MySqlConnection(_dbConnectionString);
            dbConnection.Open();

            var createKeysTable = @"
            CREATE TABLE IF NOT EXISTS `litevip_test` (
                `SteamId` VARCHAR(255) NOT NULL PRIMARY KEY,
                `Count` BIGINT NOT NULL,
                `EndTime` BIGINT NOT NULL
            );";

            await dbConnection.ExecuteAsync(createKeysTable);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private bool IsUserInDatabase(string steamId, string vipGroup)
    {
        using var connection = new MySqlConnection(_dbConnectionString);

        var existingUser = connection.QueryFirstOrDefault<User>(
            "SELECT * FROM litevip_users WHERE SteamId = @SteamId AND VipGroup = @VipGroup",
            new { SteamId = steamId, VipGroup = vipGroup });

        return existingUser != null;
    }

    private async Task<User?> GetUserFromDb(SteamID steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);
            await connection.OpenAsync();
            var user = await connection.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM `litevip_users` WHERE `SteamId` = @SteamId", new { SteamId = steamId.SteamId2 });

            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    private async Task<string> RemoveExpiredUsers()
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var expiredUsers = await connection.QueryAsync<User>(
                "SELECT * FROM litevip_users WHERE EndVipTime < @CurrentTime AND EndVipTime > 0",
                new { CurrentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            foreach (var user in expiredUsers)
            {
                await connection.ExecuteAsync("DELETE FROM litevip_users WHERE SteamId = @SteamId",
                    new { user.SteamId });

                Console.WriteLine($"User {user.SteamId} has been removed due to expired VIP status.");
            }

            return "Expired users removed successfully.";
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }

    private async Task AddUserToDb(User user)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM litevip_users WHERE SteamId = @SteamId", new { user.SteamId });

            if (existingUser != null)
            {
                PrintToServer("User already exists", ConsoleColor.Yellow);
                return;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO litevip_users (SteamId, VipGroup, StartVipTime, EndVipTime)
                VALUES (@SteamId, @VipGroup, @StartVipTime, @EndVipTime);", user);

            PrintToServer($"Player '{user.SteamId}' has been successfully added", ConsoleColor.Green);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async void AddKeyToDb(string key, string vipGroup, int time)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var keyExists = await KeyExists(connection, key);

            if (keyExists)
            {
                PrintToServer($"The key '{key}' already exists in the database.", ConsoleColor.Yellow);
                return;
            }

            await connection.ExecuteAsync(@"
                INSERT INTO litevip_keys 
                    (`Key`, VipGroup, `Time`)
                VALUES 
                    (@Key, @VipGroup, @Time);",
                new { Key = key, VipGroup = vipGroup, Time = time });

            PrintToServer($"The key '{key}' has been successfully added to the '{vipGroup}' group.",
                ConsoleColor.DarkGreen);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task RemoveKeyFromDb(string key)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var keyExists = await KeyExists(connection, key);

            if (!keyExists) return;

            await connection.ExecuteAsync(@"
            DELETE FROM litevip_keys
            WHERE `Key` = @Key;", new { Key = key });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task<bool> KeyExists(MySqlConnection connection, string key)
    {
        var existingKey = await connection.ExecuteScalarAsync<string>(@"
        SELECT `Key` FROM litevip_keys WHERE `Key` = @Key;", new { Key = key });

        return existingKey != null;
    }

    private async Task<(string VipGroup, int Time)> GetVipGroupAndTimeByKey(string key)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var result = await connection.QueryFirstOrDefaultAsync<(string VipGroup, int Time)>(@"
            SELECT VipGroup, Time FROM litevip_keys WHERE `Key` = @Key;", new { Key = key });

            return result != default ? result : (string.Empty, 0);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return (string.Empty, 0);
        }
    }

    private async void RemoveUserFromDb(string steamId)
    {
        try
        {
            await using var connection = new MySqlConnection(_dbConnectionString);

            var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
                @"SELECT * FROM litevip_users WHERE SteamId = @SteamId", new { SteamId = steamId });

            if (existingUser == null)
            {
                PrintToServer("User does not exist", ConsoleColor.Red);
                return;
            }

            await connection.ExecuteAsync(@"
            DELETE FROM litevip_users
            WHERE SteamId = @SteamId;", new { SteamId = steamId });

            PrintToServer($"Player '{steamId}' has been successfully removed", ConsoleColor.Red);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private bool IsUserVip(uint index)
    {
        var user = Users[index];
        if (user == null) return false;

        if (user.EndVipTime != 0 && DateTime.UtcNow.GetUnixEpoch() > user.EndVipTime)
        {
            Users[index] = null;
            return false;
        }

        return user.EndVipTime == 0 || DateTime.UtcNow.GetUnixEpoch() < user.EndVipTime;
    }

    private string ExtractValueInQuotes(string input)
    {
        var match = Regex.Match(input, @"""([^""]*)""");

        return match.Success ? match.Groups[1].Value : input;
    }

    private void ReplyToCommand(CCSPlayerController? controller, string msg)
    {
        if (controller != null)
            PrintToChat(controller, msg);
        else
            PrintToServer($"{msg}", ConsoleColor.DarkMagenta);
    }

    private void PrintToChat(CCSPlayerController player, string msg)
    {
        player.PrintToChat($"\x08[ \x0CLiteVip \x08] {msg}");
    }

    private void PrintToServer(string msg, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[LiteVip] {msg}");
        Console.ResetColor();
    }

    private Config LoadConfig()
    {
        var configPath = Path.Combine(ModuleDirectory, "vip.json");

        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            DisableAllOrPartial = 1, // 0 - off, 1 - items and money, 2 all
            JumpsNoVip = 2,
            Admins = new List<ulong>(),
            Delay = 2.0f,
            VipTestSettings = new VipTestSettings
            {
                VipTestEnabled = true,
                VipTestTime = 3600,
                VipTestCooldown = 3600,
                VipTestGroup = "GROUP_NAME",
                VipTestCount = 1
            },
            Groups = new Dictionary<string, VipGroup>
            {
                {
                    "GROUP_NAME", new VipGroup
                    {
                        Health = 100,
                        Armor = 100,
                        Gravity = 1.0f,
                        Money = 1000,
                        SmokeColor = "255 255 255",
                        Healthshot = 1,
                        JumpsCount = 2,
                        RainbowModel = true,
                        Respawn = 2,
                        Items = new List<string> { "weapon_molotov", "weapon_ak47" },
                        DecoySettings = new Decoy
                            { DecoyTeleport = true, DecoyCountInOneRound = 1, DecoyCountToBeIssued = 1 }
                    }
                }
            },
            Connection = new LiteVipDb
            {
                Host = "HOST",
                Database = "DATABASENAME",
                User = "USER",
                Password = "PASSWORD"
            }
        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("[LiteVip] The configuration was successfully saved to a file: " + configPath);
        Console.ResetColor();

        return config;
    }
}

public static class GetUnixTime
{
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }
}

public class Config
{
    public int DisableAllOrPartial { get; set; }
    public int JumpsNoVip { get; set; }
    public List<ulong> Admins { get; set; } = null!;
    public float Delay { get; set; }
    public VipTestSettings VipTestSettings { get; set; } = null!;
    public required Dictionary<string, VipGroup> Groups { get; set; }
    public LiteVipDb Connection { get; set; } = null!;
}

public class VipTestSettings
{
    public bool VipTestEnabled { get; init; }
    public int VipTestTime { get; init; }
    public int VipTestCooldown { get; init; }
    public required string VipTestGroup { get; init; }
    public int VipTestCount { get; init; }
}

public class User
{
    public required string SteamId { get; set; }
    public required string VipGroup { get; set; }
    public int StartVipTime { get; set; }
    public int EndVipTime { get; set; }
}

public class LiteVipDb
{
    public required string Host { get; init; }
    public required string Database { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
}