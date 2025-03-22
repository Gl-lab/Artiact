using System.Text.Json;
using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;

namespace Artiact.SmartProxy.Services;

public class CharacterCache : ICharacterCache
{
    private readonly Dictionary<string?, CharacterExtension> _characters = new();
    // private readonly string _logFilePath = "character_changes.log";

    public void UpdateCharacter( string? name, CharacterExtension character )
    {
        _characters[ name ] = character;
    }
    
    public CharacterExtension? GetCharacter( string? characterName )
    {
        return _characters.TryGetValue( characterName, out CharacterExtension? character ) ? character : null;
    }

    // public void UpdateCharacter( string? characterName,
    //                              Character newData,
    //                              string? action )
    // {
    //     if ( !_characters.TryGetValue( characterName, out Character? existingCharacter ) )
    //     {
    //         _characters[ characterName ] = newData;
    //         return;
    //     }
    //
    //     Dictionary<string, object> changes = new();
    //
    //     // Добавляем текущие координаты
    //     changes[ "position" ] = new { x = newData.X, y = newData.Y };
    //
    //     // Сравниваем основные числовые параметры
    //     CompareAndAddChange( changes, "xp", existingCharacter.Xp, newData.Xp );
    //     CompareAndAddChange( changes, "gold", existingCharacter.Gold, newData.Gold );
    //     CompareAndAddChange( changes, "mining_xp", existingCharacter.MiningXp, newData.MiningXp );
    //     CompareAndAddChange( changes, "fishing_xp", existingCharacter.FishingXp, newData.FishingXp );
    //     CompareAndAddChange( changes, "weaponcrafting_xp", existingCharacter.WeaponcraftingXp,
    //         newData.WeaponcraftingXp );
    //     CompareAndAddChange( changes, "gearcrafting_xp", existingCharacter.GearcraftingXp, newData.GearcraftingXp );
    //     CompareAndAddChange( changes, "jewelrycrafting_xp", existingCharacter.JewelrycraftingXp,
    //         newData.JewelrycraftingXp );
    //     CompareAndAddChange( changes, "cooking_xp", existingCharacter.CookingXp, newData.CookingXp );
    //     CompareAndAddChange( changes, "alchemy_xp", existingCharacter.AlchemyXp, newData.AlchemyXp );
    //
    //     // Сравниваем инвентарь
    //     List<object> inventoryChanges = new();
    //
    //     // Находим добавленные или измененные предметы
    //     foreach ( Inventory newItem in newData.Inventory )
    //     {
    //         Inventory? existingItem = existingCharacter.Inventory.FirstOrDefault( i => i.Slot == newItem.Slot );
    //         if ( existingItem == null )
    //         {
    //             inventoryChanges.Add( new
    //             {
    //                 type = "added",
    //                 //   slot = newItem.Slot,
    //                 code = newItem.Code,
    //                 difference = newItem.Quantity
    //             } );
    //         }
    //         else if ( existingItem.Code != newItem.Code || existingItem.Quantity != newItem.Quantity )
    //         {
    //             inventoryChanges.Add( new
    //             {
    //                 type = "changed",
    //                 //slot = newItem.Slot,
    //                 code = newItem.Code,
    //                 difference = newItem.Quantity - existingItem.Quantity
    //                 // old_quantity = existingItem.Quantity,
    //                 // new_quantity = newItem.Quantity
    //             } );
    //         }
    //     }
    //
    //     // Находим удаленные предметы
    //     foreach ( Inventory oldItem in existingCharacter.Inventory )
    //     {
    //         if ( !newData.Inventory.Any( i => i.Slot == oldItem.Slot ) )
    //         {
    //             inventoryChanges.Add( new
    //             {
    //                 type = "removed",
    //                 //   slot = oldItem.Slot,
    //                 code = oldItem.Code
    //                 //  quantity = oldItem.Quantity
    //             } );
    //         }
    //     }
    //
    //     if ( inventoryChanges.Any() )
    //     {
    //         changes[ "inventory" ] = inventoryChanges;
    //     }
    //
    //     // Если есть изменения, логируем их
    //     if ( changes.Any() )
    //     {
    //         var logEntry = new
    //         {
    //             timestamp = DateTime.UtcNow,
    //             character = characterName,
    //             action,
    //             changes
    //         };
    //
    //         string jsonLog = JsonSerializer.Serialize( logEntry, new JsonSerializerOptions
    //         {
    //             WriteIndented = true
    //         } );
    //
    //         File.AppendAllText( _logFilePath, jsonLog + Environment.NewLine );
    //     }
    //
    //     _characters[ characterName ] = newData;
    // }
    //
    // private void CompareAndAddChange( Dictionary<string, object> changes,
    //                                   string field,
    //                                   int oldValue,
    //                                   int newValue )
    // {
    //     if ( oldValue != newValue )
    //     {
    //         var change = new
    //         {
    //             // old_value = oldValue,
    //             // new_value = newValue,
    //             difference = newValue - oldValue,
    //             type = field
    //         };
    //         changes[ field ] = change;
    //     }
    // }
}