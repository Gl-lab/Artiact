namespace Artiact.RealApiTests;

public class DestinationValidatorTests
{
    [Theory]
    [Trait( "Category", "RealApiOffline" )]
    [InlineData( "http://api.artifactsmmo.com" )]
    [InlineData( "https://example.invalid" )]
    [InlineData( "https://user@api.artifactsmmo.com" )]
    [InlineData( "https://api.artifactsmmo.com/#fragment" )]
    [InlineData( "https://api.artifactsmmo.com:8443" )]
    public void Validate_UnapprovedDestinationFails( string value )
    {
        Assert.Throws<InvalidOperationException>(
            () => DestinationValidator.Validate( new Uri( value ) ) );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void Validate_OfficialHttpsApiSucceeds()
    {
        Uri result = DestinationValidator.Validate( new Uri( "https://api.artifactsmmo.com/v1" ) );

        Assert.Equal( "api.artifactsmmo.com", result.Host );
    }
}
