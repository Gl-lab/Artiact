using Artiact.Contracts.Models.Api;
using Xunit;

namespace Artiact.MockService.Tests;

internal static class ScenarioAssertions
{
    public static void CharacterEquals( Character expected, Character actual )
    {
        Assert.Equivalent( expected, actual, strict: true );
        Assert.Equal( expected.Inventory!.Count, actual.Inventory!.Count );
        for ( int index = 0; index < expected.Inventory!.Count; index++ )
        {
            Assert.Equal( expected.Inventory![ index ].Slot, actual.Inventory![ index ].Slot );
            Assert.Equal( expected.Inventory![ index ].Code, actual.Inventory![ index ].Code );
            Assert.Equal( expected.Inventory![ index ].Quantity, actual.Inventory![ index ].Quantity );
        }
    }
}
