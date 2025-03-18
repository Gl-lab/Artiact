using Artiact.Models.Api;

namespace Artiact.Services;

public interface IWearCraftTargetFinder
{
    Task<CraftTarget?> FindTarget(List<Item> items);
}