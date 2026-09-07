using Artiact.Client;
using Artiact.Services.Combat;
using Artiact.Contracts.Models.Api;
using System.Collections.Immutable;

namespace Artiact.Services.Strategy;

public sealed class StrategyActionPort(GameClient client, ICharacterService characters)
{
    public AtomicCommand Deposit(StrategyObservation observation, Item[] items, Func<StrategyObservation, bool> postcondition, bool withdraw = false) =>
        new((withdraw ? "Withdraw:" : "Deposit:") + string.Join(",", items.Select(x => x.Code + ":" + x.Quantity)), observation.Fingerprint, false, postcondition,
            async token =>
            {
                using var operation = client.BeginOperation(token);
                var response = withdraw ? await client.WithdrawBankItems(items) : await client.DepositBankItems(items);
                characters.SaveCharacter(response.RequireCharacter());
                var data = client.LastActionPayload!.Value;
                var stock = data.GetProperty("bank").EnumerateArray().ToImmutableDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("quantity").GetInt32());
                var returned = data.GetProperty("items").EnumerateArray().ToArray();
                var timing = data.GetProperty("cooldown");
                int total = timing.GetProperty("total_seconds").GetInt32(), remaining = timing.GetProperty("remaining_seconds").GetInt32();
                bool valid = total >= 0 && remaining >= 0 && remaining <= total &&
                    timing.GetProperty("started_at").GetDateTimeOffset() <= timing.GetProperty("expiration").GetDateTimeOffset() &&
                    !string.IsNullOrWhiteSpace(timing.GetProperty("reason").GetString()) &&
                    stock.All(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0) && returned.Length == items.Length &&
                    items.All(x => returned.Count(v => v.GetProperty("code").GetString() == x.Code && v.GetProperty("quantity").GetInt32() == x.Quantity) == 1);
                var after = new StrategyObservation(client.LastCharacterPayload!.Value, observation.Catalogs, observation.Policy, new(observation.Bank!.Slots, stock));
                return new(after, total, valid);
            });
    public AtomicCommand Craft(StrategyObservation observation, ProductionStep step, Func<StrategyObservation, bool> postcondition) =>
        new("Craft:" + step.Code + ":" + step.Quantity, observation.Fingerprint, true, postcondition, async token =>
        {
            using var operation = client.BeginOperation(token);
            var response = await client.Crafting(new() { Code = step.Code, Quantity = step.Quantity });
            characters.SaveCharacter(response.RequireCharacter());
            var raw = client.LastActionPayload!.Value;
            var timing = raw.GetProperty("cooldown");
            int total = timing.GetProperty("total_seconds").GetInt32(), remaining = timing.GetProperty("remaining_seconds").GetInt32();
            var details = raw.GetProperty("details"); var items = details.GetProperty("items");
            bool valid = total >= 0 && remaining >= 0 && remaining <= total &&
                timing.GetProperty("started_at").GetDateTimeOffset() <= timing.GetProperty("expiration").GetDateTimeOffset() &&
                !string.IsNullOrWhiteSpace(timing.GetProperty("reason").GetString()) && details.GetProperty("xp").GetInt32() >= 0 &&
                items.GetArrayLength() == 1 && items[0].GetProperty("code").GetString() == step.Code && items[0].GetProperty("quantity").GetInt32() == step.Output;
            return new(observation.WithCharacter(client.LastCharacterPayload!.Value), total, valid);
        });
    public AtomicCommand Combat(StrategyObservation observation, CombatCommand command, CombatDestination destination,
        string? equipment, bool productive, Func<StrategyObservation, bool> postcondition) =>
        new(command + (command == CombatCommand.Move ? ":" + destination.MapId : ""), observation.Fingerprint, productive, postcondition,
            async token =>
            {
                var reply = await new CombatActionPort(client, characters).DispatchAsync(command, destination, equipment, token);
                return new(observation.WithCharacter(client.LastCharacterPayload!.Value), reply.Cooldown, reply.ContractValid, reply.Defeat);
            });

    public AtomicCommand Gather(StrategyObservation observation, string skill, Func<StrategyObservation, bool> postcondition) =>
        new("Gather:" + skill, observation.Fingerprint, true, postcondition, async token =>
        {
            token.ThrowIfCancellationRequested();
            using var operation = client.BeginOperation(token);
            var response = await client.Gathering();
            characters.SaveCharacter(response.RequireCharacter());
            bool valid;
            try
            {
                var data = client.LastActionPayload!.Value;
                var timing = data.GetProperty("cooldown");
                int remaining = timing.GetProperty("remaining_seconds").GetInt32();
                int total = timing.GetProperty("total_seconds").GetInt32();
                var details = data.GetProperty("details");
                var before = CharacterObservation.Read(observation.Character);
                var after = CharacterObservation.Read(client.LastCharacterPayload!.Value);
                var expected = before!.Inventory.ToBuilder();
                foreach (var item in details.GetProperty("items").EnumerateArray())
                {
                    string code = item.GetProperty("code").GetString()!;
                    expected[code] = checked(expected.GetValueOrDefault(code) + item.GetProperty("quantity").GetInt32());
                }
                valid = total >= 0 && remaining >= 0 && remaining <= total &&
                    after is not null && expected.Count == after.Inventory.Count && expected.All(x => after.Inventory.GetValueOrDefault(x.Key) == x.Value) &&
                    timing.GetProperty("started_at").GetDateTimeOffset() <= timing.GetProperty("expiration").GetDateTimeOffset() &&
                    !string.IsNullOrWhiteSpace(timing.GetProperty("reason").GetString()) && details.GetProperty("xp").GetInt32() > 0 &&
                    details.GetProperty("items").GetArrayLength() > 0 && details.GetProperty("items").EnumerateArray().All(x =>
                        !string.IsNullOrWhiteSpace(x.GetProperty("code").GetString()) && x.GetProperty("quantity").GetInt32() > 0);
            }
            catch (Exception) { valid = false; }
            return new(observation.WithCharacter(client.LastCharacterPayload!.Value), response.RequireCooldown().TotalSeconds, valid);
        });
}
