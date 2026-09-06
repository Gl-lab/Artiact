using System.Text.Json;
using Artiact.SmartProxy.Models;
using Artiact.SmartProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace Artiact.SmartProxy.Controllers;

[ApiController]
[Route( "__mock" )]
public sealed class MockScenarioController( IMockScenarioStore store ) : ControllerBase
{
    [HttpGet( "trace" )]
    public ActionResult<IReadOnlyList<TraceEntry>> Trace()
    {
        StoreResult<IReadOnlyList<TraceEntry>> result = store.GetTrace();
        return result.Value == null ? Problem( result.Code!, result.Status ) : Ok( result.Value );
    }

    [HttpGet( "state/{name}" )]
    public ActionResult<StateSummary> State( string name )
    {
        StoreResult<StateSummary> result = store.GetState( name );
        if ( result.Value != null ) return Ok( result.Value );
        return Problem( result.Code!, result.Status );
    }

    [HttpPost( "reset" )]
    public async Task<ActionResult<ResetSummary>> Reset()
    {
        string body;
        using ( StreamReader reader = new( Request.Body ) )
        {
            body = await reader.ReadToEndAsync();
        }

        if ( !TryReadScenario( body, out string? scenario ) )
        {
            return Problem( "invalid_reset_request", StatusCodes.Status400BadRequest );
        }

        if ( scenario is not ("basic-mining" or "mining-progression") )
        {
            return Problem( "scenario_not_found", StatusCodes.Status404NotFound );
        }

        return Ok( store.Reset( scenario! ) );
    }

    private static bool TryReadScenario( string body, out string? scenario )
    {
        scenario = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse( body );
            if ( document.RootElement.ValueKind != JsonValueKind.Object )
            {
                return false;
            }

            JsonProperty[] properties = document.RootElement.EnumerateObject().ToArray();
            if ( properties.Length != 1 || properties[ 0 ].Name != "scenario" ||
                 properties[ 0 ].Value.ValueKind != JsonValueKind.String )
            {
                return false;
            }

            scenario = properties[ 0 ].Value.GetString();
            return !String.IsNullOrEmpty( scenario );
        }
        catch ( JsonException )
        {
            return false;
        }
    }

    private ObjectResult Problem( string code, int status )
    {
        ProblemDetails details = new()
        {
            Status = status
        };
        details.Extensions[ "code" ] = code;
        return new ObjectResult( details )
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
