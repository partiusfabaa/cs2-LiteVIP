using System.Collections.Generic;

namespace LiteVip;

public class VipGroup
{
    public int? Health { get; init; }
    public int? Armor { get; init; }
    public float? Gravity { get; init; }
    public int? Money { get; init; }
    public string? SmokeColor { get; init; }
    public int? Healthshot { get; init; }
    public int? JumpsCount { get; init; }
    //public bool? Bhop { get; init; }
    public bool? RainbowModel { get; init; }
    public int? Respawn { get; init; }
    public List<string>? Items { get; init; }
    public Decoy? DecoySettings { get; init; }
}

public class Decoy
{
    public bool DecoyTeleport { get; init; }
    public int DecoyCountInOneRound { get; init; }
    public int DecoyCountToBeIssued { get; init; }
}