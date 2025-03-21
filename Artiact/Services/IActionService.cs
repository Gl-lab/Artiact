namespace Artiact.Services;

public interface IActionService
{
    Task Initialize();
    Task Action();
}