using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Artiact;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.MockService.Tests;

public class StagedOperationTests
{
    [Fact]
    public async Task InaccessibleRequiredResourceDoesNotStartPointlessTraining()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("resource-preparation");
        h.Handler.Corruption = "training-no-access";
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal("NoSupportedResourcePrerequisite", Assert.Single(result.Candidates).Rejection);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Theory]
    [InlineData("training-cycle", 12, "RecipeCycle")]
    [InlineData(null, 1, "PrerequisiteCycleOrDepth")]
    public async Task CyclicOrTooDeepTrainingStopsBeforeAction(string? corruption, int depth, string rejection)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        h.Handler.Corruption = corruption;
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new(MaxDepth: depth));
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        Assert.Equal(rejection, Assert.Single(result.Candidates).Rejection);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingMockRejectsUnmetSkillWithoutMutatingStockOrTrace()
    {
        await using var factory = new MockServiceFactory();
        using var client = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = new("http://localhost") };
        using var reset = await client.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"skill-preparation\"}", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        await client.GetStringAsync("/characters/researcher");
        using var move = await client.PostAsync("/my/researcher/action/move", new StringContent("{\"map_id\":3}", Encoding.UTF8, "application/json"));
        move.EnsureSuccessStatusCode();
        string before = await client.GetStringAsync("/__mock/state/researcher"), trace = await client.GetStringAsync("/__mock/trace");
        using var craft = await client.PostAsync("/my/researcher/action/crafting", new StringContent("{\"code\":\"tool\",\"quantity\":1}", Encoding.UTF8, "application/json"));
        Assert.Equal(422, (int)craft.StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/__mock/state/researcher"));
        Assert.Equal(trace, await client.GetStringAsync("/__mock/trace"));
    }

    [Fact]
    public async Task MissingTrainingSkillSchemaBlocksBeforeDispatch()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        h.Handler.Corruption = "training-schema";
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        Assert.Equal(StrategyStatus.Blocked, (await h.Factory.Create(policy).TickAsync()).Status);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingMaterialCeilingRejectsBeforeSpendingStock()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("item-production-bank");
        h.Handler.Corruption = "training-requirement";
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)], Preparation: new(MaxIngredientUnits: 1));
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        Assert.Equal("NoSupportedTrainingRecipe:weaponcrafting", Assert.Single(result.Candidates).Rejection);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingWithoutXpStopsWithoutAnotherCraft()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var run = h.Factory.Create(policy);
        for (int i = 0; i < 3; i++) await run.TickAsync();
        h.Handler.Corruption = "training-no-xp";
        Assert.Equal("InvalidPostcondition", (await run.TickAsync()).Reason);
        Assert.Equal(StrategyStatus.Blocked, (await run.TickAsync()).Status);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingAndFinalGoalShareActionBudget()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var run = h.Factory.Create(policy, new(Actions: 4));
        for (int i = 0; i < 4; i++) await run.TickAsync();
        Assert.Equal("BudgetExhausted", (await run.TickAsync()).Reason);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Theory]
    [InlineData("skill-preparation", 9, 50, 2, 1)]
    [InlineData("resource-preparation", 7, 40, 1, 1)]
    public async Task ItemGoalTrainsMissingSkillAndCompletesWithoutConfigurationChanges(string scenario, int actions, int seconds, int level, int xp)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var saved = new Checkpoint();
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "preparation");
        var decisions = new List<StrategyDecision>();
        for (int i = 0; i <= actions; i++) decisions.Add(await run.TickAsync());
        Assert.Equal(StrategyStatus.Completed, decisions[^1].Status);
        Assert.Equal(actions, h.Handler.Actions); Assert.Equal(seconds, decisions[^1].CooldownSeconds);
        Assert.Equal(level, run.State!.Character.GetProperty("weaponcrafting_level").GetInt32());
        Assert.Equal(xp, run.State.Character.GetProperty("weaponcrafting_xp").GetInt32());
        var inventory = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(1, inventory["tool"]); Assert.Equal(1, inventory["protected"]);
        Assert.Equal(scenario == "skill-preparation" ? 1 : 0, inventory.GetValueOrDefault("bar"));
        Assert.Equal(scenario == "resource-preparation" ? 2 : 0, inventory.GetValueOrDefault("ore"));
        Assert.Equal(3, inventory.Count);
        string?[] expected = scenario == "skill-preparation"
            ? ["Move:4", "Gather:mining", "Move:3", "Craft:bar:1", "Move:4", "Gather:mining", "Move:3", "Craft:bar:1", "Craft:tool:1", null]
            : ["Move:4", "Gather:mining", "Gather:mining", "Move:5", "Gather:mining", "Move:3", "Craft:tool:1", null];
        Assert.Equal(expected, decisions.Select(x => x.Command));
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(policy, checkpoints: saved, identity: "preparation").TickAsync()).Status);
        Assert.Equal(actions, h.Handler.Actions);
        Assert.Contains(decisions, x => x.Candidates.Any(c => c.Prerequisite?.Parent == "item:tool"));
    }

    [Fact]
    public async Task TrainingReplyLossReconcilesWithoutRepeatingCraft()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var saved = new Checkpoint(); var run = h.Factory.Create(policy, checkpoints: saved, identity: "training-loss");
        for (int i = 0; i < 3; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: saved, identity: "training-loss").TickAsync()).Status);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Fact]
    public async Task MissingCraftSkillSelectsTrainingInsteadOfBlockedRecipe()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("item-production-bank");
        h.Handler.Corruption = "training-requirement";
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)], Preparation: new());
        var run = h.Factory.Create(policy);
        var decision = await run.InspectAsync();
        Assert.Equal(StrategyStatus.Selected, decision.Status);
        // Available bank materials are for the training recipe; preparation is explained before withdrawal.
        var candidate = Assert.Single(decision.Candidates);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(candidate);
        Assert.True(json.TryGetProperty("Prerequisite", out var prerequisite));
        Assert.Equal("weaponcrafting", prerequisite.GetProperty("Skill").GetString());
        Assert.Equal(2, prerequisite.GetProperty("Target").GetInt32());
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task MeasuredResourceAlternativesRetainFullWorldPreflightAndReply()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset();
        h.Handler.Corruption = "resource-alternatives";
        var policy = new PortfolioPolicy([new("mining", 2, 30)], 0, "", "", Measurement: new());
        var run = h.Factory.Create(policy);
        var inspected = await run.InspectAsync();
        Assert.Equal(2, inspected.Candidates.Length); Assert.Equal(0, h.Handler.Actions);
        Assert.All(inspected.Candidates, x => Assert.Equal("Assumed", x.EstimateSource));
        Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        Assert.Equal(2, h.Handler.Actions);
    }
    [Fact]
    public async Task UnsafePreparationNeverUsesFutureWeaponToAuthorizeFight()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("combat-preparation");
        h.Handler.Corruption = "unsafe-preparation";
        Assert.Equal(StrategyStatus.Blocked, (await h.Factory.Create(new([], 3, "dummy", "crafted_blade", PrepareEquipment: true)).TickAsync()).Status);
        Assert.Equal(0, h.Handler.Actions);
    }
    [Fact]
    public async Task PreparationUsesOneBudgetAcrossLootAndCraft()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("combat-preparation");
        var run = h.Factory.Create(new([], 3, "dummy", "crafted_blade", PrepareEquipment: true), new(Actions: 2));
        await run.TickAsync(); await run.TickAsync();
        Assert.Equal("BudgetExhausted", (await run.TickAsync()).Reason); Assert.Equal(2, h.Handler.Actions);
    }
    [Fact]
    public async Task CombatPreparationObtainsTwoLeavesCraftsEquipsAndCompletes()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("combat-preparation");
        var run = h.Factory.Create(new([], 3, "dummy", "crafted_blade", PrepareEquipment: true));
        StrategyDecision? last = null;
        for (int i = 0; i < 14; i++) last = await run.TickAsync();
        Assert.Equal(StrategyStatus.Completed, last!.Status); Assert.Equal(13, h.Handler.Actions); Assert.Equal(81, last.CooldownSeconds);
        var state = CombatObservation.Read(run.State!.Character)!;
        Assert.Equal(3, state.Level); Assert.Equal(17, state.Stats.Hp); Assert.Equal("crafted_blade", state.Weapon);
        Assert.Equal(3, state.Inventory["feather"]); Assert.Equal(3, state.Inventory["shard"]); Assert.Equal(1, state.Inventory["quick_blade"]);
    }
    [Theory]
    [InlineData("item-production-bank", 1)]
    [InlineData("item-production", 4)]
    public async Task LostProductionOrWithdrawalReplyReconcilesAfterRestart(string scenario, int beforeAction)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)]);
        var store = new Checkpoint(); var run = h.Factory.Create(policy, checkpoints: store, identity: "production");
        for (int i = 0; i < beforeAction; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss"; Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: store, identity: "production").TickAsync()).Status);
        Assert.Equal(beforeAction + 1, h.Handler.Actions);
    }
    [Theory]
    [InlineData("item-production", 6, 32)]
    [InlineData("item-production-bank", 5, 25)]
    public async Task ItemGoalProducesNestedRecipeFromGatheredOrBankStock(string scenario, int actions, int seconds)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)]);
        var run = h.Factory.Create(policy);
        StrategyDecision? last = null;
        for (int i = 0; i <= actions; i++) last = await run.TickAsync();
        Assert.Equal(StrategyStatus.Completed, last!.Status); Assert.Equal(actions, h.Handler.Actions); Assert.Equal(seconds, last.CooldownSeconds);
        var state = CharacterObservation.Read(run.State!.Character)!;
        Assert.Equal(1, state.Inventory["tool"]); Assert.Equal(1, state.Inventory["protected"]); Assert.Equal(2, state.Inventory.Count);
        Assert.Empty(run.State.Bank!.Items);
    }
    private static PortfolioPolicy BankPolicy => new([new("mining", 4, 30)], 0, "", "", Bank: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 0)));
    private sealed class Checkpoint : IRunCheckpointStore
    {
        private RunCheckpoint? _saved;
        public RunCheckpoint? Load() => _saved;
        public void Save(RunCheckpoint value) => _saved = value;
    }
    [Fact]
    public async Task IncompleteBankPagesBlockBeforeAction()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        h.Handler.Corruption = "bank-pages";
        Assert.Equal(StrategyStatus.Blocked, (await h.Factory.Create(BankPolicy).TickAsync()).Status);
        Assert.Equal(0, h.Handler.Actions);
    }
    [Fact]
    public async Task LostDepositReplyAfterRestartReconcilesBothInventoriesWithoutPost()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        var store = new Checkpoint(); var run = h.Factory.Create(BankPolicy, checkpoints: store, identity: "bank-test");
        for (int i = 0; i < 4; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        var resumed = h.Factory.Create(BankPolicy, checkpoints: store, identity: "bank-test");
        Assert.Equal(StrategyStatus.Reconciled, (await resumed.TickAsync()).Status);
        Assert.Equal(5, h.Handler.Actions); Assert.Equal(2, resumed.State!.Bank!.Items["ore"]);
    }

    [Theory]
    [InlineData("bank-full", "BankFull")]
    [InlineData("bank-access", "NoSupportedBank")]
    [InlineData("protected-only", "NoDepositableStock")]
    public async Task ImpossibleBankRemediationStopsWithoutDeposit(string corruption, string reason)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        h.Handler.Corruption = corruption;
        var policy = corruption == "protected-only" ? BankPolicy with { Bank = new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 2)) } : BankPolicy;
        var run = h.Factory.Create(policy);
        for (int i = 0; i < 3; i++) await run.TickAsync();
        var blocked = await run.TickAsync();
        Assert.Equal(StrategyStatus.Blocked, blocked.Status); Assert.Equal(reason, Assert.Single(blocked.Candidates).Rejection);
        Assert.Equal(3, h.Handler.Actions);
    }
    [Fact]
    public async Task VerifiedRealClientCheckpointRoundTripsOnDisk()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        var memory = new Checkpoint();
        var run = h.Factory.Create(BankPolicy, checkpoints: memory, identity: "disk");
        await run.TickAsync();
        string directory = Path.Combine(Path.GetTempPath(), "artiact-disk-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var disk = new FileRunCheckpointStore(directory, "hero");
            disk.Save(memory.Load()!);
            Assert.NotNull(disk.Load()!.Verified);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task GatheringBankConservesProtectedStockAcrossTwoDeposits()
    {
        await using var h = new Harness(new(), miningOnly: true);
        await h.Reset("gathering-bank");
        var policy = BankPolicy;
        var store = new Checkpoint();
        var run = h.Factory.Create(policy, checkpoints: store, identity: "bank-completion");
        var decisions = new List<StrategyDecision>();
        for (int i = 0; i < 14; i++) decisions.Add(await run.TickAsync());
        Assert.Equal(StrategyStatus.Completed, decisions[^1].Status);
        Assert.Equal(13, h.Handler.Actions);
        Assert.Equal(71, decisions[^1].CooldownSeconds);
        Assert.Equal(new string?[] { "Move:4", "Gather:mining", "Gather:mining", "Move:6", "Deposit:ore:2", "Move:4",
            "Gather:mining", "Gather:mining", "Move:6", "Deposit:ore:2", "Move:4", "Gather:mining", "Gather:mining", null }, decisions.Select(x => x.Command));
        Assert.Equal(4, run.State!.Bank!.Items["ore"]);
        var character = CharacterObservation.Read(run.State.Character)!;
        Assert.Equal(2, character.Inventory["ore"]); Assert.Equal(1, character.Inventory["protected"]);
        Assert.Equal(4, run.State.Character.GetProperty("mining_level").GetInt32());
        Assert.Equal(0, run.State.Character.GetProperty("mining_xp").GetInt32());
        var resumed = h.Factory.Create(policy, checkpoints: store, identity: "bank-completion");
        Assert.Equal(StrategyStatus.Completed, (await resumed.TickAsync(new CancellationToken(true))).Status);
        Assert.Equal(StrategyStatus.Completed, (await resumed.TickAsync()).Status);
        Assert.Equal(13, h.Handler.Actions);
        Assert.Equal(1, store.Load()!.Initial!.Character.GetProperty("mining_level").GetInt32());
        Assert.Equal(4, store.Load()!.Latest!.Bank!.Items["ore"]);
    }
    [Fact]
    public async Task BoundedMiningCompletesAndRestartDoesNotDispatchAgain()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-bounded-" + Guid.NewGuid().ToString("N"));
        var settings = new ExecutionSettings { Mode = "Bounded", AllowActions = true, RunId = "mining-test", RunDirectory = directory };
        try
        {
            await using (var h = new Harness(settings, miningOnly: true))
            {
                await h.Reset(); h.Handler.MiningOnly = true;
                var result = await h.Runner.RunAsync(CancellationToken.None);
                Assert.Equal(StrategyStatus.Completed, result!.Status); Assert.Equal(3, h.Handler.Actions);
            }
            await using (var h = new Harness(settings, miningOnly: true))
            {
                Assert.Equal(StrategyStatus.Completed, (await h.Runner.RunAsync(CancellationToken.None))!.Status);
                Assert.Equal(0, h.Handler.Actions);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task BoundedSecondExecutorDoesNotDispatch()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-owner-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var lease = new FileRunCheckpointStore(directory, "http://localhost/researcher");
            await using var h = new Harness(new() { Mode = "Bounded", AllowActions = true, RunId = "test", RunDirectory = directory }, miningOnly: true);
            Assert.Null(await h.Runner.RunAsync(CancellationToken.None)); Assert.Equal(0, h.Handler.Actions);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("Inspect", 0)]
    [InlineData("OneShot", 1)]
    public async Task MiningOnlyIgnoresCombatModelAndCombatCatalogs(string mode, int actions)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true;
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(StrategyStatus.Selected, result.Status);
        Assert.Equal("skill:mining", result.Candidate);
        Assert.Single(result.Candidates);
        Assert.Equal(actions, h.Handler.Actions);
        Assert.DoesNotContain("/items", h.Handler.Reads);
        Assert.DoesNotContain("/monsters", h.Handler.Reads);
    }

    [Fact]
    public async Task MiningOnlyMovesThenGathersWithoutCombatNormalization()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true;
        var run = h.Factory.Create(new([new("mining", 2, 30)], 0, "", ""));
        Assert.Equal("Move:4", (await run.TickAsync()).Command);
        Assert.Equal("Gather:mining", (await run.TickAsync()).Command);
        Assert.Equal("Gather:mining", (await run.TickAsync()).Command);
        Assert.Equal(StrategyStatus.Completed, (await run.TickAsync()).Status);
        Assert.Equal(3, h.Handler.Actions);
        Assert.Equal(2, run.State!.Character.GetProperty("mining_level").GetInt32());
    }

    [Theory]
    [InlineData("missing-skill")]
    [InlineData("inventory")]
    [InlineData("access")]
    [InlineData("stale")]
    [InlineData("skill-schema")]
    public async Task MiningOnlyInvalidInputsNeverDispatch(string corruption)
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true; h.Handler.Corruption = corruption;
        Assert.Equal(StrategyStatus.Blocked, (await h.Runner.RunAsync(CancellationToken.None))!.Status);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Theory]
    [InlineData("Inspect", false, 0)]
    [InlineData("OneShot", true, 1)]
    public async Task StagedModesHaveExplicitActionBudgetAndExpiringReadiness(string mode, bool allow, int actions)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = allow }); await h.Reset();
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.NotNull(result); Assert.Equal(StrategyStatus.Selected, result.Status);
        Assert.Equal(actions, h.Handler.Actions); Assert.True(h.State.Snapshot(30).Ready);
        h.Clock.Advance(31); Assert.False(h.State.Snapshot(30).Ready); Assert.Equal("StaleObservation", h.State.Snapshot(30).State);
    }
    [Theory]
    [InlineData("nonsense", true, "http://localhost")]
    [InlineData("OneShot", false, "http://localhost")]
    [InlineData("OneShot", true, "https://api.artifactsmmo.com")]
    [InlineData("OneShot", true, "https://other.invalid")]
    [InlineData("Inspect", false, "http://api.artifactsmmo.com")]
    public async Task InvalidModeConsentOrOriginDispatchesNothing(string mode, bool allow, string url)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = allow }, url);
        Assert.Null(await h.Runner.RunAsync(CancellationToken.None)); Assert.Equal(0, h.Handler.Actions);
        Assert.Equal("ConfigurationRequiredOrInvalid", h.State.Snapshot(30).State); Assert.False(h.State.Snapshot(30).Ready);
    }
    [Theory]
    [InlineData("version")]
    [InlineData("schema")]
    [InlineData("route")]
    [InlineData("malformed")]
    [InlineData("stale")]
    public async Task DriftAndStaleObservationBlockBeforeDispatch(string corruption)
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        h.Handler.Corruption = corruption;
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.Equal(StrategyStatus.Blocked, result!.Status); Assert.Equal(0, h.Handler.Actions); Assert.False(h.State.Snapshot(30).Ready);
        Assert.Equal(corruption == "stale" ? "StaleObservation" : "ApiContractUnavailableOrDrift", h.State.Snapshot(30).State);
    }
    [Fact]
    public async Task OneShotLostResponseOnlyReconcilesReadOnly()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        h.Handler.Corruption = "loss";
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.Equal(StrategyStatus.Reconciled, result!.Status); Assert.Equal(1, h.Handler.Actions);
    }
    [Fact]
    public async Task OneShotCancelledDuringAuthNeverDispatchesAction()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        using var cancel = new CancellationTokenSource(); h.Handler.Cancel = cancel;
        var result = await h.Runner.RunAsync(cancel.Token);
        Assert.Equal(StrategyStatus.Cancelled, result!.Status); Assert.Equal(0, h.Handler.Actions); Assert.False(h.State.Snapshot(30).Ready);
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }
    private sealed class Harness : IAsyncDisposable
    {
        private readonly MockServiceFactory _factory = new();
        private readonly HttpClient _transport;
        public readonly Clock Clock = new();
        public readonly Handler Handler;
        public readonly OperationState State;
        public readonly StagedExecution Runner;
        public readonly StrategySessionFactory Factory;
        public Harness(ExecutionSettings execution, string origin = "http://localhost", bool miningOnly = false)
        {
            State = new(Clock); Handler = new(_factory.Server.CreateHandler(), Clock);
            _transport = new(Handler);
            var api = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
            var http = new GameHttpClient(new ClientFactory(_transport), api, Clock, (_, _) => Task.CompletedTask);
            var client = new GameClient(http, api, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("Staged"));
            var factory = new StrategySessionFactory(client, new CombatCatalog(http), new CharacterService(), new NoDelay(), new ApiCompatibility(http, execution, State, Clock));
            Factory = factory;
            api.BaseUrl = origin;
            Runner = new(execution, api, miningOnly ? new() { Skills = [new("mining", 2, 30)] } : new() { Skills = [new("mining", 2, 30), new("woodcutting", 2, 20)], CombatTarget = 2, Monster = "dummy", Equipment = "quick_blade" }, factory, State);
        }
        public async Task Reset(string scenario = "strategy-portfolio")
        {
            using var response = await _transport.PostAsync("/__mock/reset", new StringContent(System.Text.Json.JsonSerializer.Serialize(new { scenario }), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }
        public async ValueTask DisposeAsync() { _transport.Dispose(); await _factory.DisposeAsync(); }
    }
    private sealed class Handler(HttpMessageHandler inner, Clock clock) : DelegatingHandler(inner)
    {
        public int Actions;
        public bool MiningOnly;
        public List<string> Reads = [];
        public string? Corruption;
        public CancellationTokenSource? Cancel;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await base.SendAsync(request, token);
            string path = request.RequestUri!.AbsolutePath;
            if (path == "/resources" && Corruption == "resource-alternatives")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var resource in node["data"]!.AsArray()) resource!["skill"] = "mining";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.StartsWith("/characters/", StringComparison.Ordinal) && Corruption == "unsafe-preparation")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                node["data"]!["attack_fire"] = 0; response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/my/bank" && Corruption == "bank-full")
                response.Content = new StringContent("{\"data\":{\"slots\":0}}");
            if (path == "/my/bank/items" && Corruption == "bank-pages")
                response.Content = new StringContent("{\"data\":[],\"page\":1,\"pages\":1,\"total\":1,\"size\":50}");
            if (path == "/maps" && Corruption == "bank-access")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var map in node["data"]!.AsArray().Where(x => x!["map_id"]!.GetValue<int>() == 6)) map!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (request.Method == HttpMethod.Get) Reads.Add(path);
            if (path == "/items" && Corruption == "training-requirement")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                foreach (var item in node["data"]!.AsArray())
                {
                    if (item!["code"]!.GetValue<string>() == "tool") item["craft"]!["level"] = 2;
                    if (item["code"]!.GetValue<string>() == "bar") item["level"] = 1;
                }
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.EndsWith("/action/crafting", StringComparison.Ordinal) && Corruption == "training-no-xp")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["character"]!["weaponcrafting_xp"] = 0;
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/openapi.json" && Corruption == "training-schema")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!.AsObject().Remove("weaponcrafting_xp");
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/items" && Corruption == "training-cycle")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "bar")!["craft"]!["items"]![0]!["code"] = "tool";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/maps" && Corruption == "training-no-access")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["map_id"]!.GetValue<int>() == 5)!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && (path.StartsWith("/characters/", StringComparison.Ordinal) || path.Contains("/action/")))
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                var character = path.Contains("/action/") ? node["data"]!["character"]! : node["data"]!;
                character["attack_water"] = 25;
                if (Corruption == "missing-skill") character.AsObject().Remove("mining_xp");
                if (Corruption == "inventory") character["inventory_max_items"] = character["inventory"]!.AsArray().Sum(x => x!["quantity"]!.GetValue<int>());
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && path == "/openapi.json")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var action in new[] { "fight", "rest", "equip", "unequip", "crafting" })
                    node["paths"]!.AsObject().Remove("/my/{name}/action/" + action);
                node["paths"]!.AsObject().Remove("/items"); node["paths"]!.AsObject().Remove("/monsters");
                node["components"]!["schemas"]!["MapLayer"] = JsonNode.Parse("{\"type\":\"string\",\"enum\":[\"overworld\",\"underground\",\"interior\"]}");
                foreach (var schema in new[] { "CharacterSchema", "MapSchema" })
                    node["components"]!["schemas"]![schema]!["properties"]!["layer"] = JsonNode.Parse("{\"$ref\":\"#/components/schemas/MapLayer\"}");
                if (Corruption == "skill-schema") node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!.AsObject().Remove("mining_xp");
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && path == "/maps" && Corruption == "access")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var map in node["data"]!.AsArray()) map!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/token") Cancel?.Cancel();
            if (path.Contains("/action/"))
            { Actions++; if (Corruption == "loss") { response.Dispose(); throw new HttpRequestException(); } }
            if (path.StartsWith("/characters/", StringComparison.Ordinal) && Corruption == "stale") clock.Advance(31);
            if (path == "/openapi.json" && Corruption is "version" or "schema" or "route" or "malformed")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                if (Corruption == "version") node["info"]!["version"] = "changed";
                if (Corruption == "schema") node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!["hp"]!["type"] = "string";
                if (Corruption == "route") node["paths"]!.AsObject().Remove("/my/{name}/action/fight");
                response.Content = new StringContent(Corruption == "malformed" ? "{" : node.ToJsonString());
            }
            return response;
        }
    }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class EmptyCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => Task.CompletedTask;
    }
    private sealed class NoDelay : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
}
