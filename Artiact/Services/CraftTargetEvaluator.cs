using Artiact.Contracts.Models;

namespace Artiact.Services;

public class CraftTargetEvaluator : ICraftTargetEvaluator
{
    public CraftTarget SelectBestTarget( List<CraftTarget> targets )
    {
        return targets.OrderByDescending( t => t.FinalItem.Level ).First();
    }
}