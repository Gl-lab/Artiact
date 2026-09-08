using Xunit.Abstractions;

namespace Artiact.RealApiTests;

public class RealApiLiveTests( ITestOutputHelper output )
{
    [Fact]
    [Trait("Category", "RealApiAutonomousInspect")]
    public async Task AutonomousInspect_ExplicitReadOnlyOptIn()
    {
        RealApiLiveGuard.RequireEnabled(Environment.GetEnvironmentVariable("ARTIACT_REAL_API_READONLY"));
        var configuration = RealApiConfiguration.Resolve(DotenvParser.Parse(
            await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), ".env"))));
        using var http = new HttpClient(ReadOnlyApiVerifier.CreatePrimaryHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var report = await new ReadOnlyApiVerifier(http).InspectReportAsync(configuration, deadline.Token, autonomous: true);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            report.Character, report.Fingerprint, report.WorldFingerprint, report.PolicyDigest, report.ObservedAt,
            report.MapId, report.Capacity, report.FreeUnits, report.Stock,
            Status = report.Decision.Status.ToString(), report.Decision.Reason, report.Decision.Attempts, report.Decision.Command,
            Rejections = report.Decision.Candidates.GroupBy(x => x.Rejection ?? "Eligible").ToDictionary(x => x.Key, x => x.Count()),
            Paths = report.Decision.Candidates.Where(x => x.Path is not null).Select(x => new
            {
                x.Id, x.Rejection, Target = x.Discovery?.Target, x.Path!.Remaining, x.Path.ExpectedProgress,
                x.Path.UnitSeconds, x.Path.TravelSeconds, x.Path.WorkCommand
            })
        }));
        Assert.Equal(0, report.Decision.Attempts);
        Assert.True(report.Decision.Status == Artiact.Services.Strategy.StrategyStatus.Selected,
            $"Autonomous inspection not actionable: {report.Decision.Reason}");
    }

    [Fact]
    [Trait("Category", "RealApiInspect")]
    public async Task MiningInspect_ExplicitReadOnlyOptIn()
    {
        RealApiLiveGuard.RequireEnabled(Environment.GetEnvironmentVariable("ARTIACT_REAL_API_READONLY"));
        var configuration = RealApiConfiguration.Resolve(DotenvParser.Parse(
            await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), ".env"))));
        using var http = new HttpClient(ReadOnlyApiVerifier.CreatePrimaryHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var decision = await new ReadOnlyApiVerifier(http).InspectAsync(configuration, deadline.Token);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(decision));
        Assert.Equal(0, decision.Attempts);
        Assert.True(decision.Status is Artiact.Services.Strategy.StrategyStatus.Selected or Artiact.Services.Strategy.StrategyStatus.Completed,
            $"Inspection was not actionable: {decision.Reason}");
    }

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
