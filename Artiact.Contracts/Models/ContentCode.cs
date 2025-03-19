namespace Artiact.Contracts.Models;

public record ContentCode( string Value )
{
    public static readonly ContentCode None = new( "none" );


    public static readonly ContentCode SalmonFishingSpot = new( "salmon_fishing_spot" );

    public static readonly ContentCode GoblinWolfrider = new( "goblin_wolfrider" );
    public static readonly ContentCode Orc = new( "orc" );
    public static readonly ContentCode Ogre = new( "ogre" );
    public static readonly ContentCode Pig = new( "pig" );
    public static readonly ContentCode Woodcutting = new( "woodcutting" );
    public static readonly ContentCode GoldRocks = new( "gold_rocks" );
    public static readonly ContentCode Cyclops = new( "cyclops" );

    public override string ToString()
    {
        return Value;
    }
}