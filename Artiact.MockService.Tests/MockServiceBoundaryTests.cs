using Xunit;

namespace Artiact.MockService.Tests;

public sealed class MockServiceBoundaryTests
{
    [Fact]
    public void Compatibility_authority_rejects_everything_except_exact_http_localhost()
    {
        Assert.Equal( new Uri( "http://localhost" ), MockServiceFactory.RequireLoopbackAuthority( new Uri( "http://localhost" ) ) );
        Assert.Throws<InvalidOperationException>( () => MockServiceFactory.RequireLoopbackAuthority( new Uri( "https://localhost" ) ) );
        Assert.Throws<InvalidOperationException>( () => MockServiceFactory.RequireLoopbackAuthority( new Uri( "http://127.0.0.1" ) ) );
        Assert.Throws<InvalidOperationException>( () => MockServiceFactory.RequireLoopbackAuthority( new Uri( "http://localhost:80" ) ) );
        Assert.Throws<InvalidOperationException>( () => MockServiceFactory.RequireLoopbackAuthority( new Uri( "http://localhost/" ) ) );
        Assert.Throws<InvalidOperationException>( () => MockServiceFactory.RequireLoopbackAuthority( new Uri( "https://example.invalid" ) ) );
    }

    [Fact]
    public void MockService_contains_only_the_approved_local_surface()
    {
        string root = FindRepositoryRoot();
        string project = File.ReadAllText( Path.Combine( root, "Artiact.MockService", "Artiact.MockService.csproj" ) );
        string program = File.ReadAllText( Path.Combine( root, "Artiact.MockService", "Program.cs" ) );
        string store = File.ReadAllText( Path.Combine( root, "Artiact.MockService", "Services", "MockScenarioStore.cs" ) );
        string launchSettings = File.ReadAllText( Path.Combine( root, "Artiact.MockService", "Properties", "launchSettings.json" ) );
        string productionSource = String.Join( "\n", Directory.GetFiles( Path.Combine( root, "Artiact.MockService" ), "*.cs", SearchOption.AllDirectories )
                                                           .Where( path => !path.Contains( $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal ) )
                                                           .Select( File.ReadAllText ) );
        string factorySource = File.ReadAllText( Path.Combine( root, "Artiact.MockService.Tests", "MockServiceFactory.cs" ) );
        string configurationJson = String.Join( "\n", Directory.GetFiles( Path.Combine( root, "Artiact.MockService" ), "appsettings*.json" )
                                                            .Select( File.ReadAllText ) );

        Assert.DoesNotContain( "Yarp.ReverseProxy", project, StringComparison.Ordinal );
        Assert.DoesNotContain( "Swashbuckle", project, StringComparison.Ordinal );
        Assert.DoesNotContain( "Microsoft.AspNetCore.OpenApi", project, StringComparison.Ordinal );
        Assert.DoesNotContain( "AddSwaggerGen", program, StringComparison.Ordinal );
        Assert.DoesNotContain( "AddReverseProxy", program, StringComparison.Ordinal );
        Assert.DoesNotContain( "MapReverseProxy", program, StringComparison.Ordinal );
        Assert.DoesNotContain( "UseHttpsRedirection", program, StringComparison.Ordinal );
        Assert.Contains( "ListenLocalhost( 5000 )", program, StringComparison.Ordinal );
        Assert.DoesNotContain( "\"https\"", launchSettings, StringComparison.Ordinal );
        Assert.DoesNotContain( "iisSettings", launchSettings, StringComparison.Ordinal );
        Assert.Contains( "\"applicationUrl\": \"http://localhost:5000\"", launchSettings, StringComparison.Ordinal );
        Assert.True( File.Exists( Path.Combine( root, "Artiact.MockService", "BasicMiningScenario.json" ) ) );
        Assert.Contains( "environment.ContentRootPath", store, StringComparison.Ordinal );
        Assert.DoesNotContain( "AddHostedService", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "HttpClient", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "IHttpClientFactory", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "UseUserSecrets", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "Task.Delay", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "Thread.Sleep", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "DateTime.Now", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "DateTime.UtcNow", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "Random", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "SocketsHttpHandler", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "TcpClient", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "TcpListener", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "ClientWebSocket", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "PeriodicTimer", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "System.Threading.Timer", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "X509Certificate", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "GetEnvironmentVariable", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "DotEnv", productionSource, StringComparison.Ordinal );
        Assert.DoesNotContain( "ReverseProxy", configurationJson, StringComparison.Ordinal );
        Assert.Contains( "builder.Configuration.Sources.Clear()", program, StringComparison.Ordinal );
        Assert.DoesNotContain( "private bool _initialized", store, StringComparison.Ordinal );
        Assert.DoesNotContain( "IsInitialized", store, StringComparison.Ordinal );
        Assert.Contains( "configuration.Sources.Clear()", factorySource, StringComparison.Ordinal );
        Assert.Contains( "WebApplicationFactory<Program>", factorySource, StringComparison.Ordinal );
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new( AppContext.BaseDirectory );
        while ( directory != null && !File.Exists( Path.Combine( directory.FullName, "Artiact.sln" ) ) )
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException( "Artiact.sln was not found." );
    }
}
