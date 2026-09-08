using System.Collections.Immutable;

namespace Artiact.Services.Strategy;

public sealed record ProductionStock(ImmutableDictionary<string, int> Inventory, ImmutableDictionary<string, int> Bank)
{
    public static ItemProductionPlan Plan(StrategyObservation observation, ItemMilestone goal, PortfolioPolicy policy, bool requireInventory = false)
    {
        var state = CharacterObservation.Read(observation.Character)!;
        var available = Available(observation, goal, policy);
        int quantity = policy.Production is null ? goal.Quantity : Math.Min(goal.Quantity - (requireInventory ? 0 : observation.Bank?.Items.GetValueOrDefault(goal.Code) ?? 0),
            checked(state.Inventory.GetValueOrDefault(goal.Code) + 1));
        var bank = policy.Production is not null && !requireInventory ? available.Bank.Remove(goal.Code) : available.Bank;
        return ItemProductionPlan.Build(goal.Code, quantity, available.Inventory, bank,
            observation.Catalogs["items"], observation.Catalogs["resources"],
            policy.PrepareEquipment ? observation.Catalogs["monsters"].Where(x => x.GetProperty("code").GetString() == policy.Monster).ToArray() : null,
            policy.Production is not null, policy.Needs is not null ? 16 : 30);
    }
    public static ProductionStock Available(StrategyObservation observation, ItemMilestone goal, PortfolioPolicy policy)
    {
        var inventory = CharacterObservation.Read(observation.Character)!.Inventory.ToBuilder();
        var bank = (observation.Bank?.Items ?? ImmutableDictionary<string, int>.Empty).ToBuilder();
        if (policy.Needs is { AllowBankWithdrawal: false }) bank.Clear();
        if (policy.Production is null) return new(inventory.ToImmutable(), bank.ToImmutable());
        var reserves = policy.Production.Reserved.ToBuilder();
        foreach (var other in policy.Items.IsDefault ? [] : policy.Items)
            if (other.Code != goal.Code) reserves[other.Code] = Math.Max(reserves.GetValueOrDefault(other.Code), other.Quantity);
        foreach (var reserve in reserves.Where(x => x.Key != goal.Code))
        {
            int kept = Math.Min(bank.GetValueOrDefault(reserve.Key), reserve.Value);
            bank[reserve.Key] = bank.GetValueOrDefault(reserve.Key) - kept;
            inventory[reserve.Key] = Math.Max(0, inventory.GetValueOrDefault(reserve.Key) - (reserve.Value - kept));
        }
        return new(inventory.ToImmutable(), bank.ToImmutable());
    }

    public static BankPolicy Retain(StrategyObservation observation, PortfolioPolicy policy, ItemProductionPlan plan)
    {
        var retain = policy.Bank!.Retain.ToBuilder();
        var nextCraft = plan.Steps.FirstOrDefault(x => x.Kind == "Craft");
        foreach (string code in retain.Keys.ToArray())
            retain[code] = Math.Max(retain[code], checked(policy.Production!.Reserved.GetValueOrDefault(code) +
                (nextCraft?.Ingredients?.GetValueOrDefault(code) ?? 0)));
        return new(retain.ToImmutable());
    }
}
