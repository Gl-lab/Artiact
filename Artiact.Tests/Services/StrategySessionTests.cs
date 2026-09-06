using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Strategy;
using Xunit;

namespace Artiact.Tests.Services;

public class StrategySessionTests
{
    private static StrategyObservation State(int xp = 0, string policy = "p") => new(
        JsonSerializer.SerializeToElement(new { name = "hero", xp, cooldown_expiration = "2000-01-01T00:00:00Z" }),
        ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, policy);
    private sealed class Observer : IStrategyObserver
    {
        public StrategyObservation Current = State();
        public int Reads;
        public Func<int, StrategyObservation>? Read;
        public Task<StrategyObservation> ObserveAsync(CancellationToken token) => Task.FromResult(Read?.Invoke(++Reads) ?? Current);
    }
    private sealed class Delay : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
    private sealed class CandidateStrategy(StrategyCandidate candidate) : IProgressionStrategy
    { public StrategyCandidate Evaluate(StrategyObservation observation) => candidate; }
    private sealed class Strategy(string id, decimal value, Observer observer) : IProgressionStrategy
    {
        public int Calls;
        public bool Lose, Commit = true;
        public bool Defeat, Reject;
        public CancellationTokenSource? Cancel;
        public StrategyCandidate Evaluate(StrategyObservation state) => new(id, id, value, 10, 0, 0, null,
            state.Character.GetProperty("xp").GetInt32() >= 1,
            new(id, state.Fingerprint, true, after => after.Character.GetProperty("xp").GetInt32() > state.Character.GetProperty("xp").GetInt32(),
                token =>
                {
                    Calls++;
                    if (Reject) throw new ActionFailureException(ActionFailureKind.Rejected);
                    if (Commit) observer.Current = State(1);
                    if (Lose) throw new ActionFailureException(ActionFailureKind.UnknownOutcome);
                    Cancel?.Cancel();
                    return Task.FromResult(new StrategyReply(observer.Current, 3, Defeat: Defeat));
                }));
    }

