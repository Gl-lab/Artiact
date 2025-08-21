using System.Diagnostics;
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
    private readonly ActivitySource _activitySource;

    public ActionService( IGameClient client,
                          IGoalService goalService,
                          IStepBuilder stepBuilder,
                          IGoalDecomposer goalDecomposer,
                          ICharacterService characterService,
                          ActivitySource activitySource )
    {
        _client = client;
        _goalService = goalService;
        _stepBuilder = stepBuilder;
        _goalDecomposer = goalDecomposer;
        _characterService = characterService;
        _activitySource = activitySource;
    }

    public async Task Initialize()
    {
        await _client.WarmUpCache();
        _characterService.SaveCharacter( await _client.GetCharacter() );
    }

    public async Task Action()
    {
        for ( int i = 0; i < 5; i++ )
        {
            using Activity? activity = _activitySource.StartActivity( "StartAction" );
            if ( activity == null )
            {
                throw new Exception( "Listener not initialized" );
            }
            activity.AddTag( "characterName", _characterService.GetCharacter().Name );
            try
            {
                Goal goal = _goalService.GetGoal( _characterService );
                await _goalDecomposer.DecomposeGoal( goal, _characterService );
                IStep step = await _stepBuilder.BuildStep( goal, _characterService );
                await step.Execute( _client );
            }
            catch ( Exception e )
            {
                activity.SetStatus( ActivityStatusCode.Error, e.Message );
                throw;
            }
        }
    }
}