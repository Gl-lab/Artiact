namespace Artiact.Contracts.Models;

public class LootPrerequisite
{
    public string MonsterCode { get; set; } = string.Empty;
    public MapPoint MonsterPoint { get; set; } = null!;
    public string ItemCode { get; set; } = string.Empty;
    public int RequiredQuantity { get; set; }
}
