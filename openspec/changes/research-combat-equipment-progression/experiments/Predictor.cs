namespace CombatResearch;

public enum Viability { Safe, Unsafe, Unknown }

// Explicitly normalized single-element research input, not an API DTO.
public sealed record Fighter( int Hp, int Attack, int GlobalBonus = 0, int ElementBonus = 0,
                              int Resistance = 0, int Critical = 0, int Initiative = 0,
                              bool Known = true, bool Effects = false, bool SingleElement = true );
public sealed record Prediction( Viability Verdict, string Reason, int Exchanges = 0, long Loss = 0 );

public static class Predictor
{
    public static int Damage( int attack, int global, int elemental, int resistance, bool critical )
    {
        decimal hit = Math.Round( attack * ( 1m + ( global + elemental ) / 100m ), 0, MidpointRounding.AwayFromZero );
        hit = Math.Round( hit * ( 1m - resistance / 100m ), 0, MidpointRounding.AwayFromZero );
        return checked( (int)Math.Round( hit * ( critical ? 1.5m : 1m ), 0, MidpointRounding.AwayFromZero ) );
    }

    public static Prediction Evaluate( Fighter player, Fighter monster, string monsterType = "normal" )
    {
        if ( monsterType != "normal" || !Supported( player ) || !Supported( monster ) )
            return new( Viability.Unknown, "unsupported_inputs" );

        int outgoing = Damage( player.Attack, player.GlobalBonus, player.ElementBonus, monster.Resistance, false );
        if ( outgoing == 0 ) return new( Viability.Unsafe, "zero_damage" );
        int exchanges = (int)( ( monster.Hp + (long)outgoing - 1 ) / outgoing );
        if ( exchanges > 50 ) return new( Viability.Unsafe, "turn_limit", exchanges );
        int incoming = Damage( monster.Attack, monster.GlobalBonus, monster.ElementBonus, player.Resistance,
                               monster.Critical > 0 );
        long loss = (long)exchanges * incoming;
        return new( player.Hp > loss ? Viability.Safe : Viability.Unsafe, "survival_bound", exchanges, loss );
    }

    private static bool Supported( Fighter f ) => f.Known && !f.Effects && f.SingleElement &&
        f.Hp is > 0 and <= 1_000_000 && f.Attack is >= 0 and <= 10_000 &&
        f.GlobalBonus is >= 0 and <= 1000 && f.ElementBonus is >= 0 and <= 1000 &&
        f.Resistance is >= 0 and <= 100 && f.Critical is >= 0 and <= 100;
}
