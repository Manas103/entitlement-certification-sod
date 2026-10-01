using Entitlement.Domain.Models;

namespace Entitlement.Api.Contracts;

public sealed record CampaignSummaryResponse(
    string QuarterLabel,
    int ActiveUsers,
    int ActiveRoles,
    int ActiveGrants,
    int FindingCount,
    IReadOnlyList<FindingResponse> Findings);

public sealed record FindingResponse(
    string FindingId,
    string Type,
    string UserId,
    string? SodRuleId,
    string? BusinessUnit,
    string? InstrumentClass,
    IReadOnlyList<string> GrantIds,
    string Description);

public sealed record CertifyRequest(string FindingId, CertificationDecisionType Decision, string Reviewer, string Reason);

public sealed record CertifyResponse(bool Success, string? Error);

public sealed record PersistResult(int EntriesPersisted, string Provider);
