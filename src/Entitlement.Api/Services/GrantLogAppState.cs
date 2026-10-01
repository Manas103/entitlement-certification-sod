using Entitlement.Domain.Certification;
using Entitlement.Domain.Log;
using Entitlement.Domain.Synthetic;

namespace Entitlement.Api.Services;

/// <summary>
/// The API's in-process entitlement world: one append-only grant log, seeded
/// once at startup from the same deterministic synthetic population generator
/// the Tooling measurement run uses, plus one certification service tracking
/// reviewer decisions for the process lifetime. This is a thin demo surface
/// proving the engine and the certification guard work identically over real
/// HTTP, not the measurement path; the measured numbers in the README come
/// from <c>Entitlement.Tooling</c> running the same Domain code in process.
/// </summary>
public sealed class GrantLogAppState
{
    public AppendOnlyGrantLog Log { get; }
    public CertificationService Certifications { get; } = new();
    public SeedPlan Plan { get; }

    public GrantLogAppState()
    {
        var population = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);
        Log = population.Log;
        Plan = population.Plan;
    }
}
