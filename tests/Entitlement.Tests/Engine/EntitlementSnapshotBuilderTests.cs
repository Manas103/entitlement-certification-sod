using Entitlement.Domain.Engine;
using Entitlement.Domain.Log;
using Entitlement.Domain.Models;

namespace Entitlement.Tests.Engine;

public class EntitlementSnapshotBuilderTests
{
    private static AppendOnlyGrantLog NewLogWithUserAndRole(string userId, string roleId, Permission[] perms)
    {
        var log = new AppendOnlyGrantLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: userId);
        log.Append(GrantEventType.RoleDefined, DateTime.UtcNow, roleId: roleId, rolePermissions: perms);
        return log;
    }

    [Fact]
    public void Build_GrantThenNothingElse_GrantIsActive()
    {
        var log = NewLogWithUserAndRole("U1", "R1", new[] { Permission.ViewPosition });
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "R1",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");

        var snapshot = EntitlementSnapshotBuilder.Build(log);

        Assert.Contains("U1", snapshot.ActiveUserIds);
        Assert.True(snapshot.RoleCatalog.ContainsKey("R1"));
        Assert.Single(snapshot.ActiveGrants);
        Assert.Equal("G1", snapshot.ActiveGrants[0].GrantId);
    }

    [Fact]
    public void Build_GrantThenRevoke_GrantIsNotActive()
    {
        var log = NewLogWithUserAndRole("U1", "R1", new[] { Permission.ViewPosition });
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "R1",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.Revoke, DateTime.UtcNow, grantId: "G1");

        var snapshot = EntitlementSnapshotBuilder.Build(log);

        Assert.Empty(snapshot.ActiveGrants);
    }

    [Fact]
    public void Build_UserTerminatedWithoutRevoke_UserInactiveButGrantStillActive()
    {
        var log = NewLogWithUserAndRole("U1", "R1", new[] { Permission.ViewPosition });
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "R1",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.UserTerminated, DateTime.UtcNow, userId: "U1");

        var snapshot = EntitlementSnapshotBuilder.Build(log);

        Assert.DoesNotContain("U1", snapshot.ActiveUserIds);
        Assert.Contains("U1", snapshot.TerminatedUserIds);
        Assert.Single(snapshot.ActiveGrants); // orphaned: still active despite termination
    }

    [Fact]
    public void Build_RoleDeletedWithoutRevoke_RoleGoneButGrantStillActive()
    {
        var log = NewLogWithUserAndRole("U1", "R1", new[] { Permission.ViewPosition });
        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "R1",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");
        log.Append(GrantEventType.RoleDeleted, DateTime.UtcNow, roleId: "R1");

        var snapshot = EntitlementSnapshotBuilder.Build(log);

        Assert.False(snapshot.RoleCatalog.ContainsKey("R1"));
        Assert.Contains("R1", snapshot.DeletedRoleIds);
        Assert.Single(snapshot.ActiveGrants); // orphaned: still active despite role deletion
    }

    [Fact]
    public void Build_ReOnboardAfterTermination_UserActiveAgain()
    {
        var log = new AppendOnlyGrantLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.UserTerminated, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");

        var snapshot = EntitlementSnapshotBuilder.Build(log);

        Assert.Contains("U1", snapshot.ActiveUserIds);
        Assert.DoesNotContain("U1", snapshot.TerminatedUserIds);
    }
}
