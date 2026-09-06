using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Services;
using Microsoft.Extensions.Options;

namespace Artiact.Tests.Services;

public class GoalServiceTests
{
    public static Character Snapshot( int level = 19, int used = 10 ) => new()
    {
        MiningLevel = level, InventoryMaxItems = 20,
        Inventory = new() { new() { Code = "ore", Quantity = used } }
    };

    public static IEnumerable<object?[]> Cases()
    {
        yield return new object?[] { 0, null, GoalDecisionReason.InvalidGoalPolicy, null, null };
        yield return new object?[] { -1, Snapshot(-1), GoalDecisionReason.InvalidGoalPolicy, null, null };
        yield return new object?[] { 20, null, GoalDecisionReason.InvalidCharacterSnapshot, null, null };
        yield return new object?[] { 20, Snapshot(-1), GoalDecisionReason.InvalidCharacterSnapshot, -1, null };
        yield return new object?[] { 20, Snapshot(), GoalDecisionReason.MiningBelowTarget, 19, 10 };
        yield return new object?[] { 20, Snapshot(19,11), GoalDecisionReason.InventoryPressure, 19, 9 };
        foreach (int level in new[] {20,21})
        foreach (Character c in Malformed().Append(Snapshot()))
        {
            c.MiningLevel = level;
            yield return new object?[] { 20, c, GoalDecisionReason.MiningTargetReached, level, null };
        }
        foreach (Character c in Malformed())
            yield return new object?[] { 20, c, GoalDecisionReason.InvalidInventorySnapshot, 19, null };
        foreach (string? code in new string?[] {null,""," "})
        {
            Character c = Snapshot(19,0); c.Inventory[0].Code = code!;
            yield return new object?[] {20,c,GoalDecisionReason.MiningBelowTarget,19,20};
        }
    }

    public static IEnumerable<Character> Malformed()
    {
        Character c = Snapshot(); c.Inventory = null!; yield return c;
        c = Snapshot(); c.Inventory.Add(null!); yield return c;
        c = Snapshot(); c.InventoryMaxItems = -1; yield return c;
        yield return Snapshot(19,-1);
        yield return Snapshot(19,21);
        c = Snapshot(19,int.MaxValue); c.InventoryMaxItems = int.MaxValue;
        c.Inventory.Add(new() {Code="ore",Quantity=1}); yield return c;
        foreach (string? code in new string?[] {null,""," "})
        { c = Snapshot(); c.Inventory[0].Code = code!; yield return c; }
    }

    [Fact]
    public void Evaluate_DoesNotMutateOrRetainSnapshotAndUsesConfiguredTarget()
    {
        Character snapshot=Snapshot();
        GoalService service=new(Options.Create(new GoalSelectionSettings{MiningTargetLevel=27}));
        GoalDecision original=service.Evaluate(snapshot);
        Assert.Equal(19,snapshot.MiningLevel);
        Assert.Equal(10,Assert.Single(snapshot.Inventory).Quantity);
        Assert.Equal("ore",snapshot.Inventory[0].Code);
        snapshot.MiningLevel=27;
        snapshot.Inventory.Clear();
        Assert.Equal(19,original.CurrentMiningLevel);
        Assert.Equal(10,original.InventoryUsed);
        Assert.Equal(27,original.MiningTargetLevel);
        Assert.Equal(GoalDecisionStatus.Completed,service.Evaluate(snapshot).Status);
        Assert.Equal(original,service.Evaluate(Snapshot()));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Evaluate_UsesPrecedenceAndExactFacts(int target, Character? character,
        GoalDecisionReason reason, int? current, int? free)
    {
        GoalService service = new(Options.Create(new GoalSelectionSettings {MiningTargetLevel=target}));
        GoalDecision decision = service.Evaluate(character);
        Assert.Equal(reason,decision.Reason);
        Assert.Equal(current,decision.CurrentMiningLevel);
        Assert.Equal(target,decision.MiningTargetLevel);
        Assert.Equal(10,decision.RequiredFreeInventory);
        Assert.Equal(free,decision.InventoryFree);
        Assert.Equal(free.HasValue ? 20 : (int?)null,decision.InventoryCapacity);
        Assert.Equal(free.HasValue ? 20-free : null,decision.InventoryUsed);
        Assert.Equal(reason == GoalDecisionReason.MiningBelowTarget ? GoalType.Gathering : (GoalType?)null,decision.SelectedGoalType);
        Assert.Equal(reason == GoalDecisionReason.MiningBelowTarget ? GoalDecisionStatus.Selected :
            reason == GoalDecisionReason.MiningTargetReached ? GoalDecisionStatus.Completed : GoalDecisionStatus.Blocked,decision.Status);
        Assert.Equal(decision,service.Evaluate(character));
    }
}

public class GoalDecisionFactoryTests
{
    [Theory]
    [InlineData(GoalDecisionStatus.Selected,GoalDecisionReason.MiningBelowTarget,19,20,10,10,GoalType.Gathering,"mining_below_target")]
    [InlineData(GoalDecisionStatus.Completed,GoalDecisionReason.MiningTargetReached,20,null,null,null,null,"mining_target_reached")]
    [InlineData(GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidGoalPolicy,null,null,null,null,null,"invalid_goal_policy")]
    [InlineData(GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidCharacterSnapshot,null,null,null,null,null,"invalid_character_snapshot")]
    [InlineData(GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidCharacterSnapshot,-1,null,null,null,null,"invalid_character_snapshot")]
    [InlineData(GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidInventorySnapshot,19,null,null,null,null,"invalid_inventory_snapshot")]
    [InlineData(GoalDecisionStatus.Blocked,GoalDecisionReason.InventoryPressure,19,20,11,9,null,"inventory_pressure")]
    public void Create_ValidShapes(GoalDecisionStatus status,GoalDecisionReason reason,int? current,int? capacity,int? used,int? free,GoalType? type,string code)
    {
        GoalDecision d = GoalDecision.Create(status,reason,reason == GoalDecisionReason.InvalidGoalPolicy ? 0 : 20,current,capacity,used,free,type);
        Assert.Equal(status,d.Status); Assert.Equal(reason,d.Reason); Assert.Equal(code,d.ReasonCode);
        Assert.Equal(current,d.CurrentMiningLevel); Assert.Equal(capacity,d.InventoryCapacity);
        Assert.Equal(used,d.InventoryUsed); Assert.Equal(free,d.InventoryFree); Assert.Equal(type,d.SelectedGoalType);
    }

