using Artiact.Contracts.Models.Api;

namespace Artiact.Contracts.Models;

public abstract class Goal
{
    protected Goal( GoalType type )
    {
        Type = type;
        IsCompleted = false;
    }

    public GoalType Type { get; set; }
    public bool IsCompleted { get; set; }
    public List<Goal> SubGoals { get; } = new();
    public Goal? ParentGoal { get; private set; }

    public void AddSubGoal( Goal subGoal )
    {
        subGoal.ParentGoal = this;
        SubGoals.Add( subGoal );
    }
}

public class GearCraftingGoal : Goal
{
    public GearCraftingGoal( CraftTarget item ) : base( GoalType.Gearcrafting )
    {
        Item = item;
    }

    public CraftTarget Item { get; }
}

public class GatheringGoal : Goal
{
    public GatheringGoal( int targetLevel ) : base( GoalType.Gathering )
    {
        TargetLevel = targetLevel;
    }

    public int TargetLevel { get; }
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
    public LevelUpGoal( int? targetLevel = null ) : base( GoalType.LevelUp )
    {
        TargetLevel = targetLevel;
    }

    public int? TargetLevel { get; }
}

public class SpendResourcesGoal : Goal
{
    public SpendResourcesGoal( List<ResourceToSpend> resources ) : base( GoalType.SpendResources )
    {
        Resources = resources;
    }

    public List<ResourceToSpend> Resources { get; }
}

public class ResourceToSpend
{
    public ResourceToSpend( Item item,
                            SpendMethod method )
    {
        Item = item;
        Method = method;
    }

    public Item Item { get; set; }
    public SpendMethod Method { get; }
}