using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface ICharacterService
{
    Character GetCharacter();
    void SaveCharacter( Character character );
}