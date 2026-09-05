namespace Artiact.RealApiTests;

public class RealApiConfigurationTests
{
    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void Resolve_MissingSettingReportsKeyWithoutValues()
    {
        Dictionary<string, string> values = new( StringComparer.Ordinal )
        {
            ["ApiSettings__BaseUrl"] = "https://api.artifactsmmo.com",
            ["ApiSettings__Username"] = "secret-user",
            ["ApiSettings__Character"] = "hero"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RealApiConfiguration.Resolve( values ) );

        Assert.Contains( "ApiSettings__Password", exception.Message, StringComparison.Ordinal );
        Assert.DoesNotContain( "secret-user", exception.Message, StringComparison.Ordinal );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void ToString_DoesNotExposeCredentials()
    {
        RealApiConfiguration configuration = new(
            new Uri( "https://api.artifactsmmo.com" ), "secret-user", "secret-password", "hero" );

        string text = configuration.ToString();

        Assert.DoesNotContain( "secret-user", text, StringComparison.Ordinal );
        Assert.DoesNotContain( "secret-password", text, StringComparison.Ordinal );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void Resolve_ConflictingAliasReportsSettingWithoutValues()
    {
        Dictionary<string, string> values = new( StringComparer.Ordinal )
        {
            ["ApiSettings__BaseUrl"] = "https://api.artifactsmmo.com",
            ["API_BASE_URL"] = "https://example.invalid",
            ["ApiSettings__Username"] = "user",
            ["ApiSettings__Password"] = "password",
            ["ApiSettings__Character"] = "hero"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RealApiConfiguration.Resolve( values ) );

        Assert.Contains( "ApiSettings__BaseUrl", exception.Message, StringComparison.Ordinal );
        Assert.DoesNotContain( "artifactsmmo", exception.Message, StringComparison.OrdinalIgnoreCase );
        Assert.DoesNotContain( "example", exception.Message, StringComparison.OrdinalIgnoreCase );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void Resolve_AliasKeysReturnsConfiguration()
    {
        Dictionary<string, string> values = new( StringComparer.Ordinal )
        {
            ["API_BASE_URL"] = "https://api.artifactsmmo.com",
            ["API_USERNAME"] = "user",
            ["API_PASSWORD"] = "password",
            ["API_CHARACTER"] = "hero"
        };

        RealApiConfiguration configuration = RealApiConfiguration.Resolve( values );

        Assert.Equal( "hero", configuration.Character );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void Resolve_CanonicalKeysReturnsConfiguration()
    {
        Dictionary<string, string> values = new( StringComparer.Ordinal )
        {
            ["ApiSettings__BaseUrl"] = "https://api.artifactsmmo.com",
            ["ApiSettings__Username"] = "user",
            ["ApiSettings__Password"] = "password",
            ["ApiSettings__Character"] = "hero"
        };

        RealApiConfiguration configuration = RealApiConfiguration.Resolve( values );

        Assert.Equal( new Uri( "https://api.artifactsmmo.com" ), configuration.BaseUri );
        Assert.Equal( "user", configuration.Username );
        Assert.Equal( "password", configuration.Password );
        Assert.Equal( "hero", configuration.Character );
    }
}
