using System.Text.Json;
using System.Text.Json.Serialization;
using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Microsoft.Extensions.Logging;

namespace Artiact.SmartProxy.Services;

public class ActionService : IActionService
{
    private readonly ICharacterCache _characterCache;
    private readonly string _mockDataPath = "MockData.json";
    private readonly ILogger<ActionService> _logger;

    public ActionService( ICharacterCache characterCache, ILogger<ActionService> logger )
    {
        _characterCache = characterCache;
        _logger = logger;
    }

    public Character MoveAction( string? characterName, MoveRequest request )
    {
        CharacterExtension? character = _characterCache.GetCharacter( characterName );
        if ( character == null ) throw new Exception( $"Character {characterName} not found" );

        _logger.LogInformation( "Персонаж {CharacterName} переместился с ({OldX}, {OldY}) на ({NewX}, {NewY})", 
            characterName, character.X, character.Y, request.X, request.Y );

        character.X = request.X;
        character.Y = request.Y;
        _characterCache.UpdateCharacter( characterName, character );
        return character;
    }

    public Character GatheringAction( string? characterName )
    {
        CharacterExtension? character = _characterCache.GetCharacter( characterName );
        if ( character == null ) throw new Exception( $"Character {characterName} not found" );

        List<MockAction> mockData = LoadMockData();
        MockAction? action = mockData.FirstOrDefault( a =>
            a.Action == "gathering" &&
            a.Position.X == character.X &&
            a.Position.Y == character.Y );

         if ( action == null ) throw new ApplicationException( $"No Gathering action found position ({character.X}, {character.Y})" );

        _logger.LogInformation( "Персонаж {CharacterName} начал сбор ресурсов на позиции ({X}, {Y})", 
            characterName, character.X, character.Y );

        ApplyChanges( character, action.Changes );
        _characterCache.UpdateCharacter( characterName, character );
        return character;
    }

    public Character CraftingAction( string? characterName, Item item )
    {
        CharacterExtension? character = _characterCache.GetCharacter( characterName );
        if ( character == null ) throw new Exception( $"Character {characterName} not found" );

        List<MockAction> mockData = LoadMockData();
        MockAction? action = mockData.FirstOrDefault( a =>
            a.Action == "crafting" &&
            a.Position.X == character.X &&
            a.Position.Y == character.Y &&
            a.Target == item.Code );

        if ( action == null ) throw new ApplicationException( $"No crafting action found for item {item.Code} at position ({character.X}, {character.Y})" );

        _logger.LogInformation( "Персонаж {CharacterName} начал крафт предмета {ItemCode} в количестве {Quantity} на позиции ({X}, {Y})", 
            characterName, item.Code, item.Quantity, character.X, character.Y );

        ApplyChanges( character, action.Changes, item.Quantity );
        _characterCache.UpdateCharacter( characterName, character );
        return character;
    }

    private List<MockAction> LoadMockData()
    {
        if ( !File.Exists( _mockDataPath ) ) return new List<MockAction>();

        string json = File.ReadAllText( _mockDataPath );
        return JsonSerializer.Deserialize<List<MockAction>>( json ) ?? new List<MockAction>();
    }

    private void ApplyChanges( CharacterExtension character, Changes changes, int multiplier = 1 )
    {
        // Применяем изменения опыта
        if ( changes.MiningXp != null )
        {
            int oldXp = character.MiningXp;
            character.AddMiningXp( changes.MiningXp.Difference??0 * multiplier );
            _logger.LogInformation( "Изменение опыта добычи: {OldXp} -> {NewXp} (+{Difference})", 
                oldXp, character.MiningXp, changes.MiningXp.Difference * multiplier );
        }

        if ( changes.WeaponcraftingXp != null )
        {
            int oldXp = character.WeaponcraftingXp;
            character.AddWeaponcraftingXp( changes.WeaponcraftingXp.Difference??0 * multiplier );
            _logger.LogInformation( "Изменение опыта крафта оружия: {OldXp} -> {NewXp} (+{Difference})", 
                oldXp, character.WeaponcraftingXp, changes.WeaponcraftingXp.Difference * multiplier );
        }

        // Применяем изменения инвентаря
        if ( changes.Inventory != null )
        {
            foreach ( InventoryChange itemChange in changes.Inventory )
            {
                int difference = itemChange.Difference * multiplier;
               // if ( difference < 0 ) throw new ApplicationException( $"Cannot remove more items than available for {itemChange.Code}" );

                Inventory? existingItem = character.Inventory.FirstOrDefault( i => i.Code == itemChange.Code );
                if ( existingItem != null )
                {
                    int oldQuantity = existingItem.Quantity;
                    existingItem.Quantity += difference;
                    _logger.LogInformation( "Изменение количества предмета {ItemCode}: {OldQuantity} -> {NewQuantity} ({Difference:+0;-#})", 
                        itemChange.Code, oldQuantity, existingItem.Quantity, difference );
                    
                    if ( existingItem.Quantity <= 0 )
                    {
                        character.Inventory.Remove( existingItem );
                        _logger.LogInformation( "Предмет {ItemCode} удален из инвентаря", itemChange.Code );
                    }
                }
                else if ( difference > 0 )
                {
                    character.Inventory.Add( new Inventory
                    {
                        Code = itemChange.Code,
                        Quantity = difference
                    } );
                    _logger.LogInformation( "Добавлен новый предмет {ItemCode} в количестве {Quantity}", 
                        itemChange.Code, difference );
                }
            }
        }
    }
}

public class MockAction
{
    
    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("position")]
    public Position Position { get; set; }

    [JsonPropertyName("changes")]
    public Changes Changes { get; set; }

    [JsonPropertyName("target")]
    public string Target { get; set; }
}

public class Position
{
    [JsonPropertyName("x")]
    public int? X { get; set; }

    [JsonPropertyName("y")]
    public int? Y { get; set; }
}

public class Changes
{
    [JsonPropertyName("mining_xp")]
    public XpChange? MiningXp { get; set; }
    [JsonPropertyName("weaponcrafting_xp")]
    public XpChange? WeaponcraftingXp { get; set; }
    [JsonPropertyName("inventory")]
    public List<InventoryChange>? Inventory { get; set; }
}

public class XpChange
{
    [JsonPropertyName("difference")]
    public int? Difference { get; set; }
}

public class InventoryChange
{
    [JsonPropertyName("code")]
    public string Code { get; set; }
    [JsonPropertyName("difference")]
    public int Difference { get; set; }
}