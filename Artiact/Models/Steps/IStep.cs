using Artiact.Contracts.Client;

namespace Artiact.Models.Steps;

public interface IStep
{
    Task Execute( IGameClient client );
}