using Artiact.Contracts.Models.Api;

namespace Artiact.Contracts.Models;

public class CraftTarget
{
    public ItemDatum FinalItem { get; set; }
    public List<CraftStep> Steps { get; set; } = new();
}