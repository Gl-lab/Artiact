using System.Collections.Immutable;

namespace Artiact.Services.Strategy;

public sealed record ProgressTotal(long Levels, long KnownXp, int UnknownXpTransitions);
public sealed record RunPerformance(int DispatchedCandidateSwitches, int VerifiedFactCount, int CompletedWaitCount,
    double ObservedProcessingSeconds, double CompletedWaitSeconds,
    ImmutableDictionary<string, ProgressTotal> Progress, ImmutableDictionary<string, long> ConsumedInputs)
{
    public static RunPerformance From(RunCheckpoint checkpoint)
    {
        var facts = checkpoint.Journal.Where(x => x.Status == "Verified" && x.Facts is not null).Select(x => x.Facts!).ToArray();
        var progress = facts.SelectMany(x => x.Skills).GroupBy(x => x.Key, StringComparer.Ordinal).ToImmutableDictionary(x => x.Key,
            x => new ProgressTotal(x.Sum(v => (long)v.Value.Levels), x.Sum(v => v.Value.Xp ?? 0), x.Count(v => v.Value.Xp is null)), StringComparer.Ordinal);
        var inputs = facts.SelectMany(x => x.Inputs).GroupBy(x => x.Key, StringComparer.Ordinal).ToImmutableDictionary(x => x.Key, x => x.Sum(v => v.Value), StringComparer.Ordinal);
        var candidates = checkpoint.Journal.Where(x => x.Candidate is not null).Select(x => x.Candidate).ToArray();
        return new(candidates.Zip(candidates.Skip(1)).Count(x => x.First != x.Second), facts.Length, facts.Count(x => x.WaitSeconds.HasValue),
            facts.Sum(x => x.ObservationSeconds + x.DispatchSeconds), facts.Sum(x => x.WaitSeconds ?? 0), progress, inputs);
    }
}
