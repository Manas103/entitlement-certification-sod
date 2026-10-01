using Entitlement.Domain.Models;

namespace Entitlement.Domain.Certification;

public sealed record CertificationAttemptResult(bool Success, string? Error);

/// <summary>
/// The single shared validation rule for certifying a finding during a
/// quarterly access-review campaign: a decision always needs a non-blank
/// reviewer and a non-blank reason, and a finding that has already been
/// certified cannot be re-certified with a different decision. This mirrors
/// the disposition guard in <c>transaction-monitoring-alert-triage</c>: the
/// rule is enforced once, here, and called by both the in-process tooling and
/// the API endpoint, with no separate "the caller already checked" trust
/// boundary.
/// </summary>
public static class CertificationGuard
{
    public static CertificationAttemptResult ValidateAttempt(
        string findingId,
        CertificationDecisionType decision,
        string reviewer,
        string reason,
        bool alreadyCertified)
    {
        if (alreadyCertified)
        {
            return new CertificationAttemptResult(false,
                $"Finding {findingId} has already been certified and cannot be re-certified.");
        }

        if (string.IsNullOrWhiteSpace(reviewer))
        {
            return new CertificationAttemptResult(false,
                "A reviewer identifier is required and cannot be blank.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new CertificationAttemptResult(false,
                "A certification reason is required and cannot be blank; this finding was not certified.");
        }

        return new CertificationAttemptResult(true, null);
    }
}
