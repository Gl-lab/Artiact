namespace Artiact.Services;

public interface ICraftTargetEvaluator
{
    CraftTarget SelectBestTarget(List<CraftTarget> targets);
}