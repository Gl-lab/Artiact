using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface IWearCraftTargetFinder
{
    Task<CraftTarget?> FindTarget(List<Item> items);
}