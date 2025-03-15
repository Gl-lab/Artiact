using Artiact.Models.Api;
using Artiact.Services;

namespace Artiact.Models;

public abstract class Goal
{
    public GoalType Type { get; set; }
    public bool IsCompleted { get; set; }
    public List<Goal> SubGoals { get; } = new();
    public Goal? ParentGoal { get; private set; }

    protected Goal( GoalType type )
    {
        Type = type;
        IsCompleted = false;
    }

    public void AddSubGoal( Goal subGoal )
    {
        subGoal.ParentGoal = this;
        SubGoals.Add( subGoal );
    }
}

public class GearCraftingGoal : Goal
{
    public CraftTarget Item { get; }

    public GearCraftingGoal( CraftTarget item ) : base( GoalType.Gearcrafting )
    {
        Item = item;
    }
}

public class MiningGoal : Goal
{
    public int TargetLevel { get; }

    public MiningGoal( int targetLevel ) : base( GoalType.Mining )
    {
        TargetLevel = targetLevel;
    }
}

// public class MovementGoal : Goal
// {
//     public MapPoint Target { get; }
//
//     public MovementGoal(MapPoint target) : base(GoalType.Movement)
//     {
//         Target = target;
//     }
// }

public class LevelUpGoal : Goal
{
    public int? TargetLevel { get; }

    public LevelUpGoal( int? targetLevel = null ) : base( GoalType.LevelUp )
    {
        TargetLevel = targetLevel;
    }
}

public class SpendResourcesGoal : Goal
{
    public List<ResourceToSpend> Resources { get; }

    public SpendResourcesGoal( List<ResourceToSpend> resources ) : base( GoalType.SpendResources )
    {
        Resources = resources;
    }
}

public class ResourceToSpend
{
    public List<Item> Items { get; set; }
    public SpendMethod Method { get; }

    public ResourceToSpend( List<Item> items, SpendMethod method )
    {
        Items = items;

        Method = method;
    }
}

public enum SpendMethod
{
    Craft,
    Delete,
    Recycle
}