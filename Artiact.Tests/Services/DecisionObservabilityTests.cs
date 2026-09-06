using System.Diagnostics;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Models.Steps;
using Artiact.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Artiact.Tests.Services;

internal sealed class DecisionLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, EventId Event, Dictionary<string,object?> Fields)> Events {get;}=new();
    public IDisposable? BeginScope<TState>(TState state) where TState:notnull => null;
    public bool IsEnabled(LogLevel logLevel)=>true;
    public void Log<TState>(LogLevel logLevel,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
    {
        Events.Add((logLevel,eventId,((IEnumerable<KeyValuePair<string,object?>>)state!).ToDictionary(x=>x.Key,x=>x.Value)));
    }
}

public class DecisionObservabilityTests
{
    public static IEnumerable<object?[]> Cases()=>GoalServiceTests.Cases();
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Decision_EmitsOneExactSafeEventAndMatchingOptionalActivity(int target,Character? snapshot,GoalDecisionReason reason,int? current,int? free)
    {
        if(snapshot is not null) {snapshot.Name="PRIVATE_NAME";snapshot.Account="PRIVATE_ACCOUNT";}
        GoalDecision expected=new GoalService(Options.Create(new GoalSelectionSettings{MiningTargetLevel=target})).Evaluate(snapshot);
        Assert.Equal(reason,expected.Reason);
        Dictionary<string,object?> fields=new()
        {
            ["goal.decision.status"]=expected.Status.ToString(),
            ["goal.decision.reason"]=expected.ReasonCode,
            ["goal.mining.target_level"]=target
        };
        if(current.HasValue) fields.Add("goal.mining.current_level",current.Value);
        if(free.HasValue)
        {
            fields.Add("goal.inventory.capacity",20);fields.Add("goal.inventory.used",20-free.Value);
            fields.Add("goal.inventory.free",free.Value);fields.Add("goal.inventory.required_free",10);
        }
        foreach(bool listen in new[]{false,true})
        {
            using ActivitySource source=new("DecisionObservabilityTests.Source");
            Activity? stopped=null;
            using ActivityListener listener=new(){ShouldListenTo=s=>listen && s==source,
                Sample=(ref ActivityCreationOptions<ActivityContext> _)=>ActivitySamplingResult.AllData,
                ActivityStopped=a=>stopped=a};
            ActivitySource.AddActivityListener(listener);
            Mock<ICharacterService> character=new();character.Setup(x=>x.GetCharacter()).Returns(snapshot!);
            Mock<IGoalService> selector=new();selector.Setup(x=>x.Evaluate(snapshot)).Returns(expected);
            Mock<IStepBuilder> builder=new();
            Mock<IStep> step=new();step.Setup(x=>x.Execute(It.IsAny<IGameClient>(),It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            builder.Setup(x=>x.BuildStep(It.IsAny<Artiact.Contracts.Models.Goal>(),character.Object)).ReturnsAsync(step.Object);
            DecisionLogger<ActionService> logger=new();
            ActionService action=new(Mock.Of<IGameClient>(),selector.Object,builder.Object,Mock.Of<IGoalDecomposer>(),character.Object,source,logger);
            Assert.Same(expected,await action.ExecuteCycleAsync(CancellationToken.None));
            var entry=Assert.Single(logger.Events);
            Assert.Equal(LogLevel.Information,entry.Level);
            Assert.Equal("GoalDecision",entry.Event.Name);
            Assert.Equal(fields.OrderBy(x=>x.Key),entry.Fields.OrderBy(x=>x.Key));
            if(listen) Assert.Equal(fields.OrderBy(x=>x.Key),stopped!.TagObjects.OrderBy(x=>x.Key));
            else Assert.Null(stopped);
        }
    }
}