    [Fact]
    public void FingerprintsIgnoreObjectOrderButIncludePolicyAndCatalog()
    {
        var a = new StrategyObservation(JsonDocument.Parse("{\"name\":\"hero\",\"xp\":0}").RootElement,
            ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, "p");
        var b = a.WithCharacter(JsonDocument.Parse("{\"xp\":0,\"name\":\"hero\"}").RootElement);
        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.NotEqual(State().Fingerprint, State(policy: "q").Fingerprint);
    }
    [Fact]
    public async Task HighestScoreWinsThenCompletedCommandIsNotReplayed()
    {
        var observer = new Observer(); var a = new Strategy("a", 10, observer); var b = new Strategy("b", 30, observer);
        var run = new StrategySession(observer, [a, b], new Delay());
        var first = await run.TickAsync();
        Assert.Equal(StrategyStatus.Selected, first.Status); Assert.Equal("b", first.Candidate);
        Assert.Equal(3, first.Candidates.Single(c => c.Id == "b").Score);
        Assert.Equal(StrategyStatus.Completed, (await run.TickAsync()).Status);
        Assert.Equal(0, a.Calls); Assert.Equal(1, b.Calls);
    }
    [Fact]
    public async Task EqualScoresUseOrdinalIdRegardlessOfRegistration()
    {
        var observer = new Observer(); var a = new Strategy("a", 10, observer); var b = new Strategy("b", 10, observer);
        Assert.Equal("a", (await new StrategySession(observer, [b, a], new Delay()).TickAsync()).Candidate);
    }
    [Fact]
    public async Task StalePreflightDispatchesNothingAndConsumesBudget()
    {
        var observer = new Observer { Read = n => n % 2 == 1 ? State() : State(policy: "changed") };
        var strategy = new Strategy("a", 10, observer);
        var run = new StrategySession(observer, [strategy], new Delay(), new(3, 2));
        Assert.Equal(StrategyStatus.Replan, (await run.TickAsync()).Status);
        Assert.Equal(StrategyStatus.Replan, (await run.TickAsync()).Status);
        Assert.Equal(StrategyStatus.Blocked, (await run.TickAsync()).Status);
        Assert.Equal(0, strategy.Calls);
    }
    [Theory]
    [InlineData(true, StrategyStatus.Reconciled)]
    [InlineData(false, StrategyStatus.UnknownOutcome)]
    public async Task LostReplyReconcilesReadOnlyAndNeverReplays(bool committed, StrategyStatus expected)
    {
        var observer = new Observer(); var strategy = new Strategy("a", 10, observer) { Lose = true, Commit = committed };
        var run = new StrategySession(observer, [strategy], new Delay());
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        Assert.Equal(expected, (await run.TickAsync()).Status);
        await run.TickAsync(); Assert.Equal(1, strategy.Calls);
    }
    [Fact]
    public async Task CancellationAfterReplyRetainsStateAndStops()
    {
        using var cts = new CancellationTokenSource(); var observer = new Observer();
        var strategy = new Strategy("a", 10, observer) { Cancel = cts };
        var run = new StrategySession(observer, [strategy], new Delay());
        Assert.Equal(StrategyStatus.Cancelled, (await run.TickAsync(cts.Token)).Status);
        Assert.Equal(1, run.State!.Character.GetProperty("xp").GetInt32());
        await run.TickAsync(); Assert.Equal(1, strategy.Calls);
    }
    [Fact]
    public async Task FailedPostconditionStopsAndDoesNotRefundAttempt()
    {
        var observer = new Observer(); var strategy = new Strategy("a", 10, observer) { Commit = false };
        var run = new StrategySession(observer, [strategy], new Delay());
        var result = await run.TickAsync(); Assert.Equal("InvalidPostcondition", result.Reason); Assert.Equal(1, result.Attempts);
        await run.TickAsync(); Assert.Equal(1, strategy.Calls);
    }
    [Fact]
    public async Task FinalAttemptStillAllowsReadOnlyReconciliation()
    {
        var observer = new Observer(); var strategy = new Strategy("a", 10, observer) { Lose = true };
        var run = new StrategySession(observer, [strategy], new Delay(), new(1, 1));
        await run.TickAsync();
        Assert.Equal(StrategyStatus.Reconciled, (await run.TickAsync()).Status);
        Assert.Equal(StrategyStatus.Blocked, (await run.TickAsync()).Status);
        Assert.Equal(1, strategy.Calls);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OverflowingScoresFailClosed(bool division)
    {
        var candidate = new StrategyCandidate("x", "skill", decimal.MaxValue, division ? 0.1m : decimal.MaxValue,
            division ? 0 : decimal.MaxValue, 0, null, false, null);
        var run = new StrategySession(new Observer(), [new CandidateStrategy(candidate)], new Delay());
        var result = await run.TickAsync();
        Assert.Equal("InvalidPolicy", result.Reason);
        Assert.Contains("InvalidPolicy", JsonSerializer.Serialize(result));
    }
    [Theory]
    [InlineData("catalog")]
    [InlineData("invalid")]
    [InlineData("unrelated")]
    public async Task ChangedWorldOrUnrelatedStateCannotReconcile(string change)
    {
        var observer = new Observer(); var strategy = new Strategy("a", 10, observer) { Lose = true };
        var run = new StrategySession(observer, [strategy], new Delay()); await run.TickAsync();
        observer.Current = change switch
        {
            "catalog" => new StrategyObservation(State(1).Character,
                ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty.Add("items", [JsonSerializer.SerializeToElement(new { code = "new" })]), "p"),
            "invalid" => State().WithCharacter(JsonSerializer.SerializeToElement(new { name = "hero" })),
            _ => State().WithCharacter(JsonSerializer.SerializeToElement(new { name = "hero", xp = 0, other = 1 }))
        };
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        await run.TickAsync(); Assert.Equal(1, strategy.Calls);
    }
    [Fact]
    public async Task ReconciliationDoesNotPermitActionDuringObservedCooldown()
    {
        var observer = new Observer(); var strategy = new Strategy("a", 10, observer) { Lose = true };
        var run = new StrategySession(observer, [strategy], new Delay()); await run.TickAsync();
        observer.Current = State().WithCharacter(JsonSerializer.SerializeToElement(new { name = "hero", xp = 1, cooldown_expiration = "2999-01-01T00:00:00Z" }));
        Assert.Equal(StrategyStatus.Reconciled, (await run.TickAsync()).Status);
        Assert.Equal(StrategyStatus.CoolingDown, (await run.TickAsync()).Status); Assert.Equal(1, strategy.Calls);
    }
    [Fact]
    public async Task CancellationBeforeDispatchReadsAndMutatesNothing()
    {
        using var token = new CancellationTokenSource(); token.Cancel(); var observer = new Observer();
        var strategy = new Strategy("a", 10, observer);
        Assert.Equal(StrategyStatus.Cancelled, (await new StrategySession(observer, [strategy], new Delay()).TickAsync(token.Token)).Status);
        Assert.Equal(0, observer.Reads); Assert.Equal(0, strategy.Calls);
    }
    [Theory]
    [InlineData(true, "Rejected")]
    [InlineData(false, "Defeat")]
    public async Task RejectionAndDefeatAreSticky(bool rejected, string reason)
    {
        var observer = new Observer(); var strategy = new Strategy("a", 10, observer) { Reject = rejected, Defeat = !rejected };
        var run = new StrategySession(observer, [strategy], new Delay());
        Assert.Equal(reason, (await run.TickAsync()).Reason); await run.TickAsync(); Assert.Equal(1, strategy.Calls);
    }
    [Fact]
    public async Task DuplicateIdsBlockAndRejectedGoalsAreNotCompleted()
    {
        var observer = new Observer(); var strategy = new Strategy("a", 10, observer);
        Assert.Equal("InvalidPolicy", (await new StrategySession(observer, [strategy, strategy], new Delay()).TickAsync()).Reason);
        var rejected = new CandidateStrategy(new("x", "skill", 1, 1, 0, 0, "InventoryPressure", false, null));
        var result = await new StrategySession(observer, [rejected], new Delay()).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status); Assert.Equal("InventoryPressure", result.Candidates[0].Rejection);
    }
}
