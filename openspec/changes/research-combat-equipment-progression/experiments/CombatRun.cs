namespace CombatResearch;

public sealed record Snapshot( Fighter Stats, int Level = 1, int Xp = 0, int MaxXp = 10,
                               int MaxHp = 20, int FreeUnits = 10, int MapId = 1,
                               string Weapon = "quick_blade" );
public sealed record Limits( int Decisions = 20, int Fights = 4, int Rests = 2, int NoProgress = 3 );
public sealed record Gear( string Code, int Level, Fighter ProjectedStats, string Slot = "weapon",
                           bool ConditionMet = true, bool Owned = true );
public enum Command { Move, Fight, Rest, Unequip, Equip }
public sealed record Reply( Snapshot State, int Cooldown, bool Defeat = false, bool Rejected = false );
public sealed record Decision( string Status, string Reason, Snapshot State, int Decisions, int Fights,
                               int Rests, int NoProgress, long VirtualSeconds );

public sealed class RecordingPort( Func<Command, Snapshot, Reply> dispatch )
{
    public List<Command> Commands { get; } = new();
    public Reply Dispatch( Command command, Snapshot state )
    {
        Commands.Add( command );
        return dispatch( command, state );
    }
}

public static class Equipment
{
    public static Gear? Choose( Snapshot state, Fighter monster, IEnumerable<Gear> candidates )
    {
        Prediction baseline = Predictor.Evaluate( state.Stats with { Hp = state.MaxHp }, monster );
        if ( baseline.Verdict == Viability.Unknown || state.FreeUnits < 1 ) return null;
        return candidates.Where( g => g.ConditionMet && g.Owned && g.Slot == "weapon" )
            .Select( g => (Gear: g, Prediction: Predictor.Evaluate( g.ProjectedStats with { Hp = state.MaxHp }, monster )) )
            .Where( x => x.Prediction.Verdict == Viability.Safe &&
                         ( baseline.Verdict != Viability.Safe || x.Prediction.Loss < baseline.Loss ) )
            .OrderBy( x => x.Prediction.Loss ).ThenBy( x => x.Gear.Code, StringComparer.Ordinal )
            .Select( x => x.Gear ).FirstOrDefault();
    }
}

public sealed class CombatRun( Snapshot initial, Fighter monster, RecordingPort port,
                               int? target = 2, Limits? limits = null, Gear? gear = null,
                               bool reachable = true )
{
    private readonly Limits _limits = limits ?? new Limits();
    private int _decisions;
    private int _fights;
    private int _rests;
    private int _noProgress;
    private long _virtualSeconds;
    private Decision? _terminal;
    public Snapshot State { get; private set; } = initial;
    public List<Decision> Trace { get; } = new();
    public Decision Tick( CancellationToken cancellationToken = default, Snapshot? observed = null )
    {
        if ( _terminal != null ) return _terminal;
        if ( observed != null ) State = observed;
        _decisions++;
        if ( cancellationToken.IsCancellationRequested ) return Emit( "Blocked", "cancelled" );
        if ( target is null or <= 0 ) return Emit( "Blocked", "invalid_target" );
        if ( _limits.Decisions < 1 || _limits.Fights < 1 || _limits.Rests < 1 ||
             _limits.NoProgress < 1 || _limits.NoProgress > _limits.Decisions )
            return Emit( "Blocked", "invalid_limits" );
        if ( !ValidState() ) return Emit( "Blocked", "invalid_state" );
        if ( State.Level >= target ) return Emit( "Completed", "target_reached" );
        if ( State.FreeUnits == 0 ) return Emit( "Blocked", "inventory_pressure" );
        if ( _noProgress >= _limits.NoProgress ) return Emit( "Blocked", "no_progress" );
        if ( _decisions >= _limits.Decisions ) return Emit( "Blocked", "decision_limit" );
        if ( !reachable ) return Emit( "Blocked", "unreachable" );
        if ( Predictor.Evaluate( State.Stats with { Hp = State.MaxHp }, monster ).Verdict == Viability.Unknown )
            return Emit( "Blocked", "unknown_combat" );

        Command command;
        if ( gear != null && State.Weapon != gear.Code )
        {
            if ( !gear.ConditionMet ) return Emit( "Blocked", "equipment_condition" );
            if ( gear.Slot != "weapon" ) return Emit( "Blocked", "equipment_slot" );
            if ( !gear.Owned ) return Emit( "Blocked", "equipment_missing" );
            if ( Predictor.Evaluate( gear.ProjectedStats with { Hp = State.MaxHp }, monster ).Verdict != Viability.Safe )
                return Emit( "Blocked", "unknown_equipment" );
            command = State.Weapon == "" ? Command.Equip : Command.Unequip;
        }
        else
        {
            // Full-health feasibility is checked before paying for rest or movement.
            Prediction prediction = Predictor.Evaluate( State.Stats with { Hp = State.MaxHp }, monster );
            if ( prediction.Verdict == Viability.Unknown ) return Emit( "Blocked", "unknown_combat" );
            if ( prediction.Verdict == Viability.Unsafe ) return Emit( "Blocked", "unsafe_combat" );
            command = State.Stats.Hp < State.MaxHp ? Command.Rest : State.MapId != 2 ? Command.Move : Command.Fight;
        }

        if ( command == Command.Fight && _fights >= _limits.Fights ) return Emit( "Blocked", "fight_limit" );
        if ( command == Command.Rest && _rests >= _limits.Rests ) return Emit( "Blocked", "recovery_limit" );
        if ( command == Command.Fight ) _fights++;
        if ( command == Command.Rest ) _rests++;
        Snapshot before = State;
        Reply response;
        try { response = port.Dispatch( command, before ); }
        catch ( IOException ) { return Emit( "Blocked", "unknown_outcome" ); }

        State = response.State;
        if ( response.Cooldown < 0 ) return Emit( "Blocked", "invalid_cooldown" );
        _virtualSeconds += response.Cooldown;
        bool xpProgress = command == Command.Fight &&
            ( State.Level > before.Level || State.Level == before.Level && State.Xp > before.Xp );
        _noProgress = xpProgress ? 0 : _noProgress + 1;
        if ( cancellationToken.IsCancellationRequested ) return Emit( "Blocked", "cancelled" );
        if ( !ValidState() ) return Emit( "Blocked", "invalid_state" );
        if ( response.Rejected ) return Emit( "Blocked", "action_rejected" );
        if ( response.Defeat ) return Emit( "Blocked", "defeat" );
        if ( command == Command.Move && State.MapId != 2 ) return Emit( "Blocked", "movement_failed" );
        if ( command == Command.Rest && State.Stats.Hp <= before.Stats.Hp ) return Emit( "Blocked", "recovery_no_progress" );
        if ( command == Command.Equip && State.Weapon != gear!.Code || command == Command.Unequip && State.Weapon != "" )
            return Emit( "Blocked", "equipment_failed" );
        return Emit( "Selected", command.ToString().ToLowerInvariant() );
    }

    private bool ValidState() => State.Level > 0 && State.Xp >= 0 && State.MaxXp > 0 && State.Xp < State.MaxXp &&
        State.MaxHp is > 0 and <= 1_000_000 && State.Stats.Hp > 0 && State.Stats.Hp <= State.MaxHp &&
        State.FreeUnits >= 0 && State.MapId > 0;

    private Decision Emit( string status, string reason )
    {
        Decision decision = new( status, reason, State, _decisions, _fights, _rests, _noProgress, _virtualSeconds );
        Trace.Add( decision );
        if ( status != "Selected" ) _terminal = decision;
        return decision;
    }
}
