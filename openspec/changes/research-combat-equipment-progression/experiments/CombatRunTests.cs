namespace CombatResearch;

public class CombatRunTests
{
    private static readonly Fighter Monster = new( 20, 3 );
    private static Snapshot Start => new( new Fighter( 20, 10 ) );
    private static readonly Gear Upgrade = new( "quick_blade", 1, new Fighter( 20, 10 ) );

    private static Reply Golden( Command command, Snapshot s ) => command switch
    {
        Command.Move => new( s with { MapId = 2 }, 2 ),
        Command.Rest => new( s with { Stats = s.Stats with { Hp = s.MaxHp } }, 13 ),
        Command.Fight => new( s with
        {
            Level = s.Xp == 5 ? 2 : 1, Xp = s.Xp == 5 ? 0 : 5,
            Stats = s.Stats with { Hp = 14 }, FreeUnits = s.FreeUnits - 1
        }, 7 ),
        Command.Unequip => new( s with { Weapon = "", Stats = s.Stats with { Attack = 0 } }, 3 ),
        Command.Equip => new( s with { Weapon = "quick_blade", Stats = s.Stats with { Attack = 10 } }, 3 ),
        _ => throw new InvalidOperationException()
    };

    private static (Decision End, Decision[] Trace, Command[] Commands) Run(
        Snapshot? state = null, Func<Command, Snapshot, Reply>? dispatch = null,
        int? target = 2, Limits? limits = null, Gear? gear = null, bool reachable = true )
    {
        RecordingPort port = new( dispatch ?? Golden );
        CombatRun run = new( state ?? Start, Monster, port, target, limits, gear, reachable );
        for ( int i = 0; i < 30; i++ )
        {
            Decision result = run.Tick();
            if ( result.Status == "Selected" ) continue;
            int count = port.Commands.Count;
            Assert.Equal( result, run.Tick() );
            Assert.Equal( count, port.Commands.Count );
            return ( result, run.Trace.ToArray(), port.Commands.ToArray() );
        }
        throw new InvalidOperationException( "Experiment did not terminate" );
    }

    private static void AssertReplay( Func<(Decision End, Decision[] Trace, Command[] Commands)> run,
                                      string reason, params Command[] commands )
    {
        var first = run();
        var second = run();
        Assert.Equal( reason, first.End.Reason );
        Assert.Equal( commands, first.Commands );
        Assert.Equal( first.End, second.End );
        Assert.Equal( first.Trace, second.Trace );
        Assert.Equal( first.Commands, second.Commands );
        Assert.True( first.End.Decisions <= 20 );
    }

    [Fact]
    public void GoldenMilestoneHasIndependentTraceAndState()
    {
        AssertReplay( () => Run(), "target_reached", Command.Move, Command.Fight, Command.Rest, Command.Fight );
        var result = Run();
        Assert.Equal( new Decision( "Completed", "target_reached",
            Start with { Level = 2, Xp = 0, Stats = new Fighter( 14, 10 ), MapId = 2, FreeUnits = 8 },
            5, 2, 1, 0, 29 ), result.End );
        Assert.Equal( new[] { "move", "fight", "rest", "fight", "target_reached" }, result.Trace.Select( d => d.Reason ) );
    }

    [Theory]
    [InlineData( "target", "target_reached" )]
    [InlineData( "xp", "invalid_state" )]
    [InlineData( "hp", "invalid_state" )]
    [InlineData( "full", "inventory_pressure" )]
    [InlineData( "unknown", "unknown_combat" )]
    [InlineData( "unsafe", "unsafe_combat" )]
    [InlineData( "unreachable", "unreachable" )]
    public void InitialTerminalStatesNeverMutate( string variant, string reason )
    {
        Snapshot s = variant switch
        {
            "target" => Start with { Level = 2, FreeUnits = 0 },
            "xp" => Start with { Xp = 10 },
            "hp" => Start with { Stats = Start.Stats with { Hp = -1 } },
            "full" => Start with { FreeUnits = 0 },
            "unknown" => Start with { Stats = Start.Stats with { Known = false } },
            "unsafe" => Start with { Stats = Start.Stats with { Attack = 1 } },
            _ => Start
        };
        AssertReplay( () => Run( s, reachable: variant != "unreachable" ), reason );
    }

    [Theory]
    [InlineData( null )]
    [InlineData( 0 )]
    [InlineData( -1 )]
    public void InvalidTargetNeverMutates( int? target ) => AssertReplay( () => Run( target: target ), "invalid_target" );

    [Theory]
    [InlineData( 0, 4, 2, 3 )]
    [InlineData( 20, 0, 2, 3 )]
    [InlineData( 20, 4, -1, 3 )]
    [InlineData( 20, 4, 2, 0 )]
    [InlineData( 2, 4, 2, 3 )]
    public void InvalidLimitsNeverMutate( int decisions, int fights, int rests, int noProgress ) =>
        AssertReplay( () => Run( limits: new Limits( decisions, fights, rests, noProgress ) ), "invalid_limits" );

