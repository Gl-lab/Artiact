using Artiact.Models;

﻿namespace Artiact.Services;

public interface IActionService
{
    Task InitializeAsync( CancellationToken cancellationToken );
    Task<GoalDecision> ExecuteCycleAsync( CancellationToken cancellationToken );
}