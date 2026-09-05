namespace Artiact.RealApiTests;

internal static class RealApiLiveGuard
{
    public static void RequireEnabled( string? value )
    {
        if ( !string.Equals( value, "1", StringComparison.Ordinal ) )
        {
            throw new InvalidOperationException(
                "Live read-only verification requires ARTIACT_REAL_API_READONLY=1." );
        }
    }
}
