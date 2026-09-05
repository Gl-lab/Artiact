using Artiact.Contracts.Models.Api;

namespace Artiact.MockService.Tests;

internal static class ExpectedScenario
{
    public static Character Character() => new()
    {
        Name = "MockHero",
        Account = "mock",
        Skin = "men1",
        Level = 1,
        Xp = 0,
        MaxXp = 150,
        Gold = 0,
        Speed = 100,
        MiningLevel = 1,
        MiningXp = 0,
        MiningMaxXp = 150,
        WoodcuttingLevel = 1,
        WoodcuttingXp = 0,
        WoodcuttingMaxXp = 150,
        FishingLevel = 1,
        FishingXp = 0,
        FishingMaxXp = 150,
        WeaponcraftingLevel = 1,
        WeaponcraftingXp = 0,
        WeaponcraftingMaxXp = 150,
        GearcraftingLevel = 1,
        GearcraftingXp = 0,
        GearcraftingMaxXp = 150,
        JewelrycraftingLevel = 1,
        JewelrycraftingXp = 0,
        JewelrycraftingMaxXp = 150,
        CookingLevel = 1,
        CookingXp = 0,
        CookingMaxXp = 150,
        AlchemyLevel = 1,
        AlchemyXp = 0,
        AlchemyMaxXp = 150,
        Hp = 120,
        MaxHp = 120,
        Haste = 0,
        CriticalStrike = 5,
        Wisdom = 0,
        Prospecting = 0,
        AttackFire = 0,
        AttackEarth = 4,
        AttackWater = 0,
        AttackAir = 0,
        Dmg = 0,
        DmgFire = 0,
        DmgEarth = 0,
        DmgWater = 0,
        DmgAir = 0,
        ResFire = 0,
        ResEarth = 0,
        ResWater = 0,
        ResAir = 0,
        X = 0,
        Y = 0,
        Cooldown = 0,
        CooldownExpiration = new DateTime( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc ),
        WeaponSlot = "wooden_stick",
        RuneSlot = "",
        ShieldSlot = "",
        HelmetSlot = "",
        BodyArmorSlot = "",
        LegArmorSlot = "",
        BootsSlot = "",
        Ring1Slot = "",
        Ring2Slot = "",
        AmuletSlot = "",
        Artifact1Slot = "",
        Artifact2Slot = "",
        Artifact3Slot = "",
        Utility1Slot = "",
        Utility1SlotQuantity = 0,
        Utility2Slot = "",
        Utility2SlotQuantity = 0,
        BagSlot = "",
        Task = "",
        TaskType = "",
        TaskProgress = 0,
        TaskTotal = 0,
        InventoryMaxItems = 20,
        Inventory = Enumerable.Range( 1, 20 ).Select( slot => new Inventory { Slot = slot, Code = "", Quantity = 0 } ).ToList()
    };

    public static Map Maps() => new()
    {
        Data =
        [
            new MapPlace
            {
                Name = "Origin",
                Skin = "forest",
                X = 0,
                Y = 0,
                Content = new Artiact.Contracts.Models.Api.Content { Type = "", Code = "" }
            },
            new MapPlace
            {
                Name = "Copper Rocks",
                Skin = "rocks",
                X = 2,
                Y = 0,
                Content = new Artiact.Contracts.Models.Api.Content { Type = "resource", Code = "copper_rocks" }
            }
        ],
        Total = 2,
        Page = 1,
        Size = 2,
        Pages = 1
    };

    public static ResourceResponse Resources() => new()
    {
        Data =
        [
            new ResourceDatum
            {
                Name = "Copper Rocks",
                Code = "copper_rocks",
                Skill = "mining",
                Level = 1,
                Drops = [ new Drop { Code = "copper_ore", MinQuantity = 1, MaxQuantity = 1, Rate = 1 } ]
            }
        ],
        Total = 1,
        Page = 1,
        Size = 1,
        Pages = 1
    };

    public static ItemsResponse Items() => new()
    {
        Data =
        [
            new ItemDatum
            {
                Name = "Copper Ore",
                Code = "copper_ore",
                Level = 1,
                Type = "resource",
                Subtype = "mining",
                Description = "Basic mining ore.",
                Effects = [],
                Craft = null,
                Tradeable = false
            }
        ],
        Total = 1,
        Page = 1,
        Size = 1,
        Pages = 1
    };

    public static MonstersResponse Monsters() => new()
    {
        Data = [],
        Total = 0,
        Page = 1,
        Size = 0,
        Pages = 1
    };
}
