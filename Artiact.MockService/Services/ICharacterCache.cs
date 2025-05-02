using Artiact.SmartProxy.Models;

namespace Artiact.SmartProxy.Services;

public interface ICharacterCache
{
    void UpdateCharacter( string? characterName,
                          CharacterExtension newData );

    CharacterExtension? GetCharacter( string? characterName );
}