using Artiact.Contracts.Models.Api;

namespace Artiact.SmartProxy.Services;

public interface IActionService
{
    Character MoveAction( string? characterName, MoveRequest request );
    Character GatheringAction( string? characterName );
    Character CraftingAction( string? characterName, Item item );
}