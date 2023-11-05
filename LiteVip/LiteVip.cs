using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;

namespace LiteVip;

public class LiteVip : BasePlugin
{
    public override string ModuleName => "Lite VIP | by thesamefabius";
    public override string ModuleVersion => "v1.0.3";

    private static Config _config = null!;
    public static readonly UserSettings?[] Users = new UserSettings?[Server.MaxPlayers];

    public override void Load(bool hotReload)
    {
        _config = LoadConfig();
        RegisterEventHandler<EventDecoyFiring>(EventDecoyFiring);
        RegisterEventHandler<EventPlayerBlind>(EventPlayerBlind);
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
        RegisterEventHandler<EventRoundStart>(EventRoundStart);

        RegisterListener<Listeners.OnTick>(() =>
        {
            for (var i = 1; i < Server.MaxPlayers; i++)
            {
                var entity = NativeAPI.GetEntityFromIndex(i);

                if (entity == 0) continue;

                var player = new CCSPlayerController(entity);

                if (player is not { IsValid: true }) continue;

                OnTick(player);
            }
        });

        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
        RegisterListener<Listeners.OnClientConnected>(playerSlot =>
        {
            Users[playerSlot + 1] = new UserSettings
            {
                IsGravity = false, IsHealth = true, IsArmor = true,
                IsHealthshot = true, IsDecoy = true, IsJumps = true,
                DecoyCount = 0, JumpsCount = 0, LastButtons = 0,
                LastFlags = 0
            };
        });

        RegisterListener<Listeners.OnClientDisconnectPost>(clientIndex => { Users[clientIndex + 1] = null; });

        CreateMenu();
    }

    private HookResult EventPlayerBlind(EventPlayerBlind @event, GameEventInfo info)
    {
        @event.BlindDuration = 0.0f;

        return HookResult.Continue;
    }

    private HookResult EventRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        foreach (var userSettings in Users)
        {
            if (userSettings != null)
                userSettings.DecoyCount = 0;
        }

