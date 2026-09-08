using Artiact.Services.Operation;
using Microsoft.Extensions.Configuration;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Artiact.Services;
using Artiact.Services.Strategy;
using Artiact.Contracts.Models.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Artiact.Tests.Services;

public class AutonomousGoalDiscoveryTests
{
    [Fact]
    public async Task AnonymizedInventoryPressureRemainsExplainableWhenNearestLevelAlsoCannotFit()
    {
        var state = World(1, 1, true); var raw = JsonNode.Parse(state.Character.GetRawText())!;
        raw["inventory"] = JsonSerializer.SerializeToNode(new[] { new { code = "plant", quantity = 40 }, new { code = "ore", quantity = 2 }, new { code = "ticket", quantity = 1 } });
        raw["mining_max_xp"] = 1000; raw["woodcutting_max_xp"] = 1000;
        var result = await Inspect(new(JsonSerializer.SerializeToElement(raw), state.Catalogs, state.Policy), new(6, 3, 2, 120));
        Assert.Equal(StrategyStatus.Blocked, result.Status); Assert.Equal(0, result.Attempts);
        var nearest = Assert.Single(result.Candidates.Where(x => x.Discovery?.OriginalTarget == 10));
        Assert.Equal("EstimatedInventoryInsufficient", nearest.Rejection);
        Assert.Equal(57, nearest.Feasibility!.FreeUnits); Assert.Equal(77, nearest.Feasibility.RequiredUnits);
        Assert.Equal(20, nearest.Feasibility.DeficitUnits);
        Assert.All(result.Candidates, x => Assert.Null(x.Command));
    }
    [Theory]
    [InlineData(20, true, 100, null, 5)]
    [InlineData(0, true, 100, "BankFull", 0)]
    [InlineData(20, false, 100, "NoSupportedBank", 0)]
    [InlineData(20, true, 4, "EstimatedPathExceedsBudget", 5)]
    public void BankRouteCountsDepositAndRoundTrip(int slots, bool reachable, int actions, string? reason, int required)
    {
        var state = World(); var raw = JsonNode.Parse(state.Character.GetRawText())!;
        raw["inventory_max_items"] = 1;
        var maps = state.Catalogs["maps"];
        if (reachable) maps = maps.Add(JsonSerializer.SerializeToElement(new { map_id = 4, layer = "overworld",
            access = new { type = "standard", conditions = Array.Empty<object>() }, interactions = new { content = new { type = "bank", code = "bank" } } }));
        var policy = Policy with { Bank = new(ImmutableDictionary<string, int>.Empty.Add("wood_drop", 0)) };
        state = new(JsonSerializer.SerializeToElement(raw), state.Catalogs.SetItem("maps", maps), policy.Identity,
            new BankSnapshot(slots, ImmutableDictionary<string, int>.Empty));
        var candidate = new AutonomousGoalDiscovery(policy, new StrategyActionPort(null!, null!), new(100, 10, actions, 10000))
            .EvaluateAll(state).Single(x => x.Id == "skill:woodcutting:wood");
        Assert.Equal(reason, candidate.Rejection);
        if (required > 0)
        {
            Assert.Equal(required, candidate.Feasibility!.RequiredActions);
            Assert.Equal(54, candidate.Feasibility.RequiredSeconds); // 2 gathers (10), 2 moves (14), one-code deposit (3), multiplier 2.
            Assert.Equal(1, candidate.Feasibility.Unloads);
        }
        Assert.Empty(state.Bank!.Items);
        Assert.Empty(CharacterObservation.Read(state.Character)!.Inventory);
    }

