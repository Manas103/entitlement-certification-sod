using Entitlement.Domain.Detection;
using Entitlement.Domain.Engine;
using Entitlement.Domain.Log;
using Entitlement.Domain.Models;
using Entitlement.Domain.Rules;
using Entitlement.Domain.Synthetic;

namespace Entitlement.Tests.Detection;

/// <summary>
/// Agreement between the fast/indexed detector and the deliberately naive
/// brute-force oracle, first on small hand-crafted scenarios (so a failure
/// points at a specific, readable case) and then over the full 4,000-grant
/// synthetic population (so the agreement is also checked at the scale the
/// README's measured numbers were taken at).
/// </summary>
public class ReferenceOracleAgreementTests
{
    private static IReadOnlyList<string> Ids(IReadOnlyList<Finding> findings) =>
        findings.Select(f => f.FindingId).OrderBy(x => x, StringComparer.Ordinal).ToList();

    [Fact]
    public void Oracle_AgreesWithFastDetector_OnNamedTradeInstructionConflict()
    {
        var log = new AppendOnlyGrantLog();
        foreach (var role in RoleCatalogDefinitions.BusinessRoles)
            log.Append(GrantEventType.RoleDefined, DateTime.UtcNow, roleId: role.RoleId, rolePermissions: role.Permissions);
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeMaker",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeApprover",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G2");

        var snapshot = EntitlementSnapshotBuilder.Build(log);
        Assert.Equal(Ids(FastConflictDetector.Detect(snapshot)), Ids(ReferenceOracleDetector.Detect(snapshot)));
    }

    [Fact]
    public void Oracle_AgreesWithFastDetector_OnMixedCleanAndOrphanScenario()
    {
        var log = new AppendOnlyGrantLog();
        foreach (var role in RoleCatalogDefinitions.BusinessRoles)
            log.Append(GrantEventType.RoleDefined, DateTime.UtcNow, roleId: role.RoleId, rolePermissions: role.Permissions);

        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U2");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "OperationsAnalyst",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U2", roleId: "ClientServicingRep",
            businessUnit: BusinessUnit.ClientServicing, instrumentClass: InstrumentClass.MoneyMarket, grantId: "G2");
        log.Append(GrantEventType.UserTerminated, DateTime.UtcNow, userId: "U2");

        var snapshot = EntitlementSnapshotBuilder.Build(log);
        Assert.Equal(Ids(FastConflictDetector.Detect(snapshot)), Ids(ReferenceOracleDetector.Detect(snapshot)));
    }

    [Fact]
    public void Oracle_AgreesWithFastDetector_OverFullSyntheticPopulation()
    {
        var population = SyntheticPopulationGenerator.Generate(seed: 42, totalGrantTarget: 4000, noiseUserCount: 1300);
        var snapshot = EntitlementSnapshotBuilder.Build(population.Log);

        var fast = Ids(FastConflictDetector.Detect(snapshot));
        var oracle = Ids(ReferenceOracleDetector.Detect(snapshot));

        Assert.Equal(oracle, fast);
        Assert.Equal(28, fast.Count);
    }
}
