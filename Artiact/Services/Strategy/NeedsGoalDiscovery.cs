using System.Collections.Immutable;

namespace Artiact.Services.Strategy;

public sealed class NeedsGoalDiscovery(PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation) => EvaluateAll(observation).First();
    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation)
    {
        try { return Discover(observation); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException or OverflowException)
        { return [Reject("needs", "InvalidNeedsState")]; }
    }
    public static bool Satisfied(StrategyObservation observation, NeedOrder order, NeedsPolicy needs)
    {
        var stock = CharacterObservation.Read(observation.Character) ?? throw new InvalidOperationException();
        long total = stock.Inventory.GetValueOrDefault(order.Code) + (needs.AllowBankWithdrawal ? (long)(observation.Bank?.Items.GetValueOrDefault(order.Code) ?? 0) : 0);
        return total - (needs.Reserved?.GetValueOrDefault(order.Code) ?? 0) >= order.Quantity;
    }
    private ImmutableArray<StrategyCandidate> Discover(StrategyObservation observation)
    {
        if (observation.Orders is not { Valid: true } book) return [Reject("needs", "InvalidOrderObservation")];
        var needs = policy.Needs!;
        var result = ImmutableArray.CreateBuilder<StrategyCandidate>();
        var active = book.Orders.Where(x => x.Status == "Active" && !Satisfied(observation, x, needs)).OrderByDescending(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        foreach (var order in active)
        {
            int floor = needs.Reserved?.GetValueOrDefault(order.Code) ?? 0;
            var goal = new ItemMilestone(order.Code, checked(order.Quantity + floor), 1);
            var derived = policy with { AutonomousGoals = false, Recovery = null, Skills = [],
                Items = active.GroupBy(x => x.Code).Select(x => new ItemMilestone(x.Key, checked(x.Max(y => y.Quantity) + (needs.Reserved?.GetValueOrDefault(x.Key) ?? 0)))).ToImmutableArray(),
                Preparation = needs.AllowCraft ? new() : null, Production = new(needs.Reserved ?? ImmutableDictionary<string, int>.Empty) };
            StrategyCandidate Plan(StrategyObservation state) => new SkillPrerequisiteStrategy(goal, derived, port, !needs.AllowBankWithdrawal).Evaluate(state);
            var candidate = Plan(observation);
            int total = CharacterObservation.Read(observation.Character)!.Inventory.GetValueOrDefault(order.Code) + (needs.AllowBankWithdrawal ? observation.Bank?.Items.GetValueOrDefault(order.Code) ?? 0 : 0);
            result.Add(Bounded(observation, candidate, Plan, new(order.Id, "Order", order.Code, order.Quantity, order.Priority,
                Math.Max(0, goal.Quantity - total), candidate.Prerequisite is { } prerequisite ?
                    $"Заказ {order.Id} → не хватает {order.Code} → нужен навык {prerequisite.Skill} {prerequisite.Target} → подготовка через {prerequisite.TrainingItem ?? "добычу ингредиента"}" :
                    $"Заказ {order.Id} → запас {order.Quantity} {order.Code} сверх защищённого остатка → получение недостающих ингредиентов и предмета", 0, 0, 0, 0, "", 0, Revision: order.Revision)) with { Id = "order:" + order.Id });
            if (needs.AllowBankWithdrawal)
            {
                var localPolicy = derived with { Needs = needs with { AllowBankWithdrawal = false } };
                StrategyCandidate LocalPlan(StrategyObservation state) => new SkillPrerequisiteStrategy(goal, localPolicy, port, true).Evaluate(state);
                result.Add(Bounded(observation, LocalPlan(observation), LocalPlan, result[^1].Need! with
                {
                    Cause = $"Заказ {order.Id} → {order.Code} → получение ингредиентов без изъятия из банка",
                    FullActions = 0, FullSeconds = 0, SliceActions = 0, SliceSeconds = 0, SliceResult = "", Plan = []
                }) with { Id = "order:" + order.Id + ":inventory" });
            }
        }
        if (policy.Recovery is not null)
        {
            var recoveryPolicy = policy with { Needs = null };
            var recoveryObservation = observation.WithContext(observation.Context with { Autonomous =
                observation.Context.Autonomous?.Active?.Skill == "recovery" ? observation.Context.Autonomous : AutonomousRunState.Empty });
            foreach (var candidate in new RecoveryGoalDiscovery(recoveryPolicy, port).EvaluateAll(recoveryObservation.WithContext(recoveryObservation.Context with { RemainingSeconds = null })))
            {
                if (candidate.Discovery is not { } parent) { result.Add(candidate); continue; }
                StrategyCandidate Plan(StrategyObservation state)
                {
                    if (state.Character.GetProperty("hp").GetInt32() >= parent.Target) return candidate with { Complete = true, Command = null };
                    var context = state.Context with { Autonomous = AutonomousRunState.Empty with { Active = parent, ActiveWorld = state.WorldFingerprint }, RemainingSeconds = null };
                    return new RecoveryGoalDiscovery(recoveryPolicy, port).EvaluateAll(state.WithContext(context)).SingleOrDefault(x => x.Id == candidate.Id) ?? Reject(candidate.Id, "RecoveryRouteUnavailable");
                }
                result.Add(Bounded(observation, candidate, Plan, new("hp", "ObservedHp", "hp", parent.Target, 100,
                    parent.Target - observation.Character.GetProperty("hp").GetInt32(), "HP ниже порога → восстановление → разрешённый отдых, пища или её производство с подготовкой", 0, 0, 0, 0, "", 0)));
            }
        }
        return result.Count > 0 ? result.ToImmutable() : [new("needs", "need", 1, 1, 0, 0, "NoActiveSupportedNeeds", true, null)];
    }
    private StrategyCandidate Bounded(StrategyObservation observation, StrategyCandidate candidate,
        Func<StrategyObservation, StrategyCandidate> planner, NeedEvidence evidence)
    {
        if (candidate.Command is null) return candidate with { Need = evidence };
        var estimate = NeedsChainEstimate.Build(observation, planner,
            evidence.Source == "Order" ? policy with { Recovery = null } : policy with { Needs = null });
        if (estimate.Rejection is not null) return candidate with { Rejection = estimate.Rejection, Command = null, Need = evidence };
        var steps = estimate.Steps;
        decimal fullSeconds = steps.Sum(x => x.Seconds) * (policy.Measurement?.UnknownMultiplier ?? 2), seconds = 0;
        int selected = 0, noProgress = observation.Context.NoProgress ?? 0, maximum = observation.Context.MaxNoProgress ?? 10, required = noProgress;
        bool noProgressFailure = false;
        int actionLimit = observation.Context.RemainingActions ?? 100, decisionLimit = observation.Context.RemainingDecisions ?? 100;
        decimal timeLimit = observation.Context.RemainingSeconds ?? 3600;
        for (int i = 0; i < steps.Length; i++)
        {
            // A productive command must be able to enter its tick before the threshold is reached.
            required = Math.Max(required, noProgress + 1);
            if (noProgress >= maximum) noProgressFailure = true;
            noProgress = steps[i].Productive ? 0 : noProgress + 1;
            seconds += steps[i].Seconds * (policy.Measurement?.UnknownMultiplier ?? 2);
            if (i + 1 <= actionLimit && i + 2 <= decisionLimit && seconds <= timeLimit && noProgress < maximum &&
                (steps[i].DurableResult || i == steps.Length - 1)) selected = i + 1;
        }
        if (noProgress >= maximum) { noProgressFailure = true; required = Math.Max(required, noProgress + 1); }
        evidence = evidence with { FullActions = steps.Length, FullSeconds = fullSeconds, SliceActions = selected,
            SliceSeconds = steps.Take(selected).Sum(x => x.Seconds) * (policy.Measurement?.UnknownMultiplier ?? 2),
            SliceResult = selected == 0 ? "None" : selected == steps.Length ? "ParentSatisfied" : steps[selected - 1].Result,
            RequiredNoProgress = required, ConfiguredNoProgress = maximum, Plan = steps.Select(x => x.Result).ToImmutableArray() };
        if (selected == 0) return candidate with { Rejection = noProgressFailure ? "EstimatedNoProgressInsufficient" : "EstimatedPathExceedsBudget", Command = null, Need = evidence };
        // A partial path is permitted only for budget pressure, never to evade an insufficient no-progress policy.
        if (noProgressFailure) return candidate with { Rejection = "EstimatedNoProgressInsufficient", Command = null, Need = evidence };
        var command = candidate.Command;
        return candidate with { Need = evidence, Value = 1, ActionSeconds = Math.Max(1, fullSeconds), TravelSeconds = 0, RecoverySeconds = 0,
            Command = command with { SourceFingerprint = observation.Fingerprint, Dispatch = async token =>
            {
                var reply = await command.Dispatch(token);
                return reply with { State = reply.State.WithOrders(observation.Orders!) };
            } } };
    }
    private static StrategyCandidate Reject(string id, string reason) => new(id, "need", 1, 1, 0, 0, reason, false, null);
}