    [Fact]
    public void BankEstimateCountsRepeatedUnloadsAndPreservesFloor()
    {
        var state = World(); var raw = JsonNode.Parse(state.Character.GetRawText())!;
        raw["inventory_max_items"] = 3;
        raw["inventory"] = JsonSerializer.SerializeToNode(new[] { new { code = "protected", quantity = 1 } });
        var maps = state.Catalogs["maps"].Add(JsonSerializer.SerializeToElement(new { map_id = 4, layer = "overworld",
            access = new { type = "standard", conditions = Array.Empty<object>() }, interactions = new { content = new { type = "bank", code = "bank" } } }));
        var policy = Policy with { Bank = new(ImmutableDictionary<string, int>.Empty.Add("wood_drop", 0)) };
        state = new(JsonSerializer.SerializeToElement(raw), state.Catalogs.SetItem("maps", maps), policy.Identity,
            new BankSnapshot(1, ImmutableDictionary<string, int>.Empty));
        var estimate = GatheringBankEstimate.Calculate(state, state.Catalogs["resources"][1], 6, policy);
        Assert.Null(estimate.Rejection);
        Assert.Equal(12, estimate.Actions); Assert.Equal(64, estimate.Seconds); Assert.Equal(2, estimate.Unloads);
        Assert.Equal(1, CharacterObservation.Read(state.Character)!.Inventory["protected"]);
    }
    [Fact]
    public void BankPolicyCannotMakeProtectedOutputFit()
    {
        var state = World(); var raw = JsonNode.Parse(state.Character.GetRawText())!;
        raw["inventory_max_items"] = 1;
        var policy = Policy with { Bank = new(ImmutableDictionary<string, int>.Empty.Add("unrelated", 0)) };
        state = new(JsonSerializer.SerializeToElement(raw), state.Catalogs, policy.Identity,
            new BankSnapshot(20, ImmutableDictionary<string, int>.Empty));
        var candidates = new AutonomousGoalDiscovery(policy, new StrategyActionPort(null!, null!), new(100, 10, 100, 10000)).EvaluateAll(state);
        Assert.DoesNotContain(candidates, x => x.Command is not null);
        Assert.Contains(candidates, x => x.Rejection == "NoDepositableStock");
    }
    [Fact]
    public async Task IntermediateEvidenceSurvivesSerializationAndActiveReevaluation()
    {
        var state = World(1, 1, true);
        var result = await Inspect(state, new(100, 10, 2, 10000));
        var chosen = Assert.Single(result.Candidates.Where(x => x.Discovery?.OriginalTarget == 10 && x.Command is not null));
        Assert.Equal("EstimatedPathExceedsBudget", chosen.Discovery!.FallbackReason);
        var active = JsonSerializer.Deserialize<AutonomousRunState>(JsonSerializer.Serialize(
            AutonomousRunState.Empty with { Active = chosen.Discovery, ActiveWorld = state.WorldFingerprint }))!;
        Assert.True(active.Valid);
        var restored = state.WithContext(state.Context with { Autonomous = active });
        var next = new AutonomousGoalDiscovery(Policy, new StrategyActionPort(null!, null!), new(100, 10, 2, 10000)).EvaluateAll(restored).ToArray();
        Assert.Equal(chosen.Id, next.Single(x => x.Command is not null).Id);
        Assert.All(next, x => Assert.Equal(2, x.Discovery!.Target));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, false)]
    public async Task IntermediateUsesSumOfMaximumSimultaneousDrops(int capacity, bool fits)
    {
        var state = World(1, 1, true); var character = JsonNode.Parse(state.Character.GetRawText())!;
        character["inventory_max_items"] = capacity;
        var resource = JsonNode.Parse(state.Catalogs["resources"][0].GetRawText())!;
        resource["drops"]!.AsArray().Add(JsonSerializer.SerializeToNode(new { code = "extra", min_quantity = 1, max_quantity = 1, rate = 2 }));
        state = new(JsonSerializer.SerializeToElement(character), state.Catalogs.SetItem("resources",
            state.Catalogs["resources"].SetItem(0, JsonSerializer.SerializeToElement(resource))), state.Policy);
        var result = await Inspect(state, new(100, 10, 100, 10000));
        var intermediate = Assert.Single(result.Candidates.Where(x => x.Discovery?.OriginalTarget == 10));
        Assert.Equal(fits, intermediate.Command is not null);
        Assert.Equal(4, intermediate.Feasibility!.RequiredUnits);
    }

    [Fact]
    public async Task AlreadyNearestGoalDoesNotGenerateFallbackDuplicates()
    {
        var result = await Inspect(World(), new(10, 1, 1, 1));
        Assert.DoesNotContain(result.Candidates, x => x.Discovery?.OriginalTarget is not null);
        Assert.Equal(result.Candidates.Length, result.Candidates.Select(x => x.Id).Distinct().Count());
    }
    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public async Task UnreachableUnlockFallsBackOnlyToFittingNearestLevel(int capacity, bool fits)
    {
        var state = World(1, 1, true); var character = JsonNode.Parse(state.Character.GetRawText())!;
        character["inventory_max_items"] = capacity;
        var result = await Inspect(new(JsonSerializer.SerializeToElement(character), state.Catalogs, state.Policy), new(100, 10, 100, 10000));
        Assert.Contains(result.Candidates, x => x.Discovery?.Target == 10 && x.Rejection == "EstimatedInventoryInsufficient");
        var intermediate = Assert.Single(result.Candidates.Where(x => x.Discovery?.Skill == "mining" && x.Discovery.Target == 2));
        Assert.Equal(fits, intermediate.Command is not null);
        Assert.Equal(0, intermediate.Discovery!.UnlockUtility);
        Assert.Equal(0.1m, intermediate.Discovery.ProgressUtility);
    }
    [Fact]
    public async Task InventoryRefusalCarriesCalculatedQuantities()
    {
        var state = World(); var character = JsonNode.Parse(state.Character.GetRawText())!;
        character["inventory_max_items"] = 1;
        var result = await Inspect(new(JsonSerializer.SerializeToElement(character), state.Catalogs, state.Policy));
        var candidate = result.Candidates.Single(x => x.Id == "skill:woodcutting:wood");
        var json = JsonSerializer.SerializeToElement(candidate);
        Assert.True(json.TryGetProperty("Feasibility", out var detail), "Planner must retain numeric feasibility evidence");
        Assert.Equal(1, detail.GetProperty("FreeUnits").GetInt32());
        // 26 XP remaining, estimated 13 XP and one maximum drop per action: two units.
        Assert.Equal(2, detail.GetProperty("RequiredUnits").GetDecimal());
        Assert.Equal(1, detail.GetProperty("DeficitUnits").GetDecimal());
        Assert.False(detail.GetProperty("BankConfigured").GetBoolean());
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class DelayCounter : IMiningCooldownDelay
    {
        public int Calls;
        public Task WaitAsync(int seconds, CancellationToken token) { Calls++; return Task.CompletedTask; }
    }
    private sealed class BoundaryOption(Clock clock) : IProgressionStrategy
    {
        public StrategyCandidate Evaluate(StrategyObservation state) => new("skill:mining:ore", "skill", 1, 1, 0, 0, null, false,
            new("Gather:mining", state.Fingerprint, true, _ => true, _ =>
            {
                clock.Now += TimeSpan.FromSeconds(2);
                return Task.FromResult(new StrategyReply(state, 10));
            }), Discovery: new("discovery-v1", "mining", 2, null, 0, 0.1m, 0, [], "synthetic boundary", "target"));
    }
    [Fact]
    public async Task ElapsedBudgetAfterConfirmedPostStopsBeforeStartingCooldownWait()
    {
        var clock = new Clock(); var delay = new DelayCounter();
        var run = new StrategySession(new Observer(World()), [new BoundaryOption(clock)], delay, new(10, 1, 2, 1), time: clock, autonomous: true);
        var result = await run.TickAsync();
        Assert.Equal(StrategyStatus.Stopped, result.Status);
        Assert.Equal("AutonomousBudgetExhausted", result.Reason); Assert.Equal(1, result.Attempts);
        Assert.Equal(0, delay.Calls);
    }
    [Fact]
    public void LegacyRegistrationRejectsAutonomousGoalsBeforeStartingWorker()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Execution:Mode"] = "Legacy", ["Execution:AllowActions"] = "true", ["Portfolio:AutonomousGoals"] = "true",
            ["ApiSettings:BaseUrl"] = "http://localhost", ["ApiSettings:Character"] = "hero",
            ["ApiSettings:Username"] = "mock", ["ApiSettings:Password"] = "mock"
        }).Build();
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddStagedOperation(configuration));
    }

    [Fact]
    public void BoundedRegistrationAcceptsAutonomousGoals()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Execution:Mode"] = "Bounded", ["Portfolio:AutonomousGoals"] = "true"
        }).Build();
        new ServiceCollection().AddStagedOperation(configuration);
    }
    private sealed class Observer(StrategyObservation state) : IStrategyObserver
    {
        public Task<StrategyObservation> ObserveAsync(CancellationToken token) => Task.FromResult(state);
    }
    private sealed class NoDelay : IMiningCooldownDelay
    {
        public Task WaitAsync(int seconds, CancellationToken token) => throw new InvalidOperationException("Inspect cannot wait after dispatch");
    }
    private static PortfolioPolicy Policy => new PortfolioSettings { AutonomousGoals = true }.Policy();
    private static JsonElement Map(int id, string resource, bool accessible = true) => JsonSerializer.SerializeToElement(new
    {
        map_id = id, layer = "overworld", access = new { type = accessible ? "standard" : "conditional", conditions = Array.Empty<object>() },
        interactions = new { content = new { type = "resource", code = resource } }
    });
    private static JsonElement Resource(string code, string skill, int level) => JsonSerializer.SerializeToElement(new
    {
        code, skill, level, drops = new[] { new { code = code + "_drop", min_quantity = 1, max_quantity = 1, rate = 1 } }
    });
    private static StrategyObservation World(int mining = 1, int map = 2, bool unlock = false, bool accessible = true, bool bank = false)
    {
        var character = JsonSerializer.SerializeToElement(new
        {
            name = "hero", map_id = map, layer = "overworld", inventory_max_items = 100, inventory = Array.Empty<object>(),
            mining_level = mining, mining_xp = 0, mining_max_xp = 26, woodcutting_level = 1, woodcutting_xp = 0, woodcutting_max_xp = 26,
            wisdom = 0, cooldown_expiration = "2000-01-01T00:00:00Z"
        });
        return new(character, new Dictionary<string, ImmutableArray<JsonElement>>
        {
            ["resources"] = unlock ? [Resource("ore", "mining", mining), Resource("wood", "woodcutting", 1), Resource("iron", "mining", 10)] :
                [Resource("ore", "mining", mining), Resource("wood", "woodcutting", 1)],
            ["maps"] = [Map(1, "ore"), Map(2, "wood"), Map(3, "iron", accessible)],
            ["items"] = []
        }, Policy.Identity, bank ? new BankSnapshot(20, ImmutableDictionary<string, int>.Empty.Add("ore_drop", 10)) : null);
    }
    private static Task<StrategyDecision> Inspect(StrategyObservation state, StrategyLimits? limits = null)
    {
        var policy = Policy;
        return new StrategySession(new Observer(state), [new AutonomousGoalDiscovery(policy, new StrategyActionPort(null!, null!), limits ?? new(100, 10, 20, 300))],
            new NoDelay(), selection: policy.Measurement).InspectAsync();
    }

    [Theory]
    [InlineData(1, 2, false, true, false, "skill:woodcutting:wood", 2)]
    [InlineData(9, 1, true, true, false, "skill:mining:ore", 10)]
    [InlineData(1, 2, false, true, true, "skill:woodcutting:wood", 2)]
    [InlineData(9, 2, true, false, false, "skill:woodcutting:wood", 2)]
    public async Task PreregisteredStatesSelectUsefulGoalWithoutCombatFields(int mining, int map, bool unlock, bool accessible, bool bank, string expected, int target)
    {
        var result = await Inspect(World(mining, map, unlock, accessible, bank));
        Assert.Equal(StrategyStatus.Selected, result.Status);
        Assert.Equal(expected, result.Candidate);
        var candidate = result.Candidates.Single(x => x.Id == expected);
        Assert.Equal(target, candidate.Discovery!.Target);
        Assert.Equal("discovery-v2", candidate.Discovery.Version);
        Assert.Equal(0, candidate.Discovery.RecipeUtility);
        Assert.NotNull(candidate.Path);
        Assert.Equal(0, result.Attempts);
    }

    [Fact]
    public async Task ReorderedCatalogsPreserveSelectionAndEvidence()
    {
        var state = World(9, 1, true);
        var reordered = new StrategyObservation(state.Character, state.Catalogs.ToDictionary(x => x.Key, x => x.Value.Reverse().ToImmutableArray()), state.Policy);
        var first = await Inspect(state); var second = await Inspect(reordered);
        Assert.Equal(first.Candidate, second.Candidate);
        Assert.Equal(JsonSerializer.Serialize(first.Candidates.Select(x => x.Discovery)), JsonSerializer.Serialize(second.Candidates.Select(x => x.Discovery)));
    }

    [Fact]
    public async Task SmallBudgetRejectsAllRoutesWithoutCommands()
    {
        var result = await Inspect(World(), new(10, 1, 1, 1));
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        Assert.Contains(result.Candidates, x => x.Rejection == "EstimatedPathExceedsBudget");
        Assert.All(result.Candidates, x => Assert.Null(x.Command));
    }

    [Fact]
    public async Task DuplicateCatalogFailsClosed()
    {
        var state = World();
        state = new(state.Character, state.Catalogs.SetItem("resources", [state.Catalogs["resources"][0], state.Catalogs["resources"][0]]), state.Policy);
        Assert.Equal("InvalidDiscoveryCatalog", Assert.Single((await Inspect(state)).Candidates).Rejection);
    }

    [Fact]
    public async Task MissingCatalogDoesNotClaimCompletion()
    {
        var state = World();
        state = new(state.Character, state.Catalogs.Remove("items"), state.Policy);
        Assert.Equal(StrategyStatus.Blocked, (await Inspect(state)).Status);
    }

    [Theory]
    [InlineData("inventory_max_items", 1, "EstimatedInventoryInsufficient")]
    [InlineData("mining_level", 50, "SupportedSkillCapReached")]
    [InlineData("mining_level", 51, "UnsupportedSkillObservation")]
    [InlineData("mining_max_xp", 0, "InvalidMilestone")]
    public async Task CapacityCapAndMalformedProgressAreExplained(string field, int value, string reason)
    {
        var state = World(); var character = JsonNode.Parse(state.Character.GetRawText())!;
        character[field] = value;
        var result = await Inspect(new(JsonSerializer.SerializeToElement(character), state.Catalogs, state.Policy));
        Assert.Contains(result.Candidates, x => x.Rejection == reason && x.Command is null);
    }

    [Fact]
    public async Task UnlockWithUnsupportedDropsCannotEarnUtility()
    {
        var state = World(9, 2, true);
        var resource = JsonNode.Parse(state.Catalogs["resources"][2].GetRawText())!;
        resource["drops"]![0]!["rate"] = 0;
        state = new(state.Character, state.Catalogs.SetItem("resources", state.Catalogs["resources"].SetItem(2, JsonSerializer.SerializeToElement(resource))), state.Policy);
        var result = await Inspect(state);
        Assert.Equal("skill:woodcutting:wood", result.Candidate);
        Assert.All(result.Candidates.Where(x => x.Discovery is not null), x => Assert.Equal(0, x.Discovery!.UnlockUtility));
    }

    [Fact]
    public void ManualIdentityOmitsNewDisabledSettingAndMixedGoalsAreRejected()
    {
        Assert.DoesNotContain("AutonomousGoals", new PortfolioSettings { Skills = [new("mining", 2, 30)] }.Policy().Identity);
        Assert.Throws<ArgumentException>(() => new PortfolioSettings { AutonomousGoals = true, Skills = [new("mining", 2, 30)] }.Policy());
        Assert.Throws<ArgumentException>(() => new PortfolioSettings().Policy());
    }

    [Fact]
    public void EmptyManualPortfolioAcceptsExplicitAutonomousConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AutonomousGoals"] = "true"
        }).Build();
        var settings = new PortfolioSettings();
        configuration.Bind(settings);
        Assert.Empty(settings.Policy().Skills);
    }
}