    [Theory]
    [InlineData( "decision", "decision_limit" )]
    [InlineData( "fight", "fight_limit" )]
    [InlineData( "rest", "recovery_limit" )]
    [InlineData( "progress", "no_progress" )]
    public void BudgetsBoundRepeatedCommands( string variant, string reason )
    {
        Snapshot s = Start with { MapId = 2 };
        Reply Loop( Command c, Snapshot current ) => c == Command.Rest
            ? new( current with { Stats = current.Stats with { Hp = 20 } }, 13 )
            : new( current with { Xp = variant == "rest" ? current.Xp + 1 : current.Xp,
                                  Stats = current.Stats with { Hp = variant == "rest" ? 14 : 20 } }, 7 );
        Limits limits = variant switch
        {
            "decision" => new( 2, 4, 2, 2 ),
            "fight" => new( Fights: 1 ),
            "rest" => new( Rests: 1 ),
            _ => new( NoProgress: 1 )
        };
        Command[] expected = variant == "rest" ? [Command.Fight, Command.Rest, Command.Fight] : [Command.Fight];
        AssertReplay( () => Run( s, Loop, limits: limits ), reason, expected );
    }

    [Theory]
    [InlineData( "unchanged", "recovery_no_progress" )]
    [InlineData( "invalid", "invalid_state" )]
    [InlineData( "rejected", "action_rejected" )]
    [InlineData( "lost", "unknown_outcome" )]
    public void RecoveryFailuresStop( string variant, string reason )
    {
        Snapshot s = Start with { MapId = 2, Stats = Start.Stats with { Hp = 10 } };
        Reply Rest( Command c, Snapshot current ) => variant == "lost" ? throw new IOException() :
            new( variant == "invalid" ? current with { Stats = current.Stats with { Hp = 21 } } : current,
                13, Rejected: variant == "rejected" );
        AssertReplay( () => Run( s, Rest ), reason, Command.Rest );
        Assert.Equal( 1, Run( s, Rest ).End.Rests );
    }

    [Theory]
    [InlineData( "wrong_map", "movement_failed" )]
    [InlineData( "changed_stats", "unknown_combat" )]
    [InlineData( "full", "inventory_pressure" )]
    [InlineData( "lost", "unknown_outcome" )]
    public void MovementReconcilesBeforeAnotherAction( string variant, string reason )
    {
        Reply Move( Command c, Snapshot s ) => variant == "lost" ? throw new IOException() : new(
            s with { MapId = variant == "wrong_map" ? 3 : 2,
                     Stats = s.Stats with { Known = variant != "changed_stats" },
                     FreeUnits = variant == "full" ? 0 : 10 }, 2 );
        AssertReplay( () => Run( dispatch: Move ), reason, Command.Move );
    }

    [Fact]
    public void DefeatKeepsReturnedLocationAndNeverSeeksRevenge()
    {
        Reply Defeat( Command c, Snapshot s ) => new( s with { MapId = 1, Stats = s.Stats with { Hp = 1 } }, 9, Defeat: true );
        AssertReplay( () => Run( Start with { MapId = 2 }, Defeat ), "defeat", Command.Fight );
        var result = Run( Start with { MapId = 2 }, Defeat ).End;
        Assert.Equal( 1, result.State.MapId );
        Assert.Equal( 1, result.State.Stats.Hp );
        Assert.Equal( 9, result.VirtualSeconds );
    }

    [Theory]
    [InlineData( "condition", "equipment_condition" )]
    [InlineData( "slot", "equipment_slot" )]
    [InlineData( "owned", "equipment_missing" )]
    [InlineData( "space", "inventory_pressure" )]
    public void EquipmentPreconditionsFailClosed( string variant, string reason )
    {
        Gear gear = Upgrade with { ConditionMet = variant != "condition", Slot = variant == "slot" ? "bag" : "weapon",
                                   Owned = variant != "owned" };
        Snapshot s = Start with { Weapon = "old", FreeUnits = variant == "space" ? 0 : 10 };
        AssertReplay( () => Run( s, gear: gear ), reason );
    }

    [Fact]
    public void EquipmentComparisonUsesOpponentAndCurrentLoadout()
    {
        Snapshot s = Start with { Stats = new Fighter( 20, 2 ), Weapon = "old" };
        Gear high = new( "heavy_blade", 9, new Fighter( 20, 4 ) );
        Assert.Equal( Upgrade, Equipment.Choose( s, Monster, [high, Upgrade] ) );
        Assert.Equal( Upgrade, Equipment.Choose( s, Monster, [Upgrade, high] ) );
        Assert.Null( Equipment.Choose( Start, Monster, [high] ) );
    }

    [Fact]
    public void EquipResponseStatsAreAuthoritative()
    {
        Reply Equip( Command c, Snapshot s ) => new( s with { Weapon = "quick_blade", Stats = s.Stats with { Attack = 0 } }, 3 );
        AssertReplay( () => Run( Start with { Weapon = "" }, Equip, gear: Upgrade ), "unsafe_combat", Command.Equip );
    }

