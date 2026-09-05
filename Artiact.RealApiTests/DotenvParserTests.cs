namespace Artiact.RealApiTests;

public class DotenvParserTests
{
    [Theory]
    [Trait( "Category", "RealApiOffline" )]
    [InlineData( "MISSING_SEPARATOR" )]
    [InlineData( "=missing-key" )]
    [InlineData( " =missing-key" )]
    public void Parse_MalformedLineReportsOnlyLineNumber( string content )
    {
        FormatException exception = Assert.Throws<FormatException>( () => DotenvParser.Parse( content ) );

        Assert.Contains( "line 1", exception.Message, StringComparison.Ordinal );
        Assert.DoesNotContain( content, exception.Message, StringComparison.Ordinal );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void Parse_DuplicateKeyReportsKeyWithoutValues()
    {
        const string content = "API_PASSWORD=first-secret\nAPI_PASSWORD=second-secret";

        FormatException exception = Assert.Throws<FormatException>( () => DotenvParser.Parse( content ) );

        Assert.Contains( "API_PASSWORD", exception.Message, StringComparison.Ordinal );
        Assert.DoesNotContain( "first-secret", exception.Message, StringComparison.Ordinal );
        Assert.DoesNotContain( "second-secret", exception.Message, StringComparison.Ordinal );
    }

    [Theory]
    [Trait( "Category", "RealApiOffline" )]
    [InlineData( "API_PASSWORD='unterminated" )]
    [InlineData( "API_PASSWORD=\"unterminated" )]
    public void Parse_UnmatchedQuotesReportOnlyLineNumber( string content )
    {
        FormatException exception = Assert.Throws<FormatException>( () => DotenvParser.Parse( content ) );

        Assert.Contains( "line 1", exception.Message, StringComparison.Ordinal );
        Assert.DoesNotContain( "unterminated", exception.Message, StringComparison.Ordinal );
    }

    [Fact]
    [Trait( "Category", "RealApiOffline" )]
    public void Parse_SupportedSyntaxReturnsLiteralValues()
    {
        const string content = """
            # comment

            API_BASE_URL=https://api.artifactsmmo.com
            API_USERNAME='user=name'
            API_PASSWORD="$(not-executed)"
            API_CHARACTER=hero # literal suffix
            """;

        IReadOnlyDictionary<string, string> values = DotenvParser.Parse( content );

        Assert.Equal( "https://api.artifactsmmo.com", values["API_BASE_URL"] );
        Assert.Equal( "user=name", values["API_USERNAME"] );
        Assert.Equal( "$(not-executed)", values["API_PASSWORD"] );
        Assert.Equal( "hero # literal suffix", values["API_CHARACTER"] );
    }
}
