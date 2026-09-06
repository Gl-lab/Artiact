using Artiact.Client;
using Artiact.Contracts.Models.Api;
using System.Text.Json;

namespace Artiact.Services.Combat;

public sealed class CombatActionPort(GameClient client, ICharacterService characters) : ICombatActionPort
{
    public async Task<CombatReply> DispatchAsync(CombatCommand command, CombatDestination destination,
        string? equipment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = characters.GetCharacter();
        ActionResponse response = command switch
        {
            CombatCommand.Move => await client.MoveToMap(destination.MapId),
            CombatCommand.Fight => await client.Fight(),
            CombatCommand.Rest => await client.Rest(),
            CombatCommand.Equip => await client.EquipItem(new EquipRequest(equipment!, "weapon")),
            CombatCommand.Unequip => await client.UnequipItem(new UnequipRequest("weapon")),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        characters.SaveCharacter(response.Data.Character);
        var normalized = client.LastCharacterPayload is { } raw ? CombatObservation.Read(raw) : null;
        return new(normalized, response.Data.Cooldown.TotalSeconds, response.Data.Fight?.Result == "loss",
            ValidEnvelope(command, destination, equipment, before, response));
    }

    private bool ValidEnvelope(CombatCommand command, CombatDestination destination, string? equipment,
        Character before, ActionResponse response)
    {
        try
        {
            var raw = client.LastActionPayload!.Value;
            var timing = raw.GetProperty("cooldown");
            int total = timing.GetProperty("total_seconds").GetInt32();
            int remaining = timing.GetProperty("remaining_seconds").GetInt32();
            if (total < 0 || remaining < 0 || remaining > total ||
                timing.GetProperty("started_at").GetDateTimeOffset() > timing.GetProperty("expiration").GetDateTimeOffset() ||
                string.IsNullOrWhiteSpace(timing.GetProperty("reason").GetString())) return false;
            if (command == CombatCommand.Rest)
            {
                int restored = raw.GetProperty("hp_restored").GetInt32();
                return restored > 0 && (long)before.Hp + restored == response.Data.Character.Hp;
            }
            if (command == CombatCommand.Fight)
            {
                var fight = raw.GetProperty("fight");
                if (fight.GetProperty("opponent").GetString() != destination.MonsterCode ||
                    fight.GetProperty("turns").GetInt32() < 1 || fight.GetProperty("logs").ValueKind != JsonValueKind.Array) return false;
                var participants = fight.GetProperty("characters").EnumerateArray()
                    .Where(x => x.GetProperty("character_name").GetString() == before.Name).ToArray();
                return participants.Length == 1 && participants[0].GetProperty("xp").GetInt32() >= 0 &&
                    participants[0].GetProperty("gold").GetInt32() >= 0 &&
                    participants[0].GetProperty("final_hp").GetInt32() == response.Data.Character.Hp &&
                    participants[0].GetProperty("drops").ValueKind == JsonValueKind.Array &&
                    participants[0].GetProperty("drops").EnumerateArray().All(drop =>
                        !string.IsNullOrWhiteSpace(drop.GetProperty("code").GetString()) && drop.GetProperty("quantity").GetInt32() > 0);
            }
            if (command is CombatCommand.Equip or CombatCommand.Unequip)
            {
                var items = raw.GetProperty("items");
                return items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == 1 &&
                    items[0].GetProperty("slot").GetString() == "weapon" &&
                    items[0].GetProperty("code").GetString() == (command == CombatCommand.Equip ? equipment : before.WeaponSlot) &&
                    items[0].GetProperty("quantity").GetInt32() == 1;
            }
            return raw.GetProperty("destination").GetProperty("map_id").GetInt32() == destination.MapId;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return false; }
    }
}
