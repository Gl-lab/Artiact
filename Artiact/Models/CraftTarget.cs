using Artiact.Models.Api;

namespace Artiact.Services;

public class CraftTarget
{
    public ItemDatum FinalItem { get; set; }
    public List<CraftStep> Steps { get; set; } = new();
}