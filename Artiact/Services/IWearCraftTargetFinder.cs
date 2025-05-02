using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface IWearCraftTargetFinder
{
    Task<List<CraftTarget>> FindTargets( List<Item> items );
}