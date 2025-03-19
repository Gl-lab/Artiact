using Artiact.Contracts.Models.Api;

namespace Artiact.Contracts.Models;

public class CraftStep
{
    public ItemDatum Item { get; set; }
    public List<Item> RequiredItems { get; set; }
    public int Quantity { get; set; }
}