        return HookResult.Continue;
    }

    private HookResult EventDecoyFiring(EventDecoyFiring @event, GameEventInfo info)
    {
        if (@event.Userid == null) return HookResult.Continue;

        var controller = @event.Userid;
        var entityIndex = controller.EntityIndex!.Value.Value;

        if (!_config.Users.TryGetValue(controller.SteamID.ToString(), out var user)) return HookResult.Continue;

        if (!user.DecoySettings.DecoyTeleport) return HookResult.Continue;

        if (!Users[entityIndex]!.IsDecoy) return HookResult.Continue;

        var pDecoyFiring = @event;
        var bodyComponent = @event.Userid.PlayerPawn.Value.CBodyComponent?.SceneNode;

        if (bodyComponent == null) return HookResult.Continue;

        if (Users[entityIndex]!.DecoyCount >= user.DecoySettings.DecoyCountInOneRound) return HookResult.Continue;

        bodyComponent.AbsOrigin.X = pDecoyFiring.X;
        bodyComponent.AbsOrigin.Y = pDecoyFiring.Y;
        bodyComponent.AbsOrigin.Z = pDecoyFiring.Z;

        var decoyIndex = NativeAPI.GetEntityFromIndex(pDecoyFiring.Entityid);

        if (decoyIndex == IntPtr.Zero) return HookResult.Continue;

        new CBaseCSGrenadeProjectile(decoyIndex).Remove();

        Users[entityIndex]!.DecoyCount++;

        return HookResult.Continue;
    }

    [ConsoleCommand("css_vip_reload")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller != null)
            if (!_config.Admins.Contains(controller.SteamID))
            {
                controller.PrintToChat("\x08[ \x0CLITE-VIP \x08] you do not have access to this command");
                return;
            }

        _config = LoadConfig();

        const string msg = "\x08[ \x0CLITE-VIP \x08] configuration successfully rebooted!";

        if (controller == null)
            Console.WriteLine(msg);
        else
            controller.PrintToChat(msg);
    }

    private HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid.Handle == IntPtr.Zero || @event.Userid.UserId == null) return HookResult.Continue;

        var controller = @event.Userid;

        AddTimer(_config.Delay, () => Timer_Give(controller));

        return HookResult.Continue;
    }

    private void Timer_Give(CCSPlayerController handle)
    {
        if (handle.SteamID == 0) return;
        var steamId = handle.SteamID.ToString();
        var entityIndex = handle.EntityIndex!.Value.Value;
        if (!_config.Users.TryGetValue(steamId, out var user)) return;

        var playerPawnValue = handle.PlayerPawn.Value;
        var moneyServices = handle.InGameMoneyServices;

        if (Users[entityIndex]!.IsHealth) playerPawnValue.Health = user.Health;
        if (Users[entityIndex]!.IsArmor) playerPawnValue.ArmorValue = user.Armor;

        if (Users[entityIndex]!.IsGravity) playerPawnValue.GravityScale = user.Gravity;

        if (playerPawnValue.ItemServices != null)
        {
            if (user.DecoySettings.DecoyTeleport)
                if (Users[entityIndex]!.IsDecoy)
                    if (user.DecoySettings.DecoyCountToBeIssued > 0)
                        for (var i = 0; i < user.DecoySettings.DecoyCountToBeIssued; i++)
                            GiveItem(handle, "weapon_decoy");

            if (Users[entityIndex]!.IsHealthshot)
                if (user.Healthshot > 0)
                {
                    for (var i = 0; i < user.Healthshot; i++)
                        GiveItem(handle, "weapon_healthshot");
                }
                else if (user.Healthshot == 1)
                    GiveItem(handle, "weapon_healthshot");
        }


        if (user.Money != -1)
            if (moneyServices != null)
                moneyServices.Account = user.Money;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Player {handle.PlayerName} has gained Health: {user.Health} | Armor: {user.Armor}");
        Console.ResetColor();
    }

    public static void OnTick(CCSPlayerController clientId)
    {
        if (!clientId.PawnIsAlive)
            return;

        if (!_config.Users.TryGetValue(clientId.SteamID.ToString(), out var user)) return;

        var client = clientId.EntityIndex!.Value.Value;
        var playerPawn = clientId.PlayerPawn.Value;
        var flags = (PlayerFlags)playerPawn.Flags;
        var buttons = clientId.Pawn.Value.MovementServices!.ButtonState.Value;

        if (!Users[client]!.IsJumps) return;

        if ((Users[client]!.LastFlags & PlayerFlags.FL_ONGROUND) != 0 && (flags & PlayerFlags.FL_ONGROUND) == 0 &&
            (Users[client]!.LastButtons & PlayerButtons.Jump) == 0 && (buttons & PlayerButtons.Jump) != 0)
            Users[client]!.JumpsCount++;
        else if ((flags & PlayerFlags.FL_ONGROUND) != 0)
            Users[client]!.JumpsCount = 1;
        else if ((Users[client]!.LastButtons & PlayerButtons.Jump) == 0 && (buttons & PlayerButtons.Jump) != 0 &&
                 Users[client]!.JumpsCount <= user.JumpsCount)
        {
            Users[client]!.JumpsCount++;

            playerPawn.AbsVelocity.Z = 300;
        }

        Users[client]!.LastFlags = flags;
        Users[client]!.LastButtons = buttons;
    }

    private void GiveItem(CCSPlayerController handle, string item)
    {
        handle.GiveNamedItem(item);
    }

    private void CreateMenu()
    {
        var menu = new ChatMenu("\x08--[ \x0CVIP MENU \x08]--");
        menu.AddMenuOption("Health", (player, option) =>
            TogglePlayerFunction(player, Users[player.EntityIndex!.Value.Value]!.IsHealth ^= true, option.Text));
        menu.AddMenuOption("Armor", (player, option) =>
            TogglePlayerFunction(player, Users[player.EntityIndex!.Value.Value]!.IsArmor ^= true, option.Text));
        menu.AddMenuOption("Gravity", (player, _) => AdjustPlayerGravity(player));
        menu.AddMenuOption("Healthshot", (player, option) =>
            TogglePlayerFunction(player, Users[player.EntityIndex!.Value.Value]!.IsHealthshot ^= true, option.Text));
        menu.AddMenuOption("Decoy Teleport", (player, option) =>
            TogglePlayerFunction(player, Users[player.EntityIndex!.Value.Value]!.IsDecoy ^= true, option.Text));
        menu.AddMenuOption("Jumps", (player, option) =>
            TogglePlayerFunction(player, Users[player.EntityIndex!.Value.Value]!.IsJumps ^= true, option.Text));
        AddCommand("css_vip", "command that opens the VIP MENU", (player, _) =>
        {
            if (player == null) return;

            var isVip = _config.Users.ContainsKey(player.SteamID.ToString());
            if (isVip) ChatMenus.OpenMenu(player, menu);
        });
        AddCommand("css_vip_gravity", "command allows you to turn gravity on and off.",
            (player, _) =>
            {
                if (player == null) return;

                var isVip = _config.Users.ContainsKey(player.SteamID.ToString());
                if (isVip) AdjustPlayerGravity(player);
            });
    }

    private void TogglePlayerFunction(CCSPlayerController player, bool func, string name)
    {
        player.PrintToChat(!func ? $"{name}: \x02Off" : $"{name}: \x06On");
    }

    private void AdjustPlayerGravity(CCSPlayerController? controller)
    {
        if (controller == null) return;

        var steamId = controller.SteamID;

        if (!_config.Users.TryGetValue(steamId.ToString(), out var user)) return;

        var gravity = Users[controller.EntityIndex!.Value.Value]!.IsGravity ^= true;

        if (!gravity)
        {
            controller.PrintToChat("Gravity: \x02Off");
            controller.PlayerPawn.Value.GravityScale = 1.0f;
            return;
        }

        controller.PrintToChat("Gravity: \x06On");
        controller.PlayerPawn.Value.GravityScale = user.Gravity;
    }

    private void OnEntitySpawned(CEntityInstance entity)
    {
        if (entity.DesignerName != "smokegrenade_projectile") return;

        var smokeGrenade = new CSmokeGrenadeProjectile(entity.Handle);
        if (smokeGrenade.Handle == IntPtr.Zero) return;

        Server.NextFrame(() =>
        {
            if (!_config.Users.TryGetValue(smokeGrenade.Thrower.Value.Controller.Value.SteamID.ToString(),
                    out var user)) return;

            var split = user.SmokeColor.Split(" ");

            smokeGrenade.SmokeColor.X = user.SmokeColor == "random"
                ? Random.Shared.NextSingle() * 255.0f
                : float.Parse(split[0]);
            smokeGrenade.SmokeColor.Y = user.SmokeColor == "random"
                ? Random.Shared.NextSingle() * 255.0f
                : float.Parse(split[1]);
            smokeGrenade.SmokeColor.Z = user.SmokeColor == "random"
                ? Random.Shared.NextSingle() * 255.0f
                : float.Parse(split[2]);
        });
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
            Admins = new List<ulong>(),
            Delay = 2.0f,
            Users = new Dictionary<string, VipUser>
            {
                {
                    "SteamID64", new VipUser
                    {
                        Health = 100,
                        Armor = 100,
                        Gravity = 1.0f,
                        Money = 1000,
                        SmokeColor = "255 255 255",
                        Healthshot = 1,
                        JumpsCount = 2,
                        DecoySettings = new Decoy
                            { DecoyTeleport = true, DecoyCountInOneRound = 1, DecoyCountToBeIssued = 1 }
                    }
                }
            }
        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("[LITE-VIP] The configuration was successfully saved to a file: " + configPath);
        Console.ResetColor();

        return config;
    }
}

public class UserSettings
{
    public bool IsGravity { get; set; }
    public bool IsHealth { get; set; }
    public bool IsArmor { get; set; }
    public bool IsHealthshot { get; set; }
    public bool IsDecoy { get; set; }
    public bool IsJumps { get; set; }
    public int DecoyCount { get; set; }
    public int JumpsCount { get; set; }
    public PlayerButtons LastButtons { get; set; }
    public PlayerFlags LastFlags { get; set; }
}

public class Config
{
    public List<ulong> Admins { get; set; } = null!;
    public float Delay { get; set; }
    public required Dictionary<string, VipUser> Users { get; set; }
}

public class VipUser
{
    public int Health { get; init; }
    public int Armor { get; init; }
    public float Gravity { get; init; }
    public int Money { get; init; }
    public required string SmokeColor { get; init; }
    public int Healthshot { get; init; }
    public int JumpsCount { get; init; }
    public Decoy DecoySettings { get; init; } = null!;
}

public class Decoy
{
    public bool DecoyTeleport { get; init; }
    public int DecoyCountInOneRound { get; init; }
    public int DecoyCountToBeIssued { get; init; }
}