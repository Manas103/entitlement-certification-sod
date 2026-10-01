using Entitlement.Domain.Detection;
using Entitlement.Domain.Engine;
using Entitlement.Domain.Log;
using Entitlement.Domain.Models;
using Entitlement.Domain.Rules;

namespace Entitlement.Tests.Detection;

public class FastConflictDetectorTests
{
    private static AppendOnlyGrantLog BaseLog()
    {
        var log = new AppendOnlyGrantLog();
        foreach (var role in RoleCatalogDefinitions.BusinessRoles)
        {
            log.Append(GrantEventType.RoleDefined, DateTime.UtcNow, roleId: role.RoleId, rolePermissions: role.Permissions);
        }
        return log;
    }

    [Fact]
    public void UserWithCreateAndApproveTradeInSameScope_IsFlaggedAsSod01()
    {
        var log = BaseLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeMaker",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeApprover",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G2");

        var findings = FastConflictDetector.Detect(EntitlementSnapshotBuilder.Build(log));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingType.SegregationOfDutiesConflict, finding.Type);
        Assert.Equal("SOD-01", finding.SodRuleId);
        Assert.Equal("U1", finding.UserId);
        Assert.Contains("G1", finding.GrantIds);
        Assert.Contains("G2", finding.GrantIds);
    }

    [Fact]
    public void UserWithCreateAndApproveTradeInDifferentScopes_IsNotFlagged()
    {
        var log = BaseLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeMaker",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeApprover",
            businessUnit: BusinessUnit.FixedIncome, instrumentClass: InstrumentClass.Bond, grantId: "G2");

        var findings = FastConflictDetector.Detect(EntitlementSnapshotBuilder.Build(log));

        Assert.Empty(findings);
    }

    [Fact]
    public void TwoUsersEachHoldingOneSide_IsNotFlagged()
    {
        var log = BaseLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U2");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "TradeMaker",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U2", roleId: "TradeApprover",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G2");

        var findings = FastConflictDetector.Detect(EntitlementSnapshotBuilder.Build(log));

        Assert.Empty(findings);
    }

    [Fact]
    public void WireAndSecurityMasterMakerCheckerConflicts_AreAlsoCaught()
    {
        var log = BaseLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "WireMaker",
            businessUnit: BusinessUnit.ForeignExchange, instrumentClass: InstrumentClass.Fx, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "WireApprover",
            businessUnit: BusinessUnit.ForeignExchange, instrumentClass: InstrumentClass.Fx, grantId: "G2");

        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U2");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U2", roleId: "SecurityMasterAnalyst",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G3");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U2", roleId: "SecurityMasterApprover",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G4");

        var findings = FastConflictDetector.Detect(EntitlementSnapshotBuilder.Build(log));

        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.SodRuleId == "SOD-02" && f.UserId == "U1");
        Assert.Contains(findings, f => f.SodRuleId == "SOD-03" && f.UserId == "U2");
    }

    [Fact]
    public void GrantForTerminatedUser_IsFlaggedAsOrphanedTerminatedUser()
    {
        var log = BaseLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "ClientServicingRep",
            businessUnit: BusinessUnit.ClientServicing, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.UserTerminated, DateTime.UtcNow, userId: "U1");

        var findings = FastConflictDetector.Detect(EntitlementSnapshotBuilder.Build(log));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingType.OrphanedGrantTerminatedUser, finding.Type);
        Assert.Equal("G1", finding.GrantIds[0]);
    }

    [Fact]
    public void GrantForDeletedRole_IsFlaggedAsOrphanedDeletedRole()
    {
        var log = BaseLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "ClientServicingRep",
            businessUnit: BusinessUnit.ClientServicing, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.RoleDeleted, DateTime.UtcNow, roleId: "ClientServicingRep");

        var findings = FastConflictDetector.Detect(EntitlementSnapshotBuilder.Build(log));

        var finding = Assert.Single(findings);
        Assert.Equal(FindingType.OrphanedGrantDeletedRole, finding.Type);
        Assert.Equal("G1", finding.GrantIds[0]);
    }

    [Fact]
    public void CleanPopulation_RaisesNoFindings()
    {
        var log = BaseLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "OperationsAnalyst",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "ComplianceReviewer",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G2");

        var findings = FastConflictDetector.Detect(EntitlementSnapshotBuilder.Build(log));

        Assert.Empty(findings);
    }
}
