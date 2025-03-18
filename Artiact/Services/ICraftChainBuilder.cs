using Artiact.Models.Api;

namespace Artiact.Services;

public interface ICraftChainBuilder
{
    Task<CraftTarget?> TryCreateCraftChain(ItemDatum targetItem, Dictionary<string, int> availableResources);
}