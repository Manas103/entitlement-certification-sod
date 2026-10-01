using Entitlement.Domain.Log;
using Entitlement.Domain.Models;
using Entitlement.Domain.Reporting;
using Entitlement.Domain.Rules;
using Entitlement.Domain.Synthetic;

namespace Entitlement.Tests.Reporting;

public class EvidencePackGeneratorTests
{
    [Fact]
    public void Generate_TwiceOverSameLog_IsByteIdentical()
    {
        var population = SyntheticPopulationGenerator.Generate(seed: 7, totalGrantTarget: 500, noiseUserCount: 150);

        var pack1 = EvidencePackGenerator.Generate(population.Log, "2026-Q3");
        var pack2 = EvidencePackGenerator.Generate(population.Log, "2026-Q3");

        Assert.Equal(pack1, pack2);
        Assert.Equal(DigestUtil.Sha256Hex(pack1), DigestUtil.Sha256Hex(pack2));
    }

    [Fact]
    public void Generate_AfterANewAppend_ProducesADifferentPack()
    {
        var log = new AppendOnlyGrantLog();
        log.Append(GrantEventType.RoleDefined, DateTime.UtcNow, roleId: "ClientServicingRep",
            rolePermissions: new[] { Permission.ViewClientData });
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "ClientServicingRep",
            businessUnit: BusinessUnit.ClientServicing, instrumentClass: InstrumentClass.Equity, grantId: "G1");

        var packBefore = EvidencePackGenerator.Generate(log, "2026-Q3");

        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U2");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U2", roleId: "ClientServicingRep",
            businessUnit: BusinessUnit.ClientServicing, instrumentClass: InstrumentClass.Equity, grantId: "G2");

        var packAfter = EvidencePackGenerator.Generate(log, "2026-Q3");

        Assert.NotEqual(packBefore, packAfter);
    }

    [Fact]
    public void Generate_ListsEveryFinding_AndReportsActiveGrantCount()
    {
        var log = new AppendOnlyGrantLog();
        foreach (var role in RoleCatalogDefinitions.BusinessRoles)
            log.Append(GrantEventType.RoleDefined, DateTime.UtcNow, roleId: role.RoleId, rolePermissions: role.Permissions);
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeMaker",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeApprover",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G2");

        var pack = EvidencePackGenerator.Generate(log, "2026-Q3");

        Assert.Contains("Campaign quarter: 2026-Q3", pack);
        Assert.Contains("Active user-role grants reviewed: 2", pack);
        Assert.Contains("Findings: 1", pack);
        Assert.Contains("SOD-01", pack);
    }
}
