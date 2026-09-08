using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services.Strategy;

public sealed record NeedEvidence(string Id, string Source, string Code, int Quantity, int Priority, int Deficit,
    string Cause, int FullActions, decimal FullSeconds, int SliceActions, decimal SliceSeconds, string SliceResult,
    int RequiredNoProgress, int Revision = 0, int ConfiguredNoProgress = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] ImmutableArray<string> Plan = default);
public sealed record NeedPlanStep(decimal Seconds, bool Productive, bool DurableResult, string Result);
public sealed record NeedsChainEstimate(string? Rejection, ImmutableArray<NeedPlanStep> Steps)
{
    public static NeedsChainEstimate Build(StrategyObservation observation, Func<StrategyObservation, StrategyCandidate> planner, PortfolioPolicy policy)
    {
        var steps = ImmutableArray.CreateBuilder<NeedPlanStep>();
        var current = observation.WithContext(observation.Context with { RemainingActions = null, RemainingSeconds = null });
        var seen = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            for (int i = 0; i <= 256; i++)
            {
                var candidate = planner(current);
                if (candidate.Complete) return new(null, steps.ToImmutable());
                if (candidate.Rejection is not null || candidate.Command is null) return new(candidate.Rejection ?? "UnsupportedNeedChain", steps.ToImmutable());
                if (i == 256 || !seen.Add(current.Fingerprint)) return new("NeedChainCycleOrBound", steps.ToImmutable());
                var (after, seconds) = Apply(current, candidate.Command, policy);
                if (!candidate.Command.Postcondition(after)) return new("UnsupportedNeedEstimate", steps.ToImmutable());
                var command = candidate.Command;
                var used = current.Context.Used.ToBuilder();
                foreach (var charge in command.Charges ?? ImmutableDictionary<string, int>.Empty)
                    used[charge.Key] = checked(used.GetValueOrDefault(charge.Key) + charge.Value);
                bool durable = command.Id.StartsWith("Gather:", StringComparison.Ordinal) || command.Id.StartsWith("Craft:", StringComparison.Ordinal) ||
                    command.Id.StartsWith("Use:", StringComparison.Ordinal) || command.Id == "Rest";
                string result = command.Id;
                if (candidate.Prerequisite is { Skill: "weaponcrafting" or "cooking" or "mining" or "fishing" } prerequisite &&
                    (command.Id.StartsWith("Craft:", StringComparison.Ordinal) || prerequisite.TrainingItem is null))
                {
                    durable = after.Character.GetProperty(prerequisite.Skill + "_level").GetInt32() >= prerequisite.Target;
                    result = prerequisite.Skill + ":" + prerequisite.Target;
                }
                steps.Add(new(seconds, command.Productive, durable, result));
                current = after.WithContext(current.Context with { Used = used.ToImmutable() });
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or KeyNotFoundException or OverflowException or FormatException)
        { return new("UnsupportedNeedEstimate", steps.ToImmutable()); }
        return new("NeedChainCycleOrBound", steps.ToImmutable());
    }

