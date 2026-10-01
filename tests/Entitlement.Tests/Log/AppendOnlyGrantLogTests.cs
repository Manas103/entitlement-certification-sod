using Entitlement.Domain.Log;
using Entitlement.Domain.Models;

namespace Entitlement.Tests.Log;

public class AppendOnlyGrantLogTests
{
    [Fact]
    public void Append_AssignsSequentialSequenceNumbers()
    {
        var log = new AppendOnlyGrantLog();
        var e1 = log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        var e2 = log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U2");
        var e3 = log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U3");

        Assert.Equal(1, e1.SequenceNumber);
        Assert.Equal(2, e2.SequenceNumber);
        Assert.Equal(3, e3.SequenceNumber);
        Assert.Equal(3, log.Entries.Count);
    }

    [Fact]
    public void Entries_ReflectsAppendOrder()
    {
        var log = new AppendOnlyGrantLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.UserTerminated, DateTime.UtcNow, userId: "U1");

        Assert.Equal(GrantEventType.UserOnboarded, log.Entries[0].EventType);
        Assert.Equal(GrantEventType.UserTerminated, log.Entries[1].EventType);
    }

    [Fact]
    public void PriorEntriesDigest_UnchangedAfterAppend_ProvesLogIsAppendOnly()
    {
        var log = new AppendOnlyGrantLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");
        log.Append(GrantEventType.RoleDefined, DateTime.UtcNow, roleId: "R1",
            rolePermissions: new[] { Permission.ViewPosition });

        var digestBefore = log.PriorEntriesDigest(2);

        log.Append(GrantEventType.Grant, DateTime.UtcNow, userId: "U1", roleId: "R1",
            businessUnit: BusinessUnit.Equities, instrumentClass: InstrumentClass.Equity, grantId: "G1");

        var digestAfter = log.PriorEntriesDigest(2);

        Assert.Equal(digestBefore, digestAfter);
    }

    [Fact]
    public void PriorEntriesDigest_DiffersWhenEntryContentWouldDiffer()
    {
        var logA = new AppendOnlyGrantLog();
        logA.Append(GrantEventType.UserOnboarded, new DateTime(2026, 1, 1), userId: "U1");

        var logB = new AppendOnlyGrantLog();
        logB.Append(GrantEventType.UserOnboarded, new DateTime(2026, 1, 1), userId: "U2");

        Assert.NotEqual(logA.PriorEntriesDigest(1), logB.PriorEntriesDigest(1));
    }

    [Fact]
    public void PriorEntriesDigest_RejectsOutOfRangeCount()
    {
        var log = new AppendOnlyGrantLog();
        log.Append(GrantEventType.UserOnboarded, DateTime.UtcNow, userId: "U1");

        Assert.Throws<ArgumentOutOfRangeException>(() => log.PriorEntriesDigest(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => log.PriorEntriesDigest(-1));
    }
}
