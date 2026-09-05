namespace Artiact.RealApiTests;

public class RealApiLiveGuardTests
{
    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void RequireEnabled_WithExactOptInSucceeds()
    {
        RealApiLiveGuard.RequireEnabled( "1" );
    }

    [Theory]
    [Trait( "Category", "RealApiOffline" )]
    [InlineData( null )]
    [InlineData( "0" )]
    [InlineData( "true" )]
    public void RequireEnabled_WithoutExactOptInFails( string? value )
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => RealApiLiveGuard.RequireEnabled( value ) );

        Assert.Contains( "ARTIACT_REAL_API_READONLY=1", exception.Message, StringComparison.Ordinal );
    }
}
