using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record GatheringBankEstimate(string? Rejection, int Actions, decimal Seconds, int Unloads)
{
    public const string Version = "gathering-bank-v1";

    public static GatheringBankEstimate Calculate(StrategyObservation observation, JsonElement resource, decimal work, PortfolioPolicy policy)
    {
        int actions = 0, unloads = 0; decimal seconds = 0;
        GatheringBankEstimate Reject(string reason) => new(reason, actions, seconds, unloads);
        try
        {
            var state = CharacterObservation.Read(observation.Character);
            if (state is null || observation.Bank is not { } bank || policy.Bank is null || bank.Slots < 0 ||
                bank.Items.Count > bank.Slots || bank.Items.Any(x => x.Value <= 0)) return Reject("InvalidBankObservation");
            if (work is < 0 or > 10000 || work != Math.Ceiling(work)) return Reject("BankEstimateBoundExceeded");
            var inventory = state.Inventory.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var stored = bank.Items.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var drops = resource.GetProperty("drops").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("max_quantity").GetInt32(), StringComparer.Ordinal);
            long output = drops.Values.Sum(x => (long)x);
            if (output > state.Capacity) return Reject("InsufficientWorkingCapacity");
            var maps = observation.Catalogs["maps"];
            int? Destination(string type, string? code = null) => maps.Where(x => StrategyRules.GatheringPlace(x, state) &&
                    x.GetProperty("interactions").GetProperty("content") is { ValueKind: JsonValueKind.Object } c &&
                    c.GetProperty("type").GetString() == type && (code is null || c.GetProperty("code").GetString() == code))
                .OrderBy(x => type == "resource" && x.GetProperty("map_id").GetInt32() == state.MapId ? 0 : 1)
                .ThenBy(x => x.GetProperty("map_id").GetInt32()).Select(x => (int?)x.GetProperty("map_id").GetInt32()).FirstOrDefault();
            int? gather = Destination("resource", resource.GetProperty("code").GetString());
            if (gather is null) return Reject("NoSupportedResource");
            int current = state.MapId;
            void Move(int destination)
            {
                if (current == destination) return;
                current = destination; actions++; seconds += policy.MoveSeconds;
            }
            long Free() => state.Capacity - inventory.Values.Sum(x => (long)x);
            for (int i = 0; i < (int)work; i++)
            {
                if (Free() < output)
                {
                    var deposits = inventory.Where(x => policy.Bank.Retain.TryGetValue(x.Key, out int retain) && x.Value > retain)
                        .OrderBy(x => x.Key, StringComparer.Ordinal).Take(20).ToArray();
                    if (deposits.Length == 0) return Reject("NoDepositableStock");
                    if (stored.Count + deposits.Count(x => !stored.ContainsKey(x.Key)) > bank.Slots) return Reject("BankFull");
                    int? destination = Destination("bank");
                    if (destination is null) return Reject("NoSupportedBank");
                    Move(destination.Value);
                    foreach (var item in deposits)
                    {
                        int retained = policy.Bank.Retain[item.Key];
                        stored[item.Key] = checked(stored.GetValueOrDefault(item.Key) + item.Value - retained);
                        if (retained == 0) inventory.Remove(item.Key); else inventory[item.Key] = retained;
                    }
                    actions++; unloads++; seconds += deposits.Length * 3;
                    // Runtime may need another first-20 batch before a gather; count it as a separate finite step.
                    if (Free() < output) { i--; if (unloads > 10000) return Reject("BankEstimateBoundExceeded"); continue; }
                }
                Move(gather.Value);
                foreach (var drop in drops) inventory[drop.Key] = checked(inventory.GetValueOrDefault(drop.Key) + drop.Value);
                actions++; seconds += policy.GatherSeconds;
            }
            return new(null, actions, seconds, unloads);
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or OverflowException or ArgumentException)
        { return Reject("InvalidBankStateOrCatalog"); }
    }
}
