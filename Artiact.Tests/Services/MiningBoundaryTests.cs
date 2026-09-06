using System.Diagnostics;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Models.Steps;
using Artiact.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Artiact.Tests.Services;

public class MiningBoundaryTests
{
    public static IEnumerable<object[]> Responses()
    {
        yield return new object[]{"target",GoalServiceTests.Snapshot(20),GoalDecisionReason.MiningTargetReached};
        yield return new object[]{"pressure",GoalServiceTests.Snapshot(19,11),GoalDecisionReason.InventoryPressure};
        yield return new object[]{"negative_level",GoalServiceTests.Snapshot(-1),GoalDecisionReason.InvalidCharacterSnapshot};
        int i=0;
        foreach(Character c in GoalServiceTests.Malformed())
            yield return new object[]{"malformed_"+i++,c,GoalDecisionReason.InvalidInventorySnapshot};
    }
    public static IEnumerable<object[]> BoundaryCases() => Responses().SelectMany(row=>new[]{false,true}.Select(move=>row.Append((object)move).ToArray()));

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public async Task Step_UsesLatestResponseBeforeFirstAndEveryRepeat(string name,Character response,GoalDecisionReason reason,bool move)
    {
        Assert.NotEmpty(name);
        Fixture f = new(response,move);
        var step = await f.Builder.BuildStep(new GatheringGoal(20),f.Character);
        await step.Execute(f.Client.Object,CancellationToken.None);
        Assert.Same(response,f.Character.GetCharacter());
        Assert.Equal(move ? 0 : 1,f.Gathers);
        Assert.Equal(reason,f.Selector.Evaluate(f.Character.GetCharacter()).Reason);
        f.Map.Verify(x=>x.GetByContentCode(It.Is<ContentCode>(c=>c.Value=="best")),Times.Once);
        f.Client.Verify(x=>x.Move(It.IsAny<MapPoint>()),move ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Step_ZeroQuantityBlankSlotsRemainValid(string? code)
    {
        Character response = GoalServiceTests.Snapshot(20);
        Fixture f = new(response,false);
        f.Character.GetCharacter().Inventory.Add(new(){Code=code!,Quantity=0});
        Assert.Equal(GoalDecisionStatus.Selected,f.Selector.Evaluate(f.Character.GetCharacter()).Status);
        var step = await f.Builder.BuildStep(new GatheringGoal(20),f.Character);
        await step.Execute(f.Client.Object,CancellationToken.None);
        Assert.Equal(1,f.Gathers);
    }

    [Fact]
    public async Task Step_UsesSelectedTargetAndRepeatsWhileLatestResponseRemainsAuthorized()
    {
        CharacterService character = new();
        character.SaveCharacter(GoalServiceTests.Snapshot(19, 10));
        Mock<IGameClient> client = new(MockBehavior.Strict);
        Mock<IMapService> map = new(MockBehavior.Strict);
        map.Setup(x => x.GetByContentCode(It.IsAny<ContentCode>()))
           .ReturnsAsync(new MapPoint { X = 0, Y = 0 });
        client.Setup(x => x.GetResources()).ReturnsAsync(new List<ResourceDatum>
        {
            new() { Code = "best", Skill = "mining", Level = 19 }
        });
        Queue<Character> responses = new(new[]
        {
            GoalServiceTests.Snapshot(20, 10),
            GoalServiceTests.Snapshot(27, 10)
        });
        client.Setup(x => x.Gathering()).ReturnsAsync(() => new ActionResponse
        {
            Data = new ActionData
            {
                Character = responses.Dequeue(),
                Cooldown = new Cooldown { TotalSeconds = 0 }
            }
        });
        StepBuilder builder = new(client.Object, map.Object);

        IStep step = await builder.BuildStep(new GatheringGoal(27), character);
        await step.Execute(client.Object, CancellationToken.None);

        client.Verify(x => x.Gathering(), Times.Exactly(2));
        Assert.Equal(27, character.GetCharacter().MiningLevel);
        Assert.Empty(responses);
    }

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public async Task Worker_ResponseBoundaryReturnsNormallyThenTerminates(string name,Character response,GoalDecisionReason reason,bool move)
    {
        Assert.NotEmpty(name);
        Fixture f = new(response,move);
        using CancellationTokenSource stop = new();
        using ActivitySource source = new("MiningBoundaryTests.Flow");
        DecisionLogger<ActionService> logger = new();
        ActionService action = new(f.Client.Object,f.Selector,f.Builder,
            new GoalDecomposer(NullLogger<GoalDecomposer>.Instance,Mock.Of<IWearCraftTargetFinder>(),source),f.Character,source,logger);
        CountingCycles cycles = new(action,stop);
        using ServiceProvider provider = new ServiceCollection().AddSingleton<IActionService>(cycles).BuildServiceProvider();
        int delays=0;
        using ArtiactBackgroundService worker = new(NullLogger<ArtiactBackgroundService>.Instance,provider,
            (_,_)=>{delays++;stop.Cancel();return Task.CompletedTask;});
        await worker.StartAsync(stop.Token);
        await worker.ExecuteTask!;
        Assert.Equal(2,cycles.Calls);
        Assert.Equal(2,logger.Events.Count);
        Assert.Equal(0,delays);
        Assert.Equal(new[]{GoalDecisionStatus.Selected,reason==GoalDecisionReason.MiningTargetReached ? GoalDecisionStatus.Completed : GoalDecisionStatus.Blocked},cycles.Decisions.Select(d=>d.Status));
        Assert.Equal(reason,cycles.Decisions[1].Reason);
        Assert.Same(response,f.Character.GetCharacter());
        Assert.Equal(move ? 0 : 1,f.Gathers);
    }

    private sealed class CountingCycles(ActionService action,CancellationTokenSource stop) : IActionService
    {
        public int Calls {get;private set;}
        public List<GoalDecision> Decisions {get;}=new();
        public Task InitializeAsync(CancellationToken token)=>Task.CompletedTask;
        public async Task<GoalDecision> ExecuteCycleAsync(CancellationToken token)
        {
            Calls++;
            if(Calls>2) {stop.Cancel();throw new InvalidOperationException("Unexpected third cycle");}
            GoalDecision decision=await action.ExecuteCycleAsync(token);
            Decisions.Add(decision);
            return decision;
        }
    }

    private sealed class Fixture
    {
        public CharacterService Character {get;}=new();
        public Mock<IGameClient> Client {get;}=new(MockBehavior.Strict);
        public Mock<IMapService> Map {get;}=new(MockBehavior.Strict);
        public GoalService Selector {get;}=new(Options.Create(new GoalSelectionSettings{MiningTargetLevel=20}));
        public StepBuilder Builder {get;}
        public int Gathers {get;private set;}
        public Fixture(Character response,bool move)
        {
            Character.SaveCharacter(GoalServiceTests.Snapshot());
            MapPoint point=new() {X=move ? 1 : 0,Y=0};
            Map.Setup(x=>x.GetByContentCode(It.IsAny<ContentCode>())).ReturnsAsync(point);
            Client.Setup(x=>x.GetResources()).ReturnsAsync(new List<ResourceDatum>{
                new(){Code="lower",Skill="mining",Level=1},new(){Code="best",Skill="mining",Level=19},
                new(){Code="higher",Skill="mining",Level=20},new(){Code="wood",Skill="woodcutting",Level=19}});
            Client.Setup(x=>x.Move(point)).ReturnsAsync(Response(response));
            Client.Setup(x=>x.Gathering()).Returns(()=>
            {
                Gathers++;
                if(move || Gathers>1) throw new InvalidOperationException("Unauthorized gather");
                return Task.FromResult(Response(response));
            });
            Builder=new(Client.Object,Map.Object);
        }
        private static ActionResponse Response(Character c)=>new(){Data=new(){Character=c,Cooldown=new(){TotalSeconds=0}}};
    }
}
