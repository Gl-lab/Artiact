using Artiact.Client;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public sealed class StrategyActionPort(GameClient client, ICharacterService characters)
{
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
            var response = await client.Gathering();
            characters.SaveCharacter(response.Data.Character);
            bool valid;
            try
            {
                var data = client.LastActionPayload!.Value;
                var timing = data.GetProperty("cooldown");
                int remaining = timing.GetProperty("remaining_seconds").GetInt32();
                int total = timing.GetProperty("total_seconds").GetInt32();
                var details = data.GetProperty("details");
                var before = CombatObservation.Read(observation.Character);
                var after = CombatObservation.Read(client.LastCharacterPayload!.Value);
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
            return new(observation.WithCharacter(client.LastCharacterPayload!.Value), response.Data.Cooldown.TotalSeconds, valid);
        });
}
