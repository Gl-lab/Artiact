using System.Diagnostics;
using System.Collections.Immutable;
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
    public async Task FullPathRejectsAnUnreachableLaterWorkshopBeforeGathering()
    {
        await using var h = new Harness(new()); await h.Reset("item-production");
        h.Handler.Corruption = "future-workshop";
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Measurement: new(FullPaths: true));
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status); Assert.Equal(0, h.Handler.Actions);
        Assert.Equal("UnsupportedFullPathEstimate", Assert.Single(result.Candidates).Rejection);
    }
    [Theory]
    [InlineData("item-production", false, false, 6, 32)]
    [InlineData("capacity-training", false, false, 9, 50)]
    [InlineData("consumable-production", true, false, 23, 127)]
    [InlineData("autonomous-weapon", false, true, 20, 120)]
    [InlineData("autonomous-shield", true, true, 38, 221)]
    public async Task FullPathModeCompletesProductionTrainingFoodAndCombatWithVerifiedFacts(string scenario, bool food, bool combat, int actions, int seconds)
    {
        foreach (bool full in new[] { false, true })
        {
            await using var h = new Harness(new()); await h.Reset(scenario);
            var policy = combat ? AutonomousPolicy(scenario == "autonomous-weapon" ? "water_blade" : "ward") : food ? FoodPolicy() :
                new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new(),
                    Bank: new(ImmutableDictionary<string, int>.Empty.Add("bar", 0).Add("ore", 0).Add("tool", 0)), Production: new(ImmutableDictionary<string, int>.Empty));
            if (combat && food) policy = policy with { Consumable = new("combat", "meal", HpBelowPercent: 100, MaxUsed: 8, PreparationSeconds: 0),
                AutonomousCombat = policy.AutonomousCombat! with { Equipment = ["ward", "water_blade"] } };
            policy = policy with { Measurement = full ? new(FullPaths: true) : null };
            var saved = new Checkpoint(); var decisions = new List<StrategyDecision>(); StrategySession? run = null;
            for (int i = 0; i < 95; i++)
            {
                run = h.Factory.Create(policy, new(NoProgress: 60), saved, "full-route");
                var result = await run.TickAsync(); decisions.Add(result);
                if (result.Status != StrategyStatus.Selected) break;
            }
            Assert.True(decisions[^1].Status == StrategyStatus.Completed, System.Text.Json.JsonSerializer.Serialize(decisions[^1]));
            Assert.Equal(actions, decisions[^1].Attempts); Assert.Equal(seconds, decisions[^1].CooldownSeconds);
            Assert.All(saved.Load()!.Journal, x => { Assert.NotNull(x.Facts); Assert.NotNull(x.Facts!.WaitSeconds); if (full) Assert.NotNull(x.MeasurementContext); });
            Assert.All(saved.Load()!.Journal.Where(x => x.Command.StartsWith("Deposit:") || x.Command.StartsWith("Withdraw:")), x => Assert.All(x.Facts!.StockDelta!, y => Assert.Equal(0, y.Value)));
            var consumed = RunPerformance.From(saved.Load()!).ConsumedInputs;
            var expectedInputs = scenario switch
            {
                "item-production" => new Dictionary<string, long> { ["ore"] = 2, ["bar"] = 1 },
                "capacity-training" => new Dictionary<string, long> { ["ore"] = 2, ["bar"] = 1 },
                "consumable-production" => new Dictionary<string, long> { ["ore"] = 2, ["bar"] = 1, ["fish"] = 4, ["meal"] = 2 },
                "autonomous-weapon" => new Dictionary<string, long> { ["ore"] = 2, ["feather"] = 2 },
                _ => new Dictionary<string, long> { ["fish"] = 6, ["meal"] = 4, ["feather"] = 2 }
            };
            Assert.Equal(expectedInputs.OrderBy(x => x.Key), consumed.OrderBy(x => x.Key));
            if (food) Assert.True(saved.Load()!.Journal.Sum(x => x.Facts!.Inputs.GetValueOrDefault("meal")) > 0);
            if (combat) Assert.Equal(20, saved.Load()!.Journal.Sum(x => x.Facts!.Skills.GetValueOrDefault("combat")?.Xp ?? 0));
            if (full) Assert.Contains(decisions.SelectMany(x => x.Candidates), x => x.Path is not null && x.Path.Context is not null);
            if (full && combat && food) Assert.Contains(decisions, d => d.Candidates.Count(c => c.CombatRoute?.Equipment is not null && c.Path is not null) == 2);
        }
    }
    [Fact]
    public async Task AutonomousComparesPreparationCostAcrossSupportedGearAndOpponents()
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var policy = AutonomousPolicy("ward") with { AutonomousCombat = new([new(2, ["dummy"]), new(3, ["guardian", "missing", "missing2"])], ["water_blade", "ward"]) };
        var run = h.Factory.Create(policy);
        for (int i = 0; i < 4; i++) await run.TickAsync();
        var result = await run.TickAsync();
        Assert.Equal("combat:guardian:shield:ward", result.Candidate);
        var routes = result.Candidates.Where(x => x.CombatRoute?.Equipment is not null).ToArray();
        Assert.Equal(2, routes.Length);
        Assert.True(routes.Single(x => x.CombatRoute!.Equipment == "ward").TotalSeconds < routes.Single(x => x.CombatRoute!.Equipment == "water_blade").TotalSeconds);
        Assert.Contains(result.Candidates, x => x.Rejection == "UnsupportedOrZeroXpOpponent:missing");
    }
    [Fact]
    public async Task AutonomousCombatUsesBoundedFoodAndStopsRefillWithParent()
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var policy = AutonomousPolicy("ward") with { Consumable = new("combat", "meal", HpBelowPercent: 100, MaxUsed: 8, PreparationSeconds: 0) };
        var saved = new Checkpoint(); var decisions = new List<StrategyDecision>(); StrategySession? run = null;
        for (int i = 0; i < 90; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 60), saved, "combat-food");
            var result = await run.TickAsync(); decisions.Add(result);
            if (result.Status != StrategyStatus.Selected) break;
        }
        Assert.True(decisions[^1].Status == StrategyStatus.Completed, System.Text.Json.JsonSerializer.Serialize(decisions));
        Assert.Equal(4, decisions.Count(x => x.Command == "Fight"));
        Assert.DoesNotContain(decisions, x => x.Command == "Rest");
        Assert.Equal(4, saved.Load()!.Journal.Sum(x => x.Charges?.GetValueOrDefault("use:meal") ?? 0));
        Assert.Equal(6, saved.Load()!.Journal.Sum(x => x.Charges?.GetValueOrDefault("materials:meal") ?? 0));
        var state = CharacterObservation.Read(run!.State!.Character)!;
        Assert.Equal(2, state.Inventory.GetValueOrDefault("meal") + (run.State.Bank?.Items.GetValueOrDefault("meal") ?? 0));
        int actions = h.Handler.Actions;
        await h.Factory.Create(policy, new(NoProgress: 60), saved, "combat-food").TickAsync(); Assert.Equal(actions, h.Handler.Actions);
    }

    [Theory]
    [InlineData("combat-effects")]
    [InlineData("combat-access")]
    [InlineData("combat-gear-effect")]
    [InlineData("combat-recipe")]
    public async Task AutonomousUnsupportedSecondStageCannotDispatchFight(string corruption)
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var run = h.Factory.Create(AutonomousPolicy("ward"));
        for (int i = 0; i < 4; i++) await run.TickAsync();
        h.Handler.Corruption = corruption;
        Assert.Equal(StrategyStatus.Blocked, (await run.TickAsync()).Status);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Theory]
    [InlineData("loss", StrategyStatus.UnknownOutcome)]
    [InlineData("combat-equip-result", StrategyStatus.Blocked)]
    public async Task AutonomousEquipmentResponseIsVerifiedOrReconciledWithoutReplay(string corruption, StrategyStatus status)
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var saved = new Checkpoint(); var policy = AutonomousPolicy("ward");
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "gear-response");
        for (int i = 0; i < 6; i++) await run.TickAsync();
        h.Handler.Corruption = corruption; Assert.Equal(status, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        var after = await h.Factory.Create(policy, checkpoints: saved, identity: "gear-response").TickAsync();
        Assert.Equal(status == StrategyStatus.UnknownOutcome ? StrategyStatus.Reconciled : StrategyStatus.Blocked, after.Status);
        Assert.Equal(7, h.Handler.Actions);
    }
    [Theory]
    [InlineData("combat-unknown-result")]
    [InlineData("combat-defeat")]
    public async Task AutonomousUnverifiedFightStopsAndNeverReplays(string corruption)
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var policy = AutonomousPolicy("ward"); var saved = new Checkpoint();
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "fight-boundary");
        await run.TickAsync(); h.Handler.Corruption = corruption;
        Assert.Equal(corruption == "combat-defeat" ? StrategyStatus.Blocked : StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        await h.Factory.Create(policy, checkpoints: saved, identity: "fight-boundary").TickAsync();
        Assert.Equal(2, h.Handler.Actions);
    }
    private static PortfolioPolicy AutonomousPolicy(string gear) => new([], 3, "dummy", "", Preparation: new(),
        Bank: new(ImmutableDictionary<string, int>.Empty.Add("feather", 0).Add("meal", 0)),
        Production: new(ImmutableDictionary<string, int>.Empty),
        AutonomousCombat: new([new(2, ["dummy"]), new(3, ["guardian"])], [gear]));

    [Theory]
    [InlineData("autonomous-shield", "ward", 12, 78)]
    [InlineData("autonomous-weapon", "water_blade", 20, 120)]
    public async Task AutonomousTwoStagesPrepareRequiredSlotAndSurviveEveryTickRestart(string scenario, string gear, int actions, int seconds)
    {
        await using var h = new Harness(new()); await h.Reset(scenario);
        var policy = AutonomousPolicy(gear); var saved = new Checkpoint();
        var decisions = new List<StrategyDecision>(); StrategySession? run = null;
        for (int i = 0; i < 40; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 30), saved, "autonomous");
            var decision = await run.TickAsync(); decisions.Add(decision);
            if (decision.Status != StrategyStatus.Selected) break;
        }
        Assert.True(decisions[^1].Status == StrategyStatus.Completed, System.Text.Json.JsonSerializer.Serialize(decisions));
        Assert.Equal(actions, h.Handler.Actions); Assert.Equal(seconds, decisions[^1].CooldownSeconds);
        Assert.Equal(3, run!.State!.Character.GetProperty("level").GetInt32());
        var stock = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(2, stock["feather"]); Assert.Equal(1, stock["protected"]);
        Assert.Contains(decisions.SelectMany(x => x.Candidates), x => x.CombatRoute?.Equipment == gear && x.CombatRoute.PreparationSeconds > 0);
        Assert.Equal(4, decisions.Count(x => x.Command == "Fight"));
        Assert.Equal(gear == "ward" ? 8 : 10, run.State.Character.GetProperty("hp").GetInt32());
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(policy, new(NoProgress: 30), saved, "autonomous").TickAsync()).Status);
        Assert.Equal(actions, h.Handler.Actions);
    }
    [Theory]
    [InlineData("food-effect", 0, "UnsupportedConsumableEffectOrCondition")]
    [InlineData("food-use-schema", 0, null)]
    [InlineData("food-result", 8, null)]
    public async Task UnsupportedOrUnverifiedUseStopsWithoutReplay(string corruption, int before, string? rejection)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var run = h.Factory.Create(FoodPolicy(), new(NoProgress: 60));
        for (int i = 0; i < before; i++) await run.TickAsync();
        h.Handler.Corruption = corruption;
        var result = await run.TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        if (rejection is not null) Assert.Equal(rejection, Assert.Single(result.Candidates).Rejection);
        Assert.Equal(before + (corruption == "food-result" ? 1 : 0), h.Handler.Actions);
        await run.TickAsync();
        Assert.Equal(before + (corruption == "food-result" ? 1 : 0), h.Handler.Actions);
    }

    [Fact]
    public async Task ConsumableCannotSpendAnotherGoalReservedStock()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-bank");
        var policy = FoodPolicy() with { Items = [new("tool", 1), new("meal", 4)] };
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        Assert.Contains(result.Candidates, x => x.Rejection == "ConsumableReserveConflict");
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task LostUseReplyReconcilesExactStockHpAndChargedAllowance()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = FoodPolicy(); var saved = new Checkpoint();
        var run = h.Factory.Create(policy, new(NoProgress: 60), saved, "use-loss");
        for (int i = 0; i < 8; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        var resumed = h.Factory.Create(policy, new(NoProgress: 60), saved, "use-loss");
        Assert.Equal(StrategyStatus.Reconciled, (await resumed.TickAsync()).Status);
        Assert.Equal(9, h.Handler.Actions);
        Assert.Equal(20, resumed.State!.Character.GetProperty("hp").GetInt32());
        Assert.Equal(2, resumed.State.Context.Used["use:meal"]);
        Assert.Equal(0, CharacterObservation.Read(resumed.State.Character)!.Inventory.GetValueOrDefault("meal"));
    }

    [Fact]
    public async Task FullHpAndCompletedParentDoNotUseOrReplenishFood()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-full");
        var saved = new Checkpoint(); var policy = FoodPolicy();
        var run = h.Factory.Create(policy, new(NoProgress: 60), saved, "full-hp");
        for (int i = 0; i < 7; i++) await run.TickAsync();
        Assert.Equal(StrategyStatus.Completed, saved.Load()!.Terminal!.Status);
        Assert.Equal(6, h.Handler.Actions);
        Assert.DoesNotContain(saved.Load()!.Journal, x => x.Command.StartsWith("Use:", StringComparison.Ordinal) || x.Command.StartsWith("Craft:meal", StringComparison.Ordinal));
        await h.Reset("consumable-production");
        var completed = policy with { Items = [new("protected", 1)], Consumable = policy.Consumable! with { ParentItem = "protected" } };
        int before = h.Handler.Actions;
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(completed).TickAsync()).Status);
        Assert.Equal(before, h.Handler.Actions);
    }

    [Theory]
    [InlineData(1, 100, 60, "ResourceBudgetExhausted", 7)]
    [InlineData(100, 10, 5, "BudgetExhausted", 5)]
    public async Task SupplyCannotResetMaterialOrNoProgressBudget(int materials, int uses, int noProgress, string reason, int actions)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = FoodPolicy(); policy = policy with { Consumable = policy.Consumable! with { MaxMaterialUnits = materials, MaxUsed = uses } };
        var run = h.Factory.Create(policy, new(NoProgress: noProgress));
        StrategyDecision? last = null;
        for (int i = 0; i < 30; i++) { last = await run.TickAsync(); if (last.Status == StrategyStatus.Blocked) break; }
        Assert.Equal(reason, last!.Reason); Assert.Equal(actions, h.Handler.Actions);
    }

    [Fact]
    public async Task OptionalRestUsesRemainingStockConservativelyAfterUseCap()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-bank");
        var policy = FoodPolicy(); policy = policy with { Consumable = policy.Consumable! with { MaxUsed = 1, AllowRest = true, Reserve = 1, MinimumStock = 2, TargetStock = 3 } };
        var run = h.Factory.Create(policy, new(NoProgress: 60));
        var commands = new List<string?>();
        for (int i = 0; i < 25; i++) { var decision = await run.TickAsync(); commands.Add(decision.Command); if (decision.Status == StrategyStatus.Completed) break; }
        Assert.Contains("Rest", commands);
        Assert.Equal(1, run.State!.Context.Used["use:meal"]);
        Assert.Equal(20, run.State.Character.GetProperty("hp").GetInt32());
        Assert.Equal(3, run.State.Bank!.Items.GetValueOrDefault("meal") + CharacterObservation.Read(run.State.Character)!.Inventory.GetValueOrDefault("meal"));
    }

    [Fact]
    public async Task SharedCommandRestoresTheSelectedParentCandidate()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        var policy = CapacityPolicy(new("bar", 1), new("tool", 1));
        var saved = new Checkpoint();
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await h.Factory.Create(policy, checkpoints: saved, identity: "shared").TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: saved, identity: "shared").TickAsync()).Status);
        Assert.Equal(1, h.Handler.Actions);
    }

    private static PortfolioPolicy FoodPolicy(bool training = false) => CapacityPolicy(new ItemMilestone("tool", 1)) with
    {
        Bank = new(CapacityPolicy(new ItemMilestone("tool", 1)).Bank!.Retain.Add("meal", 0).Add("snack", 0).Add("fish", 0).Add("baitfish", 0)),
        Preparation = training ? new() : null,
        Consumable = new("tool", "meal", HpBelowPercent: 100)
    };

    [Theory]
    [InlineData("consumable-production", false, 23, 127)]
    [InlineData("consumable-capacity", false, 25, 137)]
    [InlineData("consumable-bank", false, 11, 51)]
    public async Task HealingSupplyUseAndRefillSurviveReconstruction(string scenario, bool training, int actions, int seconds)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = FoodPolicy(training); var saved = new Checkpoint();
        StrategyDecision? last = null; StrategySession? run = null;
        for (int i = 0; i < 80; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 60), saved, "food");
            last = await run.TickAsync();
            if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break;
        }
        Assert.True(last!.Status == StrategyStatus.Completed, last.Reason + ":" + string.Join(',', last.Candidates.Select(x => x.Rejection)));
        Assert.Equal(actions, last.Attempts); Assert.Equal(seconds, last.CooldownSeconds);
        Assert.Equal(20, run!.State!.Character.GetProperty("hp").GetInt32());
        var inventory = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(1, inventory["tool"]); Assert.Equal(1, inventory["protected"]);
        Assert.Equal(2, inventory.GetValueOrDefault("meal") + run.State.Bank!.Items.GetValueOrDefault("meal"));
        Assert.Equal(2, run.State.Context.Used["use:meal"]);
        Assert.Equal(scenario == "consumable-bank" ? 0 : 4, run.State.Context.Used.GetValueOrDefault("materials:meal"));
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(policy, new(NoProgress: 60), saved, "food").TickAsync()).Status);
        Assert.Equal(actions, h.Handler.Actions);
    }

    [Fact]
    public async Task FoodSupplyTrainsCookingAndFishingBeforeMealRecipe()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-training");
        var run = h.Factory.Create(FoodPolicy(true), new(NoProgress: 60));
        StrategyDecision? last = null;
        for (int i = 0; i < 80; i++) { last = await run.TickAsync(); if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break; }
        Assert.True(last!.Status == StrategyStatus.Completed, last.Reason + ":" + string.Join(',', last.Candidates.Select(x => x.Rejection)));
        Assert.True(run.State!.Character.GetProperty("cooking_level").GetInt32() >= 2);
        Assert.True(run.State.Character.GetProperty("fishing_level").GetInt32() >= 2);
        Assert.Equal(20, run.State.Character.GetProperty("hp").GetInt32());
        Assert.Equal(2, run.State.Context.Used["use:meal"]);
    }

    [Theory]
    [InlineData("capacity-production", 9, 5)]
    [InlineData("capacity-production", 4, 5)]
    [InlineData("item-production-bank", 1, 1)]
    public async Task CapacityBankAndCraftRepliesReconcileWithoutRepeatedPost(string scenario, int beforeAction, int quantity)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = CapacityPolicy(new ItemMilestone("tool", quantity));
        var saved = new Checkpoint(); var run = h.Factory.Create(policy, checkpoints: saved, identity: "capacity-loss");
        for (int i = 0; i < beforeAction; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: saved, identity: "capacity-loss").TickAsync()).Status);
        Assert.Equal(beforeAction + 1, h.Handler.Actions);
    }

    [Theory]
    [InlineData("capacity-too-small", "MinimalRecipeExceedsCapacity", 0)]
    [InlineData("bank-full", "BankFull", 8)]
    public async Task ImpossibleCapacityOrFullBankStopsWithoutShuttling(string corruption, string reason, int actions)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        h.Handler.Corruption = corruption;
        var run = h.Factory.Create(CapacityPolicy(new ItemMilestone("tool", 5)));
        StrategyDecision? last = null;
        for (int i = 0; i < 20; i++) { last = await run.TickAsync(); if (last.Status == StrategyStatus.Blocked) break; }
        Assert.Equal(StrategyStatus.Blocked, last!.Status);
        Assert.Equal(reason, Assert.Single(last.Candidates).Rejection);
        Assert.Equal(actions, h.Handler.Actions);
    }

    private static PortfolioPolicy CapacityPolicy(params ItemMilestone[] goals) => new([], 0, "", "", Items: goals.ToImmutableArray(),
        Bank: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 0).Add("bar", 0).Add("tool", 0).Add("protected", 1)),
        Production: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("protected", 1)));

    [Fact]
    public async Task SharedIngredientGoalRetainsItsQuotaWhileAnotherGoalCrafts()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        var run = h.Factory.Create(CapacityPolicy(new("bar", 1), new("tool", 1)));
        StrategyDecision? last = null;
        for (int i = 0; i < 30; i++) { last = await run.TickAsync(); if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break; }
        Assert.Equal(StrategyStatus.Completed, last!.Status);
        var inventory = CharacterObservation.Read(run.State!.Character)!.Inventory;
        Assert.Equal(1, inventory.GetValueOrDefault("bar") + run.State.Bank!.Items.GetValueOrDefault("bar"));
        Assert.Equal(1, inventory.GetValueOrDefault("tool") + run.State.Bank.Items.GetValueOrDefault("tool"));
        Assert.Equal(1, inventory["protected"]);
        Assert.Equal(14, last.Attempts); Assert.Equal(77, last.CooldownSeconds);
    }

    [Fact]
    public async Task SkillPreparationAndLargerOrderShareSmallInventory()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-training");
        var run = h.Factory.Create(CapacityPolicy(new ItemMilestone("tool", 3)) with { Preparation = new() });
        StrategyDecision? last = null;
        for (int i = 0; i < 50; i++) { last = await run.TickAsync(); if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break; }
        Assert.Equal(StrategyStatus.Completed, last!.Status);
        var inventory = CharacterObservation.Read(run.State!.Character)!.Inventory;
        Assert.Equal(3, inventory.GetValueOrDefault("tool") + run.State.Bank!.Items.GetValueOrDefault("tool"));
        Assert.Equal(1, inventory["protected"]);
        Assert.Equal(2, run.State.Character.GetProperty("weaponcrafting_level").GetInt32());
    }

    [Fact]
    public async Task OrderLargerThanInventoryCompletesThroughBankAndNestedBatches()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        var retain = System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 0).Add("bar", 0).Add("tool", 0).Add("protected", 1);
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 5)], Bank: new(retain),
            Production: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("protected", 1)));
        var run = h.Factory.Create(policy);
        StrategyDecision? last = null;
        for (int i = 0; i < 60; i++)
        {
            last = await run.TickAsync();
            if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break;
        }
        Assert.Equal(StrategyStatus.Completed, last!.Status);
        Assert.Equal(5, run.State!.Bank!.Items.GetValueOrDefault("tool") + CharacterObservation.Read(run.State.Character)!.Inventory.GetValueOrDefault("tool"));
        Assert.Equal(1, CharacterObservation.Read(run.State.Character)!.Inventory["protected"]);
        Assert.Equal(42, last.Attempts); Assert.Equal(228, last.CooldownSeconds);
    }

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
            if (path == "/maps" && Corruption == "future-workshop")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["map_id"]!.GetValue<int>() == 3)!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/monsters" && Corruption == "combat-effects" || path == "/maps" && Corruption == "combat-access" ||
                path == "/items" && Corruption is "combat-gear-effect" or "combat-recipe" || path.EndsWith("/action/equip", StringComparison.Ordinal) && Corruption == "combat-equip-result")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                if (Corruption == "combat-effects") node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "guardian")!["effects"] = JsonNode.Parse("[{\"code\":\"poison\",\"value\":1}]");
                if (Corruption == "combat-access") node["data"]!.AsArray().Single(x => x!["map_id"]!.GetValue<int>() == 9)!["access"]!["type"] = "conditional";
                if (Corruption == "combat-gear-effect") node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "ward")!["effects"]![0]!["code"] = "unknown";
                if (Corruption == "combat-recipe") node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "ward")!["craft"]!["items"]![0]!["code"] = "missing_material";
                if (Corruption == "combat-equip-result") node["data"]!["character"]!["res_fire"] = 74;
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.EndsWith("/action/fight", StringComparison.Ordinal) && Corruption is "combat-unknown-result" or "combat-defeat")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["fight"]!["result"] = Corruption == "combat-defeat" ? "loss" : "unknown";
                response.Content = new StringContent(node.ToJsonString());
            }
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
            if (path.StartsWith("/characters/", StringComparison.Ordinal) && Corruption == "capacity-too-small")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["inventory_max_items"] = 2;
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/items" && Corruption == "food-effect")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "meal")!["effects"]![0]!["code"] = "gold";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/openapi.json" && Corruption == "food-use-schema")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["paths"]!.AsObject().Remove("/my/{name}/action/use");
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.EndsWith("/action/use", StringComparison.Ordinal) && Corruption == "food-result")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["character"]!["hp"] = 19;
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
