using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class Character
{
    [JsonPropertyName( "name" )]
    public string Name { get; set; }

    [JsonPropertyName( "account" )]
    public string Account { get; set; }

    [JsonPropertyName( "skin" )]
    public string Skin { get; set; }

    [JsonPropertyName( "level" )]
    public int Level { get; set; }

    [JsonPropertyName( "xp" )]
    public int Xp { get; set; }

    [JsonPropertyName( "max_xp" )]
    public int MaxXp { get; set; }

    [JsonPropertyName( "gold" )]
    public int Gold { get; set; }

    [JsonPropertyName( "speed" )]
    public int Speed { get; set; }

    [JsonPropertyName( "mining_level" )]
    public int MiningLevel { get; set; }

    [JsonPropertyName( "mining_xp" )]
    public int MiningXp { get; set; }

    [JsonPropertyName( "mining_max_xp" )]
    public int MiningMaxXp { get; set; }

    [JsonPropertyName( "woodcutting_level" )]
    public int WoodcuttingLevel { get; set; }

    [JsonPropertyName( "woodcutting_xp" )]
    public int WoodcuttingXp { get; set; }

    [JsonPropertyName( "woodcutting_max_xp" )]
    public int WoodcuttingMaxXp { get; set; }

    [JsonPropertyName( "fishing_level" )]
    public int FishingLevel { get; set; }

    [JsonPropertyName( "fishing_xp" )]
    public int FishingXp { get; set; }

    [JsonPropertyName( "fishing_max_xp" )]
    public int FishingMaxXp { get; set; }

    [JsonPropertyName( "weaponcrafting_level" )]
    public int WeaponcraftingLevel { get; set; }

    [JsonPropertyName( "weaponcrafting_xp" )]
    public int WeaponcraftingXp { get; set; }

    [JsonPropertyName( "weaponcrafting_max_xp" )]
    public int WeaponcraftingMaxXp { get; set; }

    [JsonPropertyName( "gearcrafting_level" )]
    public int GearcraftingLevel { get; set; }

    [JsonPropertyName( "gearcrafting_xp" )]
    public int GearcraftingXp { get; set; }

    [JsonPropertyName( "gearcrafting_max_xp" )]
    public int GearcraftingMaxXp { get; set; }

    [JsonPropertyName( "jewelrycrafting_level" )]
    public int JewelrycraftingLevel { get; set; }

    [JsonPropertyName( "jewelrycrafting_xp" )]
    public int JewelrycraftingXp { get; set; }

    [JsonPropertyName( "jewelrycrafting_max_xp" )]
    public int JewelrycraftingMaxXp { get; set; }

    [JsonPropertyName( "cooking_level" )]
    public int CookingLevel { get; set; }

    [JsonPropertyName( "cooking_xp" )]
    public int CookingXp { get; set; }

    [JsonPropertyName( "cooking_max_xp" )]
    public int CookingMaxXp { get; set; }

    [JsonPropertyName( "alchemy_level" )]
    public int AlchemyLevel { get; set; }

    [JsonPropertyName( "alchemy_xp" )]
    public int AlchemyXp { get; set; }

    [JsonPropertyName( "alchemy_max_xp" )]
    public int AlchemyMaxXp { get; set; }

    [JsonPropertyName( "hp" )]
    public int Hp { get; set; }

    [JsonPropertyName( "max_hp" )]
    public int MaxHp { get; set; }

    [JsonPropertyName( "haste" )]
    public int Haste { get; set; }

    [JsonPropertyName( "critical_strike" )]
    public int CriticalStrike { get; set; }

    [JsonPropertyName( "wisdom" )]
    public int Wisdom { get; set; }

    [JsonPropertyName( "prospecting" )]
    public int Prospecting { get; set; }

    [JsonPropertyName( "attack_fire" )]
    public int AttackFire { get; set; }

    [JsonPropertyName( "attack_earth" )]
    public int AttackEarth { get; set; }

    [JsonPropertyName( "attack_water" )]
    public int AttackWater { get; set; }

    [JsonPropertyName( "attack_air" )]
    public int AttackAir { get; set; }

    [JsonPropertyName( "dmg" )]
    public int Dmg { get; set; }

    [JsonPropertyName( "dmg_fire" )]
    public int DmgFire { get; set; }

    [JsonPropertyName( "dmg_earth" )]
    public int DmgEarth { get; set; }

    [JsonPropertyName( "dmg_water" )]
    public int DmgWater { get; set; }

    [JsonPropertyName( "dmg_air" )]
    public int DmgAir { get; set; }

    [JsonPropertyName( "res_fire" )]
    public int ResFire { get; set; }

    [JsonPropertyName( "res_earth" )]
    public int ResEarth { get; set; }

    [JsonPropertyName( "res_water" )]
    public int ResWater { get; set; }

    [JsonPropertyName( "res_air" )]
    public int ResAir { get; set; }

    [JsonPropertyName( "x" )]
    public int X { get; set; }

    [JsonPropertyName( "y" )]
    public int Y { get; set; }

    [JsonPropertyName( "cooldown" )]
    public int Cooldown { get; set; }

    [JsonPropertyName( "cooldown_expiration" )]
    public DateTime CooldownExpiration { get; set; }

    [JsonPropertyName( "weapon_slot" )]
    public string WeaponSlot { get; set; }

    [JsonPropertyName( "rune_slot" )]
    public string RuneSlot { get; set; }

    [JsonPropertyName( "shield_slot" )]
    public string ShieldSlot { get; set; }

    [JsonPropertyName( "helmet_slot" )]
    public string HelmetSlot { get; set; }

    [JsonPropertyName( "body_armor_slot" )]
    public string BodyArmorSlot { get; set; }

    [JsonPropertyName( "leg_armor_slot" )]
    public string LegArmorSlot { get; set; }

    [JsonPropertyName( "boots_slot" )]
    public string BootsSlot { get; set; }

    [JsonPropertyName( "ring1_slot" )]
    public string Ring1Slot { get; set; }

    [JsonPropertyName( "ring2_slot" )]
    public string Ring2Slot { get; set; }

    [JsonPropertyName( "amulet_slot" )]
    public string AmuletSlot { get; set; }

    [JsonPropertyName( "artifact1_slot" )]
    public string Artifact1Slot { get; set; }

    [JsonPropertyName( "artifact2_slot" )]
    public string Artifact2Slot { get; set; }

    [JsonPropertyName( "artifact3_slot" )]
    public string Artifact3Slot { get; set; }

    [JsonPropertyName( "utility1_slot" )]
    public string Utility1Slot { get; set; }

    [JsonPropertyName( "utility1_slot_quantity" )]
    public int Utility1SlotQuantity { get; set; }

    [JsonPropertyName( "utility2_slot" )]
    public string Utility2Slot { get; set; }

    [JsonPropertyName( "utility2_slot_quantity" )]
    public int Utility2SlotQuantity { get; set; }

    [JsonPropertyName( "bag_slot" )]
    public string BagSlot { get; set; }

    [JsonPropertyName( "task" )]
    public string Task { get; set; }

    [JsonPropertyName( "task_type" )]
    public string TaskType { get; set; }

    [JsonPropertyName( "task_progress" )]
    public int TaskProgress { get; set; }

    [JsonPropertyName( "task_total" )]
    public int TaskTotal { get; set; }

    [JsonPropertyName( "inventory_max_items" )]
    public int InventoryMaxItems { get; set; }

    [JsonPropertyName( "inventory" )]
    public List<Inventory> Inventory { get; set; }
}