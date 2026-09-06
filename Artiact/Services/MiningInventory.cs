using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

internal static class MiningInventory
{
    public static bool TryRead(Character? character, out int used, out int free)
    {
        used = 0;
        free = 0;
        if (character is null || character.InventoryMaxItems < 0 || character.Inventory is null)
            return false;

        int total = 0;
        foreach (Inventory? item in character.Inventory)
        {
            if (item is null || item.Quantity < 0 ||
                item.Quantity > 0 && string.IsNullOrWhiteSpace(item.Code))
                return false;
            try { total = checked(total + item.Quantity); }
            catch (OverflowException) { return false; }
        }
        if (total > character.InventoryMaxItems)
            return false;
        used = total;
        free = character.InventoryMaxItems - total;
        return true;
    }
}
