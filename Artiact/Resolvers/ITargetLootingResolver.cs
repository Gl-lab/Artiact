using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface ITargetLootingResolver
{
    Task<bool> CanLooting( ItemDatum informationAboutCraftComponent, ICharacterService characterService );
}