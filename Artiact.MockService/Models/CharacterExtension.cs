using Artiact.Contracts.Models.Api;

namespace Artiact.SmartProxy.Models;

public class CharacterExtension : Character
{
    public void AddXp( int xp )
    {
        Xp += xp;
        if ( Xp >= MaxXp )
        {
            Xp = xp - MaxXp;
            Level++;
        }
    }

    public void AddMiningXp( int xp )
    {
        MiningXp += xp;
        if ( MiningXp >= MiningMaxXp )
        {
            MiningXp = xp - MiningMaxXp;
            MiningLevel++;
        }
    }

    public void AddWeaponcraftingXp( int xp )
    {
        WeaponcraftingXp += xp;
        if ( WeaponcraftingXp >= WeaponcraftingMaxXp )
        {
            WeaponcraftingXp = xp - WeaponcraftingMaxXp;
            WeaponcraftingLevel++;
        }
    }
}