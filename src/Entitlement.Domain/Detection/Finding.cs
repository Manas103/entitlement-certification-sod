using Entitlement.Domain.Models;

namespace Entitlement.Domain.Detection;

/// <summary>
/// One detected defect: a segregation-of-duties conflict or an orphaned grant.
/// <see cref="FindingId"/> is deterministic (built from the sorted grant ids and
/// rule involved), not a random GUID, so the same defect produces the same id on
/// every run, which the evidence pack's byte-identity claim depends on.
/// </summary>
public sealed record Finding
{
    public required string FindingId { get; init; }
    public required FindingType Type { get; init; }
    public required string UserId { get; init; }
    public string? SodRuleId { get; init; }
    public string? SodRuleName { get; init; }
    public BusinessUnit? BusinessUnit { get; init; }
    public InstrumentClass? InstrumentClass { get; init; }
    public required IReadOnlyList<string> GrantIds { get; init; }
    public required string Description { get; init; }
}
