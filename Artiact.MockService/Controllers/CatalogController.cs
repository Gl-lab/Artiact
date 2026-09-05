using System.Globalization;
using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Artiact.SmartProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace Artiact.SmartProxy.Controllers;

[ApiController]
public sealed class CatalogController( IMockScenarioStore store ) : ControllerBase
{
    [HttpGet( "/maps" )]
    public ActionResult<Map> Maps()
    {
        StoreResult<Map> result = store.GetMaps();
        if ( result.Value == null ) return Failure<Map>( result );
        if ( !HasPageOne() ) return InvalidPage<Map>();
        return Ok( result.Value );
    }

    [HttpGet( "/resources" )]
    public ActionResult<ResourceResponse> Resources()
    {
        StoreResult<ResourceResponse> result = store.GetResources();
        if ( result.Value == null ) return Failure<ResourceResponse>( result );
        if ( !HasPageOne() ) return InvalidPage<ResourceResponse>();
        return Ok( result.Value );
    }

    [HttpGet( "/items" )]
    public ActionResult<ItemsResponse> Items()
    {
        StoreResult<ItemsResponse> result = store.GetItems();
        if ( result.Value == null ) return Failure<ItemsResponse>( result );
        if ( !HasPageOne() ) return InvalidPage<ItemsResponse>();
        return Ok( result.Value );
    }

    [HttpGet( "/monsters" )]
    public ActionResult<MonstersResponse> Monsters()
    {
        StoreResult<MonstersResponse> result = store.GetMonsters();
        if ( result.Value == null ) return Failure<MonstersResponse>( result );
        if ( !HasPageOne() ) return InvalidPage<MonstersResponse>();
        return Ok( result.Value );
    }

    private bool HasPageOne()
    {
        var values = Request.Query[ "page" ];
        return values.Count == 1 && Int32.TryParse( values[ 0 ], NumberStyles.None, CultureInfo.InvariantCulture, out int page ) && page == 1;
    }

    private ActionResult<T> InvalidPage<T>()
    {
        ProblemDetails details = new() { Status = StatusCodes.Status400BadRequest };
        details.Extensions[ "code" ] = "invalid_page";
        return new ObjectResult( details )
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static ActionResult<T> Failure<T>( StoreResult<T> result ) where T : class
    {
        ProblemDetails details = new() { Status = result.Status };
        details.Extensions[ "code" ] = result.Code;
        return new ObjectResult( details )
        {
            StatusCode = result.Status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
