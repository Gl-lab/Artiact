using Artiact.Contracts.Models.Api;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public static class BankPrerequisite
{
    public static StrategyCandidate Evaluate(StrategyObservation observation, StrategyCandidate blocked, BankPolicy policy,
        StrategyActionPort port, decimal travel)
    {
        StrategyCandidate Reject(string reason) => blocked with { Rejection = reason };
        try
        {
            var state = CharacterObservation.Read(observation.Character);
            if (state is null || observation.Bank is not { } bank) return Reject("InvalidBankObservation");
            var deposits = state.Inventory.Where(x => policy.Retain.ContainsKey(x.Key) && x.Value > policy.Retain[x.Key])
                .OrderBy(x => x.Key, StringComparer.Ordinal).Take(20)
                .Select(x => new Item { Code = x.Key, Quantity = x.Value - policy.Retain[x.Key] }).ToArray();
            if (deposits.Length == 0) return Reject("NoDepositableStock");
            if (bank.Items.Count + deposits.Count(x => !bank.Items.ContainsKey(x.Code)) > bank.Slots) return Reject("BankFull");
            var maps = observation.Catalogs["maps"];
            if (maps.Select(x => x.GetProperty("map_id").GetInt32()).Distinct().Count() != maps.Length ||
                !StrategyRules.GatheringPlace(maps.Single(x => x.GetProperty("map_id").GetInt32() == state.MapId), state)) return Reject("UnsupportedBankAccess");
            var target = maps.Where(x => StrategyRules.GatheringPlace(x, state) &&
                x.GetProperty("interactions").TryGetProperty("content", out var content) && content.ValueKind == System.Text.Json.JsonValueKind.Object &&
                content.GetProperty("type").GetString() == "bank").OrderBy(x => x.GetProperty("map_id").GetInt32()).FirstOrDefault();
            if (target.ValueKind == System.Text.Json.JsonValueKind.Undefined) return Reject("NoSupportedBank");
            int id = target.GetProperty("map_id").GetInt32();
            AtomicCommand command;
            if (id != state.MapId)
                command = port.Combat(observation, CombatCommand.Move, new(id, state.Layer, "", new(1, 0), true), null, false,
                    after => CharacterObservation.Read(after.Character)?.MapId == id &&
                        CharacterObservation.Preserved(observation.Character, after.Character, "map_id", "x", "y") && SameBank(bank, after.Bank));
            else
            {
                var inventory = state.Inventory.ToBuilder(); var stored = bank.Items.ToBuilder();
                foreach (var item in deposits)
                {
                    inventory[item.Code] -= item.Quantity;
                    if (inventory[item.Code] == 0) inventory.Remove(item.Code);
                    stored[item.Code] = checked(stored.GetValueOrDefault(item.Code) + item.Quantity);
                }
                command = port.Deposit(observation, deposits, after => CharacterObservation.Read(after.Character) is { } changed &&
                    CharacterObservation.Preserved(observation.Character, after.Character, "inventory") &&
                    inventory.Count == changed.Inventory.Count && inventory.All(x => changed.Inventory.GetValueOrDefault(x.Key) == x.Value) &&
                    SameBank(new(bank.Slots, stored.ToImmutable()), after.Bank));
            }
            return blocked with { Rejection = null, Command = command, TravelSeconds = id == state.MapId ? 0 : travel, ActionSeconds = deposits.Length * 3 };
        }
        catch (Exception) { return Reject("InvalidBankStateOrCatalog"); }
    }
    public static bool SameBank(BankSnapshot? a, BankSnapshot? b) => a is null ? b is null : b is not null && a.Slots == b.Slots &&
        a.Items.Count == b.Items.Count && a.Items.All(x => b.Items.GetValueOrDefault(x.Key) == x.Value);
}
