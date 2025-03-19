namespace Artiact.Contracts.Models;

public record ResourceSkill( string Value )
{
    public static readonly ResourceSkill Woodcutting = new( "woodcutting" );
    public static readonly ResourceSkill Fishing = new( "fishing" );
    public static readonly ResourceSkill Mining = new( "mining" );
    public static readonly ResourceSkill Alchemy = new( "alchemy" );

    public override string ToString()
    {
        return Value;
    }
}