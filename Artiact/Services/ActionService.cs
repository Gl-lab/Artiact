using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Contracts.Models.Steps;

namespace Artiact.Services;

public interface IActionService
{
    Task Initialize();
    Task Action();
}

public class ActionService : IActionService
{
    private readonly IGameClient _client;
    private readonly IGoalService _goalService;
    private readonly IStepBuilder _stepBuilder;
    private readonly IGoalDecomposer _goalDecomposer;
    private Character _character;

    public ActionService( IGameClient client,
                          IGoalService goalService,
                          IStepBuilder stepBuilder,
                          IGoalDecomposer goalDecomposer )
    {
        _client = client;
        _goalService = goalService;
        _stepBuilder = stepBuilder;
        _goalDecomposer = goalDecomposer;
    }

    public async Task Initialize()
    {
        _character = await _client.GetCharacter();
    }

    public async Task Action()
    {
        for ( int i = 0; i < 2; i++ )
        {
            Goal goal = _goalService.GetGoal( _character );
            await _goalDecomposer.DecomposeGoal( goal, _character );
            IStep step = await _stepBuilder.BuildStep( goal, _character );
            await step.Execute( _client );
        }
    }
}