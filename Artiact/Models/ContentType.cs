namespace Artiact.Models;

public record ContentType( string Value )
{
    public static readonly ContentType None = new( "none" );
    public static readonly ContentType Resource = new( "resource" );
    public static readonly ContentType Monster = new( "monster" );
    public static readonly ContentType Workshop = new( "workshop" );

    public override string ToString()
    {
        return Value;
    }
}