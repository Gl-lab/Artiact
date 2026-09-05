namespace Artiact.RealApiTests;

internal static class DotenvParser
{
    public static IReadOnlyDictionary<string, string> Parse( string content )
    {
        Dictionary<string, string> values = new( StringComparer.Ordinal );
        string[] lines = content.Replace( "\r\n", "\n", StringComparison.Ordinal ).Split( '\n' );

        for ( int index = 0; index < lines.Length; index++ )
        {
            string line = lines[index].Trim();
            if ( line.Length == 0 || line[0] == '#' )
            {
                continue;
            }

            int separator = line.IndexOf( '=' );
            if ( separator <= 0 )
            {
                throw new FormatException( $"Invalid dotenv line {index + 1}." );
            }

            string key = line[..separator].Trim();
            string value = line[( separator + 1 )..].Trim();
            bool startsWithQuote = value.Length > 0 && ( value[0] == '\'' || value[0] == '"' );
            bool endsWithQuote = value.Length > 0 && ( value[^1] == '\'' || value[^1] == '"' );
            if ( startsWithQuote != endsWithQuote ||
                 ( startsWithQuote && value[0] != value[^1] ) )
            {
                throw new FormatException( $"Invalid dotenv line {index + 1}." );
            }

            if ( startsWithQuote )
            {
                value = value[1..^1];
            }

            if ( !values.TryAdd( key, value ) )
            {
                throw new FormatException( $"Duplicate dotenv key {key}." );
            }
        }

        return values;
    }
}
