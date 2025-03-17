using Artiact.Models.Api;

namespace Artiact.Services;

public class CraftStep
{
    public ItemDatum Item { get; set; }
    public List<Item> RequiredItems { get; set; }
    public int Quantity { get; set; }
}