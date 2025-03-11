using Artiact.Client;
using Artiact.Models;

namespace Artiact.Services;

public class ActionService
{
    private Character _character;
    private readonly IGameClient _client;
    private readonly IGoalService _goalService;
    private readonly IStepBuilder _stepBuilder;

    public ActionService( IGameClient client,
                          IGoalService goalService,
                          IStepBuilder stepBuilder )
    {
        _client = client;
        _goalService = goalService;
        _stepBuilder = stepBuilder;
    }

    public async Task Initialize()
    {
        _character = await _client.GetCharacter();
    }

    public async Task Action()
    {
        Goal goal = _goalService.GetGoal( _character );
        IStep steps = await _stepBuilder.BuildSteps(goal , _character );
        await steps.Execute( _client );
    }
}