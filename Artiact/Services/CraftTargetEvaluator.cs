using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class CraftTargetEvaluator : ICraftTargetEvaluator
{
    public CraftTarget SelectBestTarget( List<CraftTarget> targets, ICharacterService characterService )
    {
        Character character = characterService.GetCharacter();
        return targets.OrderByDescending( t => t.FinalItem.Level ).First();
    }
}