using Entitlement.Domain.Detection;
using Entitlement.Domain.Engine;
using Entitlement.Domain.Log;
using Entitlement.Domain.Models;

namespace Entitlement.Domain.Reporting;

/// <summary>
/// One quarterly access-review campaign: a named quarter, the entitlement
/// snapshot it reviews, and the findings the fast detector raised against that
/// snapshot. Building a campaign never mutates the log; it is a read-only view
/// over a replay of it at a point in time.
/// </summary>
public sealed class AccessReviewCampaign
{
    public required string QuarterLabel { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required EntitlementSnapshot Snapshot { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }

    public static AccessReviewCampaign Run(AppendOnlyGrantLog log, string quarterLabel, DateTime generatedAtUtc)
    {
        var snapshot = EntitlementSnapshotBuilder.Build(log);
        var findings = FastConflictDetector.Detect(snapshot);
        return new AccessReviewCampaign
        {
            QuarterLabel = quarterLabel,
            GeneratedAtUtc = generatedAtUtc,
            Snapshot = snapshot,
            Findings = findings
        };
    }
}
