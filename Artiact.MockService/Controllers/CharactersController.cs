using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace Artiact.SmartProxy.Controllers;

[ApiController]
[Route( "characters" )]
public sealed class CharactersController( IMockScenarioStore store ) : ControllerBase
{
    [HttpGet( "{name}" )]
    public ActionResult<CharacterResponse> GetCharacter( string name )
    {
        var result = store.GetCharacter( name );
        if ( result.Value == null )
        {
            ProblemDetails details = new() { Status = result.Status };
            details.Extensions[ "code" ] = result.Code;
            return new ObjectResult( details )
            {
                StatusCode = result.Status,
                ContentTypes = { "application/problem+json" }
            };
        }

        return Ok( new CharacterResponse { Data = result.Value } );
    }
}