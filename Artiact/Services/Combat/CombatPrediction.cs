namespace Artiact.Services.Combat;

public enum CombatViability { Safe, Unsafe, Unknown }

public sealed record CombatStats(int Hp, int Attack, int Bonus = 0, int ElementBonus = 0,
    int Resistance = 0, int Critical = 0, ElementStats? Earth = null, ElementStats? Water = null, ElementStats? Air = null);
public sealed record ElementStats(int Attack = 0, int Bonus = 0, int Resistance = 0);

public sealed record CombatPrediction(CombatViability Viability, int Exchanges = 0, long MaximumLoss = 0)
{
    public static CombatPrediction Evaluate(CombatStats player, CombatStats monster)
    {
        if (!Supported(player) || !Supported(monster)) return new(CombatViability.Unknown);
        int outgoing = Hit(player, monster, false);
        if (outgoing == 0) return new(CombatViability.Unsafe);
        int exchanges = (int)((monster.Hp + (long)outgoing - 1) / outgoing);
        if (exchanges > 50) return new(CombatViability.Unsafe, exchanges);
        long loss = (long)exchanges * Hit(monster, player, monster.Critical > 0);
        return new(player.Hp > loss ? CombatViability.Safe : CombatViability.Unsafe, exchanges, loss);
    }

    private static bool Supported(CombatStats stats) => stats.Hp is > 0 and <= 1_000_000 &&
        stats.Attack is >= 0 and <= 10_000 && stats.Bonus is >= 0 and <= 1000 &&
        stats.ElementBonus is >= 0 and <= 1000 && stats.Resistance is >= 0 and <= 100 &&
        stats.Critical is >= 0 and <= 100 && new[] { stats.Earth, stats.Water, stats.Air }.All(x => x is null ||
            x.Attack is >= 0 and <= 10000 && x.Bonus is >= 0 and <= 1000 && x.Resistance is >= 0 and <= 100);

    private static int Hit(CombatStats attacker, CombatStats defender, bool critical)
    {
        int Channel(ElementStats? attack, ElementStats? defense) => Damage(attack?.Attack ?? 0, attacker.Bonus,
            attack?.Bonus ?? 0, defense?.Resistance ?? 0, critical);
        return checked(Damage(attacker.Attack, attacker.Bonus, attacker.ElementBonus, defender.Resistance, critical) +
            Channel(attacker.Earth, defender.Earth) + Channel(attacker.Water, defender.Water) + Channel(attacker.Air, defender.Air));
    }

    private static int Damage(int attack, int bonus, int elemental, int resistance, bool critical)
    {
        decimal hit = Math.Round(attack * (1m + (bonus + elemental) / 100m), 0, MidpointRounding.AwayFromZero);
        hit = Math.Round(hit * (1m - resistance / 100m), 0, MidpointRounding.AwayFromZero);
        return checked((int)Math.Round(hit * (critical ? 1.5m : 1m), 0, MidpointRounding.AwayFromZero));
    }
}
