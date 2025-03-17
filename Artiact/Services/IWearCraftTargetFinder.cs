using Artiact.Models.Api;

namespace Artiact.Services;

public interface IWearCraftTargetFinder
{
    CraftTarget? FindTarget(List<Item> items);
}