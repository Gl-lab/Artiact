using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Models.Steps;

namespace Artiact.Services;

public class ActionService : IActionService
{
    private readonly ICharacterService _characterService;
    private readonly IGameClient _client;
    private readonly IGoalDecomposer _goalDecomposer;
    private readonly IGoalService _goalService;
    private readonly IStepBuilder _stepBuilder;

    public ActionService( IGameClient client,
                          IGoalService goalService,
                          IStepBuilder stepBuilder,
                          IGoalDecomposer goalDecomposer,
                          ICharacterService characterService )
    {
        _client = client;
        _goalService = goalService;
        _stepBuilder = stepBuilder;
        _goalDecomposer = goalDecomposer;
        _characterService = characterService;
    }

    public async Task Initialize()
    {
        await _client.WarmUpCache();
        _characterService.SaveCharacter( await _client.GetCharacter() );
    }

    public async Task Action()
    {
        for ( int i = 0; i < 2; i++ )
        {
            Goal goal = _goalService.GetGoal( _characterService );
            await _goalDecomposer.DecomposeGoal( goal, _characterService );
            IStep step = await _stepBuilder.BuildStep( goal, _characterService );
            await step.Execute( _client );
        }
    }
}