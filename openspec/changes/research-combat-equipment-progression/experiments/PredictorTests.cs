namespace CombatResearch;

public class PredictorTests
{
    [Theory]
    [InlineData( 20, 0, Viability.Safe, 6 )]
    [InlineData( 7, 0, Viability.Safe, 6 )]
    [InlineData( 6, 0, Viability.Unsafe, 6 )]
    [InlineData( 10, 1, Viability.Unsafe, 10 )]
    [InlineData( 11, 100, Viability.Safe, 10 )]
    public void IndependentHpAndCriticalBounds( int hp, int crit, Viability expected, long loss )
    {
        Prediction Run() => Predictor.Evaluate( new Fighter( hp, 10 ), new Fighter( 20, 3, Critical: crit ) );
        Prediction first = Run();
        Assert.Equal( first, Run() );
        Assert.Equal( expected, first.Verdict );
        Assert.Equal( 2, first.Exchanges );
        Assert.Equal( loss, first.Loss );
    }

    [Theory]
    [InlineData( false, 5 )]
    [InlineData( true, 8 )]
    public void RoundAtEachStage( bool critical, int expected )
    {
        Assert.Equal( expected, Predictor.Damage( 5, 10, 0, 25, critical ) );
        Assert.Equal( expected, Predictor.Damage( 5, 10, 0, 25, critical ) );
    }

    [Theory]
    [InlineData( -10 )]
    [InlineData( 0 )]
    [InlineData( 10 )]
    public void InitiativeCannotImproveWorstCaseBound( int initiative )
    {
        Prediction Run() => Predictor.Evaluate( new Fighter( 20, 10, Initiative: initiative ), new Fighter( 20, 3 ) );
        Assert.Equal( new Prediction( Viability.Safe, "survival_bound", 2, 6 ), Run() );
        Assert.Equal( Run(), Run() );
    }

    [Theory]
    [InlineData( "missing" )]
    [InlineData( "effect" )]
    [InlineData( "elements" )]
    [InlineData( "negative" )]
    [InlineData( "overflow" )]
    [InlineData( "crit" )]
    [InlineData( "resistance" )]
    [InlineData( "elite" )]
    public void UnsupportedInputsAreUnknown( string variant )
    {
        Fighter p = new( 20, 10 );
        p = variant switch
        {
            "missing" => p with { Known = false },
            "effect" => p with { Effects = true },
            "elements" => p with { SingleElement = false },
            "negative" => p with { Attack = -1 },
            "overflow" => p with { Attack = int.MaxValue },
            "crit" => p with { Critical = 101 },
            "resistance" => p with { Resistance = -1 },
            _ => p
        };
        Prediction Run() => Predictor.Evaluate( p, new Fighter( 20, 3 ), variant == "elite" ? "elite" : "normal" );
        Assert.Equal( Viability.Unknown, Run().Verdict );
        Assert.Equal( Run(), Run() );
    }

    [Theory]
    [InlineData( 0, 20, 0, "zero_damage" )]
    [InlineData( 10, 20, 100, "zero_damage" )]
    [InlineData( 1, 51, 0, "turn_limit" )]
    public void UnproductiveCombatIsRejected( int attack, int hp, int resistance, string reason )
    {
        Prediction Run() => Predictor.Evaluate( new Fighter( 1000, attack ), new Fighter( hp, 1, Resistance: resistance ) );
        Assert.Equal( Viability.Unsafe, Run().Verdict );
        Assert.Equal( reason, Run().Reason );
        Assert.Equal( Run(), Run() );
    }
}
