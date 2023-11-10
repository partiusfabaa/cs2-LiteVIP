using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace LiteVip;

public class UserSettings
{
    public bool IsGravity { get; set; }
    public bool IsHealth { get; set; }
    public bool IsArmor { get; set; }
    public bool IsHealthshot { get; set; }
    public bool IsDecoy { get; set; }
    public bool IsJumps { get; set; }
    public bool IsItems { get; set; }
    public bool IsRainbow { get; set; }
    public int DecoyCount { get; set; }
    public int JumpsCount { get; set; }
    public PlayerButtons LastButtons { get; set; }
    public PlayerFlags LastFlags { get; set; }
    public Timer? RainbowTimer { get; set; }
}