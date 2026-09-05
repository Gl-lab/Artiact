using Xunit.Abstractions;

namespace Artiact.RealApiTests;

public class RealApiLiveTests( ITestOutputHelper output )
{
    [Fact]
    [Trait( "Category", "RealApiLive" )]
    public async Task ReadOnlySmoke_ExplicitOptInOnly()
    {
        RealApiLiveGuard.RequireEnabled(
            Environment.GetEnvironmentVariable( "ARTIACT_REAL_API_READONLY" ) );

        string repositoryRoot = FindRepositoryRoot();
        string dotenv = await File.ReadAllTextAsync( Path.Combine( repositoryRoot, ".env" ) );
        RealApiConfiguration configuration = RealApiConfiguration.Resolve( DotenvParser.Parse( dotenv ) );
        using HttpClient httpClient = new( ReadOnlyApiVerifier.CreatePrimaryHandler() )
        {
            Timeout = TimeSpan.FromSeconds( 30 )
        };
        ReadOnlyApiVerifier verifier = new( httpClient );

        VerificationSummary summary = await verifier.VerifyAsync( configuration, CancellationToken.None );

        output.WriteLine( "authentication status=success" );
        output.WriteLine( "character status=success" );
        output.WriteLine( $"maps status=success count={summary.MapCount}" );
        output.WriteLine( $"resources status=success count={summary.ResourceCount}" );
        output.WriteLine( $"items status=success count={summary.ItemCount}" );
        output.WriteLine( $"monsters status=success count={summary.MonsterCount}" );
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new( AppContext.BaseDirectory );
        while ( directory != null )
        {
            if ( File.Exists( Path.Combine( directory.FullName, "Artiact.sln" ) ) )
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException( "Repository root was not found." );
    }
}
