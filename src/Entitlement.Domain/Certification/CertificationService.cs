using Entitlement.Domain.Models;

namespace Entitlement.Domain.Certification;

/// <summary>
/// The only place allowed to construct a <see cref="CertificationDecision"/>.
/// Tracks which findings have already been certified in this campaign so a
/// second attempt at the same finding is rejected by
/// <see cref="CertificationGuard"/> rather than silently overwritten.
/// </summary>
public sealed class CertificationService
{
    private readonly Dictionary<string, CertificationDecision> _decisions = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, CertificationDecision> Decisions => _decisions;

    public CertificationAttemptResult Certify(
        string findingId,
        CertificationDecisionType decision,
        string reviewer,
        string reason,
        DateTime decidedAtUtc)
    {
        var result = CertificationGuard.ValidateAttempt(
            findingId, decision, reviewer, reason, _decisions.ContainsKey(findingId));

        if (!result.Success)
        {
            return result;
        }

        _decisions[findingId] = new CertificationDecision
        {
            FindingId = findingId,
            Decision = decision,
            Reviewer = reviewer,
            Reason = reason,
            DecidedAtUtc = decidedAtUtc
        };

        return result;
    }
}
