using Entitlement.Domain.Detection;
using Entitlement.Domain.Engine;
using Entitlement.Domain.Synthetic;

namespace Entitlement.Tests.Synthetic;

public class SyntheticPopulationGeneratorTests
{
    [Fact]
    public void Generate_ProducesExactlyTheTargetGrantCount()
    {
        var population = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);
        Assert.Equal(4000, population.TotalGrantCount);

        var snapshot = EntitlementSnapshotBuilder.Build(population.Log);
        Assert.Equal(4000, snapshot.ActiveGrants.Count);
    }

    [Fact]
    public void Generate_ProducesExactlyTwentyEightSeededDefects()
    {
        var population = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);
        Assert.Equal(28, population.SeededFindingIds.Count);
        Assert.Equal(28, population.Plan.TotalDefects);
    }

    [Fact]
    public void Generate_FastDetectorCatchesAllSeededDefectsWithZeroFalsePositives()
    {
        var population = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);
        var snapshot = EntitlementSnapshotBuilder.Build(population.Log);
        var findings = FastConflictDetector.Detect(snapshot);

        var findingIds = findings.Select(f => f.FindingId).ToHashSet(StringComparer.Ordinal);
        var seededIds = population.SeededFindingIds.ToHashSet(StringComparer.Ordinal);

        Assert.Equal(seededIds, findingIds); // exact set equality: every seeded id caught, no extras
    }

    [Fact]
    public void Generate_IsDeterministic_SameSeedProducesSameLogLength()
    {
        var a = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);
        var b = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);

        Assert.Equal(a.Log.Entries.Count, b.Log.Entries.Count);
        Assert.Equal(a.TotalGrantCount, b.TotalGrantCount);
        Assert.Equal(a.SeededFindingIds, b.SeededFindingIds);
    }
}
