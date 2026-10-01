namespace Entitlement.Domain.Models;

/// <summary>
/// A reviewer's recorded decision on one flagged finding during a quarterly
/// access-review campaign. Constructed only by
/// <see cref="Certification.CertificationService"/>, which enforces
/// <see cref="Certification.CertificationGuard"/> before one of these can exist.
/// </summary>
public sealed record CertificationDecision
{
    public required string FindingId { get; init; }
    public required CertificationDecisionType Decision { get; init; }
    public required string Reviewer { get; init; }
    public required string Reason { get; init; }
    public required DateTime DecidedAtUtc { get; init; }
}
