using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace Artiact.SmartProxy.Controllers;

[ApiController]
[Route( "my" )]
public class CharacterActionsController : ControllerBase
{
    private readonly IActionService _actionService;

    public CharacterActionsController( IActionService actionService )
    {
        _actionService = actionService;
    }

    [HttpPost( "{name}/action/move" )]
    public ActionResult<ActionResponse> MoveAction( string name, MoveRequest request )
    {
        Character character = _actionService.MoveAction( name, request );
        return new ActionResponse
        {
            Data = new ActionData
            {
                Character = character,
                Cooldown = new Cooldown()
            }
        };
    }

    [HttpPost( "{name}/action/gathering" )]
    public ActionResult<ActionResponse> GatheringAction( string name )
    {
        Character character = _actionService.GatheringAction( name );
        return new ActionResponse
        {
            Data = new ActionData
            {
                Character = character,
                Cooldown = new Cooldown()
            }
        };
    }

    [HttpPost( "{name}/action/crafting" )]
    public ActionResult<ActionResponse> CraftingAction( string name, Item item )
    {
        if ( item.Quantity <= 0 )
        {
            throw new Exception();
        }

        Character character = _actionService.CraftingAction( name, item );
        return new ActionResponse
        {
            Data = new ActionData
            {
                Character = character,
                Cooldown = new Cooldown()
            }
        };
    }
}