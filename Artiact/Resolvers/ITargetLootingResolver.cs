using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface ITargetLootingResolver
{
    Task<LootPrerequisite?> Resolve( ItemDatum craftComponent,
                                     int requiredQuantity,
                                     ICharacterService characterService );
}