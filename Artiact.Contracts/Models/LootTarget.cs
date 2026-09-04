using Artiact.Contracts.Models.Api;

namespace Artiact.Contracts.Models;

public class LootTarget
{
    public MonsterDatum Monster { get; init; } = null!;
    public string ItemCode { get; init; } = string.Empty;
    public int RequiredQuantity { get; init; }
}
