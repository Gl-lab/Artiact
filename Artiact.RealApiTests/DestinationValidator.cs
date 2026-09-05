namespace Artiact.RealApiTests;

internal static class DestinationValidator
{
    private const string OfficialHost = "api.artifactsmmo.com";

    public static Uri Validate( Uri baseUri )
    {
        bool approved = baseUri.IsAbsoluteUri &&
                        string.Equals( baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase ) &&
                        string.Equals( baseUri.IdnHost, OfficialHost, StringComparison.OrdinalIgnoreCase ) &&
                        baseUri.IsDefaultPort &&
                        string.IsNullOrEmpty( baseUri.UserInfo ) &&
                        string.IsNullOrEmpty( baseUri.Fragment );
        if ( !approved )
        {
            throw new InvalidOperationException( "Real API destination is not approved." );
        }

        return baseUri;
    }
}