    public static IEnumerable<object?[]> InvalidShapes()
    {
        object?[] valid = {GoalDecisionStatus.Selected,GoalDecisionReason.MiningBelowTarget,20,19,20,10,10,GoalType.Gathering};
        foreach (var (index, values) in new (int,object?[])[] {
            (0,new object?[]{(GoalDecisionStatus)99,GoalDecisionStatus.Blocked,GoalDecisionStatus.Completed}),
            (1,new object?[]{(GoalDecisionReason)99,GoalDecisionReason.InventoryPressure,GoalDecisionReason.MiningTargetReached}),
            (2,new object?[]{0,-1,19}), (3,new object?[]{null,-1,20}),
            (4,new object?[]{null,-1,21}), (5,new object?[]{null,-1,11,int.MaxValue}),
            (6,new object?[]{null,-1,9,11}), (7,new object?[]{null,GoalType.LevelUp,(GoalType)99}) })
            foreach(object? value in values) { var row = (object?[])valid.Clone(); row[index]=value; yield return row; }
        yield return new object?[]{GoalDecisionStatus.Completed,GoalDecisionReason.MiningTargetReached,20,19,null,null,null,null};
        yield return new object?[]{GoalDecisionStatus.Completed,GoalDecisionReason.MiningTargetReached,20,20,20,10,10,null};
        yield return new object?[]{GoalDecisionStatus.Completed,GoalDecisionReason.MiningTargetReached,20,20,null,null,null,GoalType.Gathering};
        yield return new object?[]{GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidGoalPolicy,20,null,null,null,null,null};
        yield return new object?[]{GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidGoalPolicy,0,19,null,null,null,null};
        yield return new object?[]{GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidCharacterSnapshot,20,0,null,null,null,null};
        yield return new object?[]{GoalDecisionStatus.Blocked,GoalDecisionReason.InvalidInventorySnapshot,20,null,null,null,null,null};
        yield return new object?[]{GoalDecisionStatus.Blocked,GoalDecisionReason.InventoryPressure,20,19,20,10,10,null};
    }
    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public void Create_RejectsInvalidShapes(GoalDecisionStatus status,GoalDecisionReason reason,int target,int? current,int? capacity,int? used,int? free,GoalType? type) =>
        Assert.Throws<ArgumentException>(()=>GoalDecision.Create(status,reason,target,current,capacity,used,free,type));

    [Fact]
    public void Decision_ExposesOnlyImmutableFacts()
    {
        Assert.Empty(typeof(GoalDecision).GetConstructors());
        Assert.All(typeof(GoalDecision).GetProperties(),p=>Assert.Null(p.SetMethod));
        Assert.DoesNotContain(typeof(GoalDecision).GetProperties(),p=>typeof(Goal).IsAssignableFrom(p.PropertyType));
        Assert.Equal(3,Enum.GetValues<GoalDecisionStatus>().Length);
        Assert.Equal(12,Enum.GetValues<GoalDecisionReason>().Length);
    }
}
