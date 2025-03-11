using System.ComponentModel;

namespace Artiact.Models;
public record DropCode(string Value)
{
    public override string ToString() => Value;

    public static readonly DropCode AshWood = new("ash_wood");
    public static readonly DropCode Sap = new("sap");
    public static readonly DropCode Gudgeon = new("gudgeon");
    public static readonly DropCode Algae = new("algae");
    public static readonly DropCode CopperOre = new("copper_ore");
    public static readonly DropCode TopazStone = new("topaz_stone");
    public static readonly DropCode Topaz = new("topaz");
    public static readonly DropCode Emerald = new("emerald");
    public static readonly DropCode EmeraldStone = new("emerald_stone");
    public static readonly DropCode Ruby = new("ruby");
    public static readonly DropCode RubyStone = new("ruby_stone");
    public static readonly DropCode Sapphire = new("sapphire");
    public static readonly DropCode SapphireStone = new("sapphire_stone");
    public static readonly DropCode Sunflower = new("sunflower");
    public static readonly DropCode Shrimp = new("shrimp");
    public static readonly DropCode GoldenShrimp = new("golden_shrimp");
    public static readonly DropCode IronOre = new("iron_ore");
    public static readonly DropCode SpruceWood = new("spruce_wood");
    public static readonly DropCode Apple = new("apple");
    public static readonly DropCode Coal = new("coal");
    public static readonly DropCode Trout = new("trout");
    public static readonly DropCode SilverChalice = new("silver_chalice");
    public static readonly DropCode BirchWood = new("birch_wood");
    public static readonly DropCode NettleLeaf = new("nettle_leaf");
    public static readonly DropCode Bass = new("bass");
    public static readonly DropCode GoldOre = new("gold_ore");
    public static readonly DropCode DeadWood = new("dead_wood");
    public static readonly DropCode StrangeOre = new("strange_ore");
    public static readonly DropCode DiamondStone = new("diamond_stone");
    public static readonly DropCode Diamond = new("diamond");
    public static readonly DropCode MagicWood = new("magic_wood");
    public static readonly DropCode MagicSap = new("magic_sap");
    public static readonly DropCode Salmon = new("salmon");
    public static readonly DropCode GoldenChalice = new("golden_chalice");
    public static readonly DropCode GlowstemLeaf = new("glowstem_leaf");
    public static readonly DropCode MithrilOre = new("mithril_ore");
    public static readonly DropCode MapleWood = new("maple_wood");
    public static readonly DropCode MapleSap = new("maple_sap");
}