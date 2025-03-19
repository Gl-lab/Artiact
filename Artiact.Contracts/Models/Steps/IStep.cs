using Artiact.Contracts.Client;

namespace Artiact.Contracts.Models.Steps;

public interface IStep
{
    Task Execute( IGameClient client );
}