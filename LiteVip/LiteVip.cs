using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;

namespace LiteVip;

public class LiteVip : BasePlugin
{
    public override string ModuleName => "LITE-VIP by thesamefabius";
    public override string ModuleVersion => "v1.0.0";

    private Config _config = null!;

    public override void Load(bool hotReload)
    {
        _config = LoadConfig();
        RegisterEventHandler<EventPlayerSpawn>(EventPlayerSpawn);
        RegisterListener<Listeners.OnEntitySpawned>(OnEntitySpawned);
    }

    [ConsoleCommand("css_vip_reload", " ")]
    public void OnCommandReloadConfig(CCSPlayerController? controller, CommandInfo command)
    {
        _config = LoadConfig();

        const string msg = "\x08[ \x0CLITE-VIP \x08] configuration successfully rebooted!";

        if (controller == null)
            Console.WriteLine(msg);
        else
            controller.PrintToChat(msg);
    }

    private void EventPlayerSpawn(EventPlayerSpawn @event)
    {
        if (@event.Userid.Handle == IntPtr.Zero || @event.Userid.UserId == null) return;

        var controller = @event.Userid;

        AddTimer(_config.Delay, () => Timer_Give(controller));
    }

    private void Timer_Give(CCSPlayerController handle)
    {
        if (handle.SteamID == 0) return;
        var steamId = handle.SteamID.ToString();

        if (!_config.Users.TryGetValue(steamId, out var user)) return;

        var playerPawnValue = handle.PlayerPawn.Value;
        var moneyServices = handle.InGameMoneyServices;

        playerPawnValue.Health = user.Health;
        playerPawnValue.ArmorValue = user.Armor;
        playerPawnValue.GravityScale = user.Gravity;

        if (moneyServices != null) moneyServices.Account = user.Money;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Player {handle.PlayerName} has gained Health: {user.Health} | Armor: {user.Armor}");
        Console.ResetColor();
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

            smokeGrenade.SmokeColor.X = float.Parse(split[0]);
            smokeGrenade.SmokeColor.Y = float.Parse(split[1]);
            smokeGrenade.SmokeColor.Z = float.Parse(split[2]);
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
            Delay = 2.0f,
            Users = new Dictionary<string, VipUser>
            {
                {
                    "steamId", new VipUser
                    {
                        Health = 100,
                        Armor = 100,
                        Gravity = 1.0f,
                        Money = 1000,
                        SmokeColor = "255 255 255"
                    }
                }
            }
        };

        File.WriteAllText(configPath, JsonSerializer.Serialize(config));

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("[LITE-VIP] The configuration was successfully saved to a file: " + configPath);
        Console.ResetColor();

        return config;
    }
}

public class Config
{
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
}