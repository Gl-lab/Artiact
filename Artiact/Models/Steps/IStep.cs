using Artiact.Client;

namespace Artiact.Models;

public interface IStep
{
    Task Execute( IGameClient client );
}