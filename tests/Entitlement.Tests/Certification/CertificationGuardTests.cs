using Entitlement.Domain.Certification;
using Entitlement.Domain.Models;

namespace Entitlement.Tests.Certification;

public class CertificationGuardTests
{
    [Fact]
    public void ValidateAttempt_BlankReason_IsRejected()
    {
        var result = CertificationGuard.ValidateAttempt("F1", CertificationDecisionType.Approved, "alice", "  ", alreadyCertified: false);
        Assert.False(result.Success);
        Assert.Contains("reason", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAttempt_BlankReviewer_IsRejected()
    {
        var result = CertificationGuard.ValidateAttempt("F1", CertificationDecisionType.Approved, "", "looks fine", alreadyCertified: false);
        Assert.False(result.Success);
        Assert.Contains("reviewer", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAttempt_AlreadyCertified_IsRejected()
    {
        var result = CertificationGuard.ValidateAttempt("F1", CertificationDecisionType.Approved, "alice", "ok", alreadyCertified: true);
        Assert.False(result.Success);
    }

    [Fact]
    public void ValidateAttempt_ValidReviewerAndReason_Succeeds()
    {
        var result = CertificationGuard.ValidateAttempt("F1", CertificationDecisionType.Revoked, "alice",
            "Confirmed this is a real conflict; grant revoked.", alreadyCertified: false);
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void CertificationService_SecondAttemptOnSameFinding_IsRejected()
    {
        var service = new CertificationService();
        var first = service.Certify("F1", CertificationDecisionType.Approved, "alice", "reviewed, no issue", DateTime.UtcNow);
        var second = service.Certify("F1", CertificationDecisionType.Revoked, "bob", "changed my mind", DateTime.UtcNow);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(CertificationDecisionType.Approved, service.Decisions["F1"].Decision);
    }

    [Fact]
    public void CertificationService_EmptyReason_NeverCreatesADecision()
    {
        var service = new CertificationService();
        var result = service.Certify("F1", CertificationDecisionType.Approved, "alice", "", DateTime.UtcNow);

        Assert.False(result.Success);
        Assert.Empty(service.Decisions);
    }
}
