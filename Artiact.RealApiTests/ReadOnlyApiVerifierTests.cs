using System.Net;
using System.Text;

namespace Artiact.RealApiTests;

public class ReadOnlyApiVerifierTests
{
    [Theory]
    [Trait( "Category", "RealApiOffline" )]
    [InlineData( "/characters/hero", "character", "CharacterResponse", 2 )]
    [InlineData( "/maps", "maps", "Map", 3 )]
    [InlineData( "/resources", "resources", "ResourceResponse", 4 )]
    [InlineData( "/items", "items", "ItemsResponse", 5 )]
    [InlineData( "/monsters", "monsters", "MonstersResponse", 6 )]
    public async Task VerifyAsync_MissingDataFailsAtAffectedContract(
        string invalidPath,
        string operation,
        string contract,
        int expectedRequests )
    {
        RecordingHandler handler = new( invalidContractPath: invalidPath );
        using HttpClient httpClient = new( handler );
        ReadOnlyApiVerifier verifier = new( httpClient );
        RealApiConfiguration configuration = new(
            new Uri( "https://api.artifactsmmo.com" ), "user", "password", "hero" );

        RealApiVerificationException exception = await Assert.ThrowsAsync<RealApiVerificationException>(
            () => verifier.VerifyAsync( configuration, CancellationToken.None ) );

        Assert.Equal( $"{operation} response does not match {contract}.", exception.Message );
        Assert.Equal( expectedRequests, handler.Requests.Count );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public async Task VerifyAsync_InvalidDestinationMakesNoRequests()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new( handler );
        ReadOnlyApiVerifier verifier = new( httpClient );
        RealApiConfiguration configuration = new(
            new Uri( "https://example.invalid" ), "secret-user", "secret-password", "hero" );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => verifier.VerifyAsync( configuration, CancellationToken.None ) );

