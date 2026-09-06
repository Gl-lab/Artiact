using Artiact.Models;
﻿using System.Diagnostics;
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
    private readonly ILogger<ActionService> _logger;

    public ActionService( IGameClient client,
                          IGoalService goalService,
                          IStepBuilder stepBuilder,
                          IGoalDecomposer goalDecomposer,
                          ICharacterService characterService,
                          ActivitySource activitySource,
                          ILogger<ActionService>? logger = null )
    {
        _client = client;
        _goalService = goalService;
        _stepBuilder = stepBuilder;
        _goalDecomposer = goalDecomposer;
        _characterService = characterService;
        _activitySource = activitySource;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ActionService>.Instance;
    }

    public async Task InitializeAsync( CancellationToken cancellationToken )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _client.WarmUpCache();
        cancellationToken.ThrowIfCancellationRequested();
        _characterService.SaveCharacter( await _client.GetCharacter() );
    }

    public async Task<GoalDecision> ExecuteCycleAsync( CancellationToken cancellationToken )
    {
        cancellationToken.ThrowIfCancellationRequested();
        using Activity? activity = _activitySource.StartActivity( "StartAction" );
        try
        {
            GoalDecision decision = _goalService.Evaluate( _characterService.GetCharacter() );
            ExplainDecision(decision, activity);
            if (decision.Status != GoalDecisionStatus.Selected)
                return decision;

            GatheringGoal goal = new(decision.MiningTargetLevel);
            await _goalDecomposer.DecomposeGoal( goal, _characterService );
            IStep step = await _stepBuilder.BuildStep( goal, _characterService );
            await step.Execute( _client, cancellationToken );
            return decision;
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            throw;
        }
    }
    private void ExplainDecision(GoalDecision decision, Activity? activity)
    {
        List<KeyValuePair<string, object?>> fields = new()
        {
            new("goal.decision.status", decision.Status.ToString()),
            new("goal.decision.reason", decision.ReasonCode),
            new("goal.mining.target_level", decision.MiningTargetLevel)
        };
        if (decision.CurrentMiningLevel is int current)
            fields.Add(new("goal.mining.current_level", current));
        if (decision.InventoryCapacity is int capacity)
        {
            fields.Add(new("goal.inventory.capacity", capacity));
            fields.Add(new("goal.inventory.used", decision.InventoryUsed));
            fields.Add(new("goal.inventory.free", decision.InventoryFree));
            fields.Add(new("goal.inventory.required_free", decision.RequiredFreeInventory));
        }
        foreach (var field in fields)
            activity?.SetTag(field.Key, field.Value);
        _logger.Log(LogLevel.Information, new EventId(1, "GoalDecision"), fields, null,
            static (state, _) => string.Join(", ", state.Select(field => $"{field.Key}={field.Value}")));
    }

}
