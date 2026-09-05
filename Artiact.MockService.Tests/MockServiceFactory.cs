using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Artiact.MockService.Tests;

public sealed class MockServiceFactory : WebApplicationFactory<Program>
{
    public static Uri RequireLoopbackAuthority( Uri authority )
    {
        const string expected = "http://localhost";
        if ( !String.Equals( authority.OriginalString, expected, StringComparison.Ordinal ) )
        {
            throw new InvalidOperationException( "Compatibility tests require exact http://localhost authority." );
        }

        return authority;
    }

    protected override void ConfigureWebHost( IWebHostBuilder builder )
    {
        builder.ConfigureAppConfiguration( ( _, configuration ) =>
        {
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection( new Dictionary<string, string?>
            {
                [ "MockService:Scenario" ] = "basic-mining"
            } );
        } );
    }
}
