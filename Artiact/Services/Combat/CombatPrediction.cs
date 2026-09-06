namespace Artiact.Services.Combat;

public enum CombatViability { Safe, Unsafe, Unknown }

public sealed record CombatStats(int Hp, int Attack, int Bonus = 0, int ElementBonus = 0,
    int Resistance = 0, int Critical = 0);

public sealed record CombatPrediction(CombatViability Viability, int Exchanges = 0, long MaximumLoss = 0)
{
    public static CombatPrediction Evaluate(CombatStats player, CombatStats monster)
    {
        if (!Supported(player) || !Supported(monster)) return new(CombatViability.Unknown);
        int outgoing = Damage(player.Attack, player.Bonus, player.ElementBonus, monster.Resistance, false);
        if (outgoing == 0) return new(CombatViability.Unsafe);
        int exchanges = (int)((monster.Hp + (long)outgoing - 1) / outgoing);
        if (exchanges > 50) return new(CombatViability.Unsafe, exchanges);
        long loss = (long)exchanges * Damage(monster.Attack, monster.Bonus, monster.ElementBonus,
            player.Resistance, monster.Critical > 0);
        return new(player.Hp > loss ? CombatViability.Safe : CombatViability.Unsafe, exchanges, loss);
    }

    private static bool Supported(CombatStats stats) => stats.Hp is > 0 and <= 1_000_000 &&
        stats.Attack is >= 0 and <= 10_000 && stats.Bonus is >= 0 and <= 1000 &&
        stats.ElementBonus is >= 0 and <= 1000 && stats.Resistance is >= 0 and <= 100 &&
        stats.Critical is >= 0 and <= 100;

    private static int Damage(int attack, int bonus, int elemental, int resistance, bool critical)
    {
        decimal hit = Math.Round(attack * (1m + (bonus + elemental) / 100m), 0, MidpointRounding.AwayFromZero);
        hit = Math.Round(hit * (1m - resistance / 100m), 0, MidpointRounding.AwayFromZero);
        return checked((int)Math.Round(hit * (critical ? 1.5m : 1m), 0, MidpointRounding.AwayFromZero));
    }
}
