using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class CharacterService : ICharacterService
{
    private Character _character;

    public Character GetCharacter()
    {
        return _character;
    }

    public void SaveCharacter( Character character )
    {
        _character = character;
    }
}