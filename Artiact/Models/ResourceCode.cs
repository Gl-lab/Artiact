namespace Artiact.Models;

public record ResourceCode( string Value )
{
    public static readonly ResourceCode AshTree = new( "ash_tree" );
    public static readonly ResourceCode GudgeonFishingSpot = new( "gudgeon_fishing_spot" );
    public static readonly ResourceCode CopperRocks = new( "copper_rocks" );
    public static readonly ResourceCode SunflowerField = new( "sunflower_field" );
    public static readonly ResourceCode ShrimpFishingSpot = new( "shrimp_fishing_spot" );
    public static readonly ResourceCode IronRocks = new( "iron_rocks" );
    public static readonly ResourceCode SpruceTree = new( "spruce_tree" );
    public static readonly ResourceCode CoalRocks = new( "coal_rocks" );
    public static readonly ResourceCode TroutFishingSpot = new( "trout_fishing_spot" );
    public static readonly ResourceCode BirchTree = new( "birch_tree" );
    public static readonly ResourceCode Nettle = new( "nettle" );
    public static readonly ResourceCode BassFishingSpot = new( "bass_fishing_spot" );
    public static readonly ResourceCode GoldRocks = new( "gold_rocks" );
    public static readonly ResourceCode DeadTree = new( "dead_tree" );
    public static readonly ResourceCode StrangeRocks = new( "strange_rocks" );
    public static readonly ResourceCode MagicTree = new( "magic_tree" );
    public static readonly ResourceCode SalmonFishingSpot = new( "salmon_fishing_spot" );
    public static readonly ResourceCode Glowstem = new( "glowstem" );
    public static readonly ResourceCode MithrilRocks = new( "mithril_rocks" );
    public static readonly ResourceCode MapleTree = new( "maple_tree" );

    public override string ToString()
    {
        return Value;
    }
}