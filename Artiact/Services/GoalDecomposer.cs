using Artiact.Client;
using Artiact.Models;
using Artiact.Models.Api;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;

namespace Artiact.Services;

public interface IGoalDecomposer
{
    Task<Goal> DecomposeGoal( Goal goal, Character character );
}

public class GoalDecomposer : IGoalDecomposer
{
    private readonly ILogger<GoalDecomposer> _logger;

    private readonly IGameClient _gameClient;
    private readonly IWearCraftTargetFinder _wearCraftTargetFinder;

    public GoalDecomposer( ILogger<GoalDecomposer> logger,
                           IGameClient gameClient,
                           IWearCraftTargetFinder wearCraftTargetFinder )
    {
        _logger = logger;

        _gameClient = gameClient;
        _wearCraftTargetFinder = wearCraftTargetFinder;
    }

    public async Task<Goal> DecomposeGoal( Goal goal, Character character )
    {
        switch ( goal )
        {
            case MiningGoal miningGoal:
                return DecomposeMiningGoal( miningGoal, character );


            case SpendResourcesGoal spendGoal:
                DecomposeSpendResourcesGoal( spendGoal );
                break;
        }

        return goal;
    }

    private Goal DecomposeMiningGoal( MiningGoal miningGoal, Character character )
    {
        // Проверяем текущее состояние инвентаря
        int currentInventorySpace = character.InventoryMaxItems;
        int usedInventorySpace = character.Inventory.Sum( item => item.Quantity );
        int availableSpace = currentInventorySpace - usedInventorySpace;

        _logger.LogDebug( $"Checking inventory space: {availableSpace} slots available" );

        // Если места достаточно, возвращаем исходную цель
        if ( availableSpace >= 10 ) // Предполагаем, что нам нужно минимум 10 слотов для майнинга
        {
            return miningGoal;
        }

        // Если места недостаточно, создаем цель для освобождения инвентаря
        _logger.LogDebug( "Not enough inventory space, creating SpendResourcesGoal" );

        // Получаем список ресурсов, которые можно потратить
        List<ResourceToSpend> resourcesToSpend = new List<ResourceToSpend>();

        // Проверяем возможность крафта предметов из имеющихся ресурсов
        CraftTarget? craftTarget = _wearCraftTargetFinder.FindTarget( character.Inventory.Select( x => new Item
        {
            Code = x.Code,
            Quantity = x.Quantity
        } ).ToList() );

        if ( craftTarget != null )
        {
            // Если нашли что можно скрафтить, добавляем ресурсы в список для крафта
            foreach ( CraftStep step in craftTarget.Steps )
            {
                // resourcesToSpend.Add( new ResourceToSpend
                // {
                //     Items = step.RequiredItems,
                //     Method = SpendMethod.Craft
                // } );
            }
        }
        else
        {
            // // Если крафтить нечего, ищем ресурсы которые можно удалить или переработать
            // var resourceItems = character.Inventory
            //                              .Where( item => item.Code == "resource" && item.Quantity > 0 )
            //                              .OrderBy( item => item.Level ) // Начинаем с низкоуровневых ресурсов
            //                              .Take( 3 ) // Берем первые 3 типа ресурсов
            //                              .ToList();
            //
            // if ( resourceItems.Any() )
            // {
            //     resourcesToSpend.Add( new ResourceToSpend
            //     {
            //         Items = resourceItems,
            //         Method = SpendMethod.Recycle // Предпочитаем переработку удалению
            //     } );
            // }
        }

        // Создаем подцель для освобождения места
        SpendResourcesGoal spendResourcesGoal = new SpendResourcesGoal( resourcesToSpend );
        spendResourcesGoal.AddSubGoal( miningGoal );


        return spendResourcesGoal;
    }


    private List<Goal> DecomposeSpendResourcesGoal( SpendResourcesGoal goal )
    {
        List<Goal> subgoals = new List<Goal>();

        foreach ( ResourceToSpend resource in goal.Resources )
        {
            switch ( resource.Method )
            {
                case SpendMethod.Craft:
                    // For crafting, we need to find a recipe that uses this item and create a crafting goal
                    CraftTarget? target = _wearCraftTargetFinder.FindTarget( resource.Items );
                    if ( target != null )
                    {
                        subgoals.Add( new GearCraftingGoal( target ) );
                    }

                    break;

                case SpendMethod.Delete:
                case SpendMethod.Recycle:
                    // These methods can be handled directly by the client
                    subgoals.Add( goal );
                    break;
            }
        }

        return subgoals;
    }
}