    [Fact]
    public void FailedSwapStopsAfterUnequip()
    {
        Reply Swap( Command c, Snapshot s ) => c == Command.Unequip ? Golden( c, s ) : new( s, 0, Rejected: true );
        AssertReplay( () => Run( Start with { Weapon = "old" }, Swap, gear: Upgrade ), "action_rejected", Command.Unequip, Command.Equip );
        Assert.Equal( "", Run( Start with { Weapon = "old" }, Swap, gear: Upgrade ).End.State.Weapon );
    }

    [Theory]
    [InlineData( false )]
    [InlineData( true )]
    public void CancellationRetainsResponseAndPreventsNextDispatch( bool afterResponse )
    {
        Decision RunCancelled()
        {
            using CancellationTokenSource cts = new();
            RecordingPort port = new( ( c, s ) => { cts.Cancel(); return Golden( c, s ); } );
            CombatRun run = new( Start, Monster, port );
            if ( !afterResponse ) cts.Cancel();
            Decision result = run.Tick( cts.Token );
            Assert.Equal( "cancelled", result.Reason );
            Assert.Equal( afterResponse ? 2 : 1, result.State.MapId );
            Assert.Equal( afterResponse ? 1 : 0, port.Commands.Count );
            Assert.Equal( result, run.Tick() );
            return result;
        }
        Assert.Equal( RunCancelled(), RunCancelled() );
    }

    [Fact]
    public void StaleObservationIsRecheckedBeforeDispatch()
    {
        Decision RunStale()
        {
            RecordingPort port = new( Golden );
            CombatRun run = new( Start, Monster, port );
            Decision result = run.Tick( observed: Start with { FreeUnits = 0 } );
            Assert.Empty( port.Commands );
            Assert.Equal( "inventory_pressure", result.Reason );
            return result;
        }
        Assert.Equal( RunStale(), RunStale() );
    }

    [Fact]
    public void LostFightResponseChargesOneAttemptWithoutReplay()
    {
        Reply Lost( Command c, Snapshot s ) => throw new IOException( "scripted response loss" );
        AssertReplay( () => Run( Start with { MapId = 2 }, Lost ), "unknown_outcome", Command.Fight );
        Assert.Equal( 1, Run( Start with { MapId = 2 }, Lost ).End.Fights );
    }

    [Fact]
    public void SuccessfulSwapThenProgressionIsBounded()
    {
        AssertReplay( () => Run( Start with { Weapon = "old" }, gear: Upgrade, limits: new Limits( NoProgress: 5 ) ),
            "target_reached", Command.Unequip, Command.Equip, Command.Move, Command.Fight, Command.Rest, Command.Fight );
        var end = Run( Start with { Weapon = "old" }, gear: Upgrade, limits: new Limits( NoProgress: 5 ) ).End;
        Assert.Equal( 7, end.Decisions );
        Assert.Equal( 35, end.VirtualSeconds );
    }

    [Fact]
    public void EquipmentAndMovementCannotRefundNoProgressBudget() =>
        AssertReplay( () => Run( Start with { Weapon = "old" }, gear: Upgrade ), "no_progress",
                      Command.Unequip, Command.Equip, Command.Move );

    [Fact]
    public void PartialRestCannotResetNoProgressBudget()
    {
        Reply Partial( Command c, Snapshot s ) => new( s with { Stats = s.Stats with { Hp = s.Stats.Hp + 1 } }, 3 );
        AssertReplay( () => Run( Start with { Stats = Start.Stats with { Hp = 10 } }, Partial,
                                limits: new Limits( Rests: 10, NoProgress: 2 ) ), "no_progress", Command.Rest, Command.Rest );
    }

    [Theory]
    [InlineData( Command.Equip )]
    [InlineData( Command.Unequip )]
    public void WrongEquipmentPostconditionStops( Command expected )
    {
        Reply Unchanged( Command c, Snapshot s ) => new( s, 3 );
        AssertReplay( () => Run( Start with { Weapon = expected == Command.Equip ? "" : "old" }, Unchanged, gear: Upgrade ),
                      "equipment_failed", expected );
    }

    [Fact]
    public void InvalidReturnedCooldownStopsAndRetainsState()
    {
        Reply Invalid( Command c, Snapshot s ) => new( s with { MapId = 2 }, -1 );
        AssertReplay( () => Run( dispatch: Invalid ), "invalid_cooldown", Command.Move );
        Assert.Equal( 2, Run( dispatch: Invalid ).End.State.MapId );
    }

    [Fact]
    public void EquipmentCannotBypassUnknownCurrentState() =>
        AssertReplay( () => Run( Start with { Weapon = "old", Stats = Start.Stats with { Known = false } }, gear: Upgrade ),
                      "unknown_combat" );
}
