using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface ITargetLootingResolver
{
    Task<LootTarget?> FindTarget( ItemDatum craftComponent, int requiredQuantity,
                                  ICharacterService characterService );
}