        Assert.Empty( handler.Requests );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public async Task VerifyAsync_AuthenticationFailureDoesNotExposeResponseOrCredentials()
    {
        AuthenticationFailureHandler handler = new();
        using HttpClient httpClient = new( handler );
        ReadOnlyApiVerifier verifier = new( httpClient );
        RealApiConfiguration configuration = new(
            new Uri( "https://api.artifactsmmo.com" ), "secret-user", "secret-password", "hero" );

        RealApiVerificationException exception = await Assert.ThrowsAsync<RealApiVerificationException>(
            () => verifier.VerifyAsync( configuration, CancellationToken.None ) );

        Assert.Equal( "authentication failed with HTTP 401.", exception.Message );
        Assert.DoesNotContain( "secret", exception.Message, StringComparison.OrdinalIgnoreCase );
        Assert.DoesNotContain( "server-body", exception.Message, StringComparison.OrdinalIgnoreCase );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public async Task VerifyAsync_NetworkFailureIsSanitized()
    {
        using HttpClient httpClient = new( new ThrowingHandler() );
        ReadOnlyApiVerifier verifier = new( httpClient );
        RealApiConfiguration configuration = new(
            new Uri( "https://api.artifactsmmo.com" ), "secret-user", "secret-password", "secret-hero" );

        RealApiVerificationException exception = await Assert.ThrowsAsync<RealApiVerificationException>(
            () => verifier.VerifyAsync( configuration, CancellationToken.None ) );

        Assert.Equal( "authentication network request failed.", exception.Message );
        Assert.DoesNotContain( "secret", exception.Message, StringComparison.OrdinalIgnoreCase );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public async Task VerifyAsync_ContractFailureDoesNotExposeResponseBody()
    {
        using HttpClient httpClient = new( new MalformedCharacterHandler() );
        ReadOnlyApiVerifier verifier = new( httpClient );
        RealApiConfiguration configuration = new(
            new Uri( "https://api.artifactsmmo.com" ), "user", "password", "hero" );

        RealApiVerificationException exception = await Assert.ThrowsAsync<RealApiVerificationException>(
            () => verifier.VerifyAsync( configuration, CancellationToken.None ) );

        Assert.Equal( "character response does not match CharacterResponse.", exception.Message );
        Assert.DoesNotContain( "sensitive-body", exception.Message, StringComparison.OrdinalIgnoreCase );
    }

    [Theory]
    [Trait( "Category", "RealApiOffline" )]
    [InlineData( "/token", 1 )]
    [InlineData( "/characters/hero", 2 )]
    [InlineData( "/maps", 3 )]
    public async Task VerifyAsync_RedirectFailsWithoutFollowUp( string redirectPath, int expectedRequests )
    {
        RecordingHandler handler = new( redirectPath );
        using HttpClient httpClient = new( handler );
        ReadOnlyApiVerifier verifier = new( httpClient );
        RealApiConfiguration configuration = new(
            new Uri( "https://api.artifactsmmo.com" ), "user", "password", "hero" );

        RealApiVerificationException exception = await Assert.ThrowsAsync<RealApiVerificationException>(
            () => verifier.VerifyAsync( configuration, CancellationToken.None ) );

        Assert.Contains( "HTTP 302", exception.Message, StringComparison.Ordinal );
        Assert.Equal( expectedRequests, handler.Requests.Count );
        Assert.DoesNotContain( handler.Requests, request => request.Contains( "example.invalid", StringComparison.Ordinal ) );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void CreatePrimaryHandler_DisablesAutomaticRedirects()
    {
        using HttpClientHandler handler = ReadOnlyApiVerifier.CreatePrimaryHandler();

        Assert.False( handler.AllowAutoRedirect );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public async Task VerifyAsync_UsesOnlyTokenAndSelectedReadRequests()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new( handler );
        ReadOnlyApiVerifier verifier = new( httpClient );
        RealApiConfiguration configuration = new(
            new Uri( "https://api.artifactsmmo.com" ),
            "user",
            "password",
            "hero" );

        VerificationSummary summary = await verifier.VerifyAsync( configuration, CancellationToken.None );

        Assert.Equal( new[]
        {
            "POST /token",
            "GET /characters/hero",
            "GET /maps?page=1",
            "GET /resources?page=1",
            "GET /items?page=1",
            "GET /monsters?page=1"
        }, handler.Requests );
        Assert.DoesNotContain( handler.Requests, request => request.Contains( "/action/", StringComparison.OrdinalIgnoreCase ) );
        Assert.Equal( 0, summary.MapCount );
        Assert.Equal( 0, summary.ResourceCount );
        Assert.Equal( 0, summary.ItemCount );
        Assert.Equal( 0, summary.MonsterCount );
    }

    private sealed class RecordingHandler(
        string? redirectPath = null,
        string? invalidContractPath = null ) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken )
        {
            Requests.Add( $"{request.Method} {request.RequestUri!.PathAndQuery}" );
            if ( string.Equals( request.RequestUri.AbsolutePath, redirectPath, StringComparison.Ordinal ) )
            {
                return Task.FromResult( new HttpResponseMessage( HttpStatusCode.Found )
                {
                    Headers = { Location = new Uri( "https://example.invalid/redirected" ) }
                } );
            }

            if ( string.Equals( request.RequestUri.AbsolutePath, invalidContractPath, StringComparison.Ordinal ) )
            {
                return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
                {
                    Content = new StringContent( "{}", Encoding.UTF8, "application/json" )
                } );
            }

            string json = request.RequestUri.AbsolutePath switch
            {
                "/token" => "{\"token\":\"test-token\"}",
                "/characters/hero" => "{\"data\":{\"name\":\"hero\",\"inventory\":[]}}",
                _ => "{\"data\":[],\"total\":0,\"page\":1,\"size\":0,\"pages\":0}"
            };
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( json, Encoding.UTF8, "application/json" )
            } );
        }
    }

    private sealed class AuthenticationFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken )
        {
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.Unauthorized )
            {
                Content = new StringContent( "server-body-secret", Encoding.UTF8, "text/plain" )
            } );
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken )
        {
            throw new HttpRequestException( "secret transport details" );
        }
    }

    private sealed class MalformedCharacterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken )
        {
            string content = request.RequestUri!.AbsolutePath == "/token"
                ? "{\"token\":\"test-token\"}"
                : "sensitive-body";
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( content, Encoding.UTF8, "application/json" )
            } );
        }
    }
}
