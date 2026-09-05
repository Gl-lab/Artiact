namespace Artiact.RealApiTests;

internal sealed record RealApiConfiguration(
    Uri BaseUri,
    string Username,
    string Password,
    string Character )
{
    public override string ToString() => "RealApiConfiguration [REDACTED]";

    public static RealApiConfiguration Resolve( IReadOnlyDictionary<string, string> values )
    {
        string baseUrl = GetRequired( values, "ApiSettings__BaseUrl", "API_BASE_URL" );
        return new RealApiConfiguration(
            new Uri( baseUrl, UriKind.Absolute ),
            GetRequired( values, "ApiSettings__Username", "API_USERNAME" ),
            GetRequired( values, "ApiSettings__Password", "API_PASSWORD" ),
            GetRequired( values, "ApiSettings__Character", "API_CHARACTER" ) );
    }

    private static string GetRequired(
        IReadOnlyDictionary<string, string> values,
        string canonicalKey,
        string alias )
    {
        values.TryGetValue( canonicalKey, out string? canonicalValue );
        values.TryGetValue( alias, out string? aliasValue );
        if ( canonicalValue != null && aliasValue != null &&
             !string.Equals( canonicalValue, aliasValue, StringComparison.Ordinal ) )
        {
            throw new InvalidOperationException( $"Conflicting configuration setting {canonicalKey}." );
        }

        string? value = canonicalValue ?? aliasValue;
        if ( string.IsNullOrWhiteSpace( value ) )
        {
            throw new InvalidOperationException( $"Missing configuration setting {canonicalKey}." );
        }

        return value;
    }
}
