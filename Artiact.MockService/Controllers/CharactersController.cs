using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Artiact.SmartProxy.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Artiact.SmartProxy.Controllers;

[ApiController]
[Route( "characters" )]
public class CharactersController : ControllerBase
{
    private readonly ICharacterCache _characterCache;
    private readonly IWebHostEnvironment _environment;

    public CharactersController( ICharacterCache characterCache, IWebHostEnvironment environment )
    {
        _characterCache = characterCache;
        _environment = environment;
    }

    [HttpGet( "{name}" )]
    public ActionResult<CharacterResponse> GetCharacter( string name )
    {
        string jsonPath = Path.Combine( _environment.ContentRootPath, "MockCharacters.json" );
        string jsonContent = System.IO.File.ReadAllText( jsonPath );
        List<CharacterExtension>? characters = JsonSerializer.Deserialize<List<CharacterExtension>>( jsonContent );

        CharacterExtension? character = characters.FirstOrDefault( c => c.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
         ?? characters.FirstOrDefault( c => c.Name.Equals( "NewCharacter", StringComparison.OrdinalIgnoreCase ) );

        if ( character == null )
        {
            return NotFound();
        }

        _characterCache.UpdateCharacter( name, character );
        return new CharacterResponse
        {
            Data = character
        };
    }
}