    private static (StrategyObservation State, decimal Seconds) Apply(StrategyObservation before, AtomicCommand command, PortfolioPolicy policy)
    {
        var raw = JsonNode.Parse(before.Character.GetRawText())!;
        var inventory = CharacterObservation.Read(before.Character)!.Inventory.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var bank = before.Bank?.Items.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var parts = command.Id.Split(':'); decimal seconds;
        void Add(string code, int quantity)
        {
            int value = checked(inventory.GetValueOrDefault(code) + quantity);
            if (value < 0) throw new InvalidOperationException();
            if (value == 0) inventory.Remove(code); else inventory[code] = value;
        }
        void Xp(string skill, int required, bool crafting = false, int count = 1)
        {
            // Official skill formula estimate; future thresholds reuse the observed maximum and replan from live facts.
            int level = raw[skill + "_level"]!.GetValue<int>();
            decimal basis = crafting ? required switch { < 5 => 50, < 10 => 100, < 15 => 200, < 20 => 325, < 25 => 450, < 30 => 550, < 35 => 650, < 40 => 750, < 45 => 850, _ => 1000 } :
                required switch { < 10 => 5, < 20 => 10, < 30 => 13, < 35 => 16, < 40 => 20, < 45 => 28, _ => 36 };
            decimal coefficient = crafting ? Math.Min(70, 25 + required / 5 * 5) : 8;
            decimal wisdom = 1 + (raw["wisdom"]?.GetValue<int>() ?? 0) * 0.001m;
            int gained = level - required >= 10 ? 0 : checked((int)Math.Round((basis + required * coefficient / level) *
                (crafting && skill == "cooking" ? 0.5m : 1) * wisdom, 0, MidpointRounding.AwayFromZero) * count);
            int xp = checked(raw[skill + "_xp"]!.GetValue<int>() + gained), maximum = raw[skill + "_max_xp"]!.GetValue<int>();
            if (maximum <= 0) throw new InvalidOperationException();
            while (xp >= maximum && level < 50) { level++; xp -= maximum; }
            raw[skill + "_level"] = level;
            if (level == 50) xp = Math.Min(xp, maximum - 1);
            raw[skill + "_xp"] = xp;
        }
        switch (parts[0])
        {
            case "Move":
                var map = before.Catalogs["maps"].Single(x => x.GetProperty("map_id").GetInt32() == int.Parse(parts[1]));
                raw["map_id"] = int.Parse(parts[1]);
                foreach (string field in new[] { "x", "y" }) raw[field] = map.GetProperty(field).GetInt32();
                seconds = policy.MoveSeconds; break;
            case "Gather":
                var place = before.Catalogs["maps"].Single(x => x.GetProperty("map_id").GetInt32() == raw["map_id"]!.GetValue<int>());
                var resource = before.Catalogs["resources"].Single(x => x.GetProperty("code").GetString() == place.GetProperty("interactions").GetProperty("content").GetProperty("code").GetString());
                var drops = resource.GetProperty("drops").EnumerateArray().ToArray();
                if (drops.Any(x => x.GetProperty("min_quantity").GetInt32() <= 0 || x.GetProperty("rate").GetInt32() != 1))
                    throw new InvalidOperationException("UnsupportedNeedYield");
                if (inventory.Values.Sum(x => (long)x) + drops.Sum(x => (long)x.GetProperty("max_quantity").GetInt32()) > raw["inventory_max_items"]!.GetValue<int>())
                    throw new InvalidOperationException("NeedInventoryInsufficient");
                foreach (var drop in drops) Add(drop.GetProperty("code").GetString()!, drop.GetProperty("min_quantity").GetInt32());
                Xp(parts[1], resource.GetProperty("level").GetInt32()); seconds = policy.GatherSeconds; break;
            case "Craft":
                if (policy.Needs?.AllowCraft != true && policy.Recovery?.AllowCraft != true) throw new InvalidOperationException();
                var crafted = before.Catalogs["items"].Single(x => x.GetProperty("code").GetString() == parts[1]);
                var recipe = crafted.GetProperty("craft");
                string skill = recipe.GetProperty("skill").GetString()!;
                if (skill is not ("weaponcrafting" or "cooking")) throw new InvalidOperationException();
                int count = int.Parse(parts[2]);
                foreach (var item in recipe.GetProperty("items").EnumerateArray()) Add(item.GetProperty("code").GetString()!, checked(-item.GetProperty("quantity").GetInt32() * count));
                Add(parts[1], checked(recipe.GetProperty("quantity").GetInt32() * count));
                Xp(skill, crafted.TryGetProperty("level", out var itemLevel) ? itemLevel.GetInt32() : recipe.GetProperty("level").GetInt32(), true, count);
                seconds = count * 4; break;
            case "Deposit":
            case "Withdraw":
                if (bank is null || policy.Bank is null || parts[0] == "Withdraw" && policy.Needs?.AllowBankWithdrawal != true && policy.Recovery?.AllowBankWithdrawal != true)
                    throw new InvalidOperationException();
                var transfers = command.Id[(parts[0].Length + 1)..].Split(',');
                foreach (string transfer in transfers)
                {
                    var entry = transfer.Split(':'); int quantity = int.Parse(entry[1]);
                    if (parts[0] == "Deposit" && (!policy.Bank.Retain.TryGetValue(entry[0], out int floor) || inventory.GetValueOrDefault(entry[0]) - quantity < floor)) throw new InvalidOperationException();
                    int delta = parts[0] == "Withdraw" ? quantity : -quantity;
                    Add(entry[0], delta); bank[entry[0]] = checked(bank.GetValueOrDefault(entry[0]) - delta);
                    if (bank[entry[0]] < 0) throw new InvalidOperationException();
                    if (bank[entry[0]] == 0) bank.Remove(entry[0]);
                }
                if (bank.Count > before.Bank!.Slots) throw new InvalidOperationException();
                seconds = transfers.Length * 3; break;
            case "Use":
                var food = before.Catalogs["items"].Single(x => x.GetProperty("code").GetString() == parts[1]);
                int quantityUsed = int.Parse(parts[2]); Add(parts[1], -quantityUsed);
                raw["hp"] = Math.Min(raw["max_hp"]!.GetValue<int>(), checked(raw["hp"]!.GetValue<int>() + food.GetProperty("effects")[0].GetProperty("value").GetInt32() * quantityUsed));
                seconds = 3; break;
            case "Rest":
                seconds = Math.Max(3, Math.Ceiling(100m * (raw["max_hp"]!.GetValue<int>() - raw["hp"]!.GetValue<int>()) / raw["max_hp"]!.GetValue<int>()));
                raw["hp"] = raw["max_hp"]!.GetValue<int>(); break;
            default: throw new InvalidOperationException();
        }
        if (inventory.Values.Sum(x => (long)x) > raw["inventory_max_items"]!.GetValue<int>()) throw new InvalidOperationException();
        if (parts[0] is not ("Move" or "Rest"))
            raw["inventory"] = JsonSerializer.SerializeToNode(inventory.OrderBy(x => x.Key).Select((x, i) => new { slot = i + 1, code = x.Key, quantity = x.Value }));
        return (new(JsonSerializer.SerializeToElement(raw), before.Catalogs, before.Policy,
            bank is null ? null : new BankSnapshot(before.Bank!.Slots, bank.ToImmutableDictionary(StringComparer.Ordinal)), before.Context, before.Orders), seconds);
    }
}
