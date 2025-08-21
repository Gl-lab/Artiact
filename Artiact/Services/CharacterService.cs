using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class CharacterService : ICharacterService
{
    private Character? _character;

    public Character GetCharacter()
    {
        return _character ?? throw new InvalidOperationException("CharacterService Character is null");
    }

    public void SaveCharacter( Character character )
    {
        _character = character;
    }
}