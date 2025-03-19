using Artiact.Contracts.Models.Api;

namespace Artiact.Contracts.Models;

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

public class GatheringGoal : Goal
{
    public int TargetLevel { get; }

    public GatheringGoal( int targetLevel ) : base( GoalType.Gathering )
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
    public Item Item { get; set; }
    public SpendMethod Method { get; }

    public ResourceToSpend( Item item,
                            SpendMethod method )
    {
        Item = item;
        Method = method;
    }
}