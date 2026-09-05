using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Artiact.SmartProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace Artiact.SmartProxy.Controllers;

[ApiController]
[Route( "my" )]
public sealed class CharacterActionsController( IMockScenarioStore store ) : ControllerBase
{
    [HttpPost( "{name}/action/move" )]
    public async Task<ActionResult<ActionResponse>> MoveAction( string name )
    {
        using StreamReader reader = new( Request.Body );
        return Result( store.Move( name, await reader.ReadToEndAsync() ) );
    }

    [HttpPost( "{name}/action/gathering" )]
    public ActionResult<ActionResponse> GatheringAction( string name )
    {
        return Result( store.Gather( name ) );
    }


    private ActionResult<ActionResponse> Result( StoreResult<ActionResponse> result )
    {
        if ( result.Value != null ) return Ok( result.Value );
        ProblemDetails details = new() { Status = result.Status };
        details.Extensions[ "code" ] = result.Code;
        return new ObjectResult( details ) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }
}
