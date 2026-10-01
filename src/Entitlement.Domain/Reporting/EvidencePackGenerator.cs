using System.Text;
using Entitlement.Domain.Detection;
using Entitlement.Domain.Engine;
using Entitlement.Domain.Log;

namespace Entitlement.Domain.Reporting;

/// <summary>
/// Produces the reviewer evidence pack: the document a human reviewer signs off
/// a quarterly campaign with. This is deliberately a pure function of the
/// append-only grant log's contents and nothing else: no wall-clock timestamp,
/// no random identifier, no dictionary enumerated in insertion order without an
/// explicit sort. Two calls to <see cref="Generate"/> against the same log
/// produce byte-identical output, which is the property
/// <c>EvidencePackGeneratorTests</c> and the tooling measurement run both check
/// by hashing the output of two independent calls and comparing.
/// </summary>
public static class EvidencePackGenerator
{
    public static string Generate(AppendOnlyGrantLog log, string quarterLabel)
    {
        var snapshot = EntitlementSnapshotBuilder.Build(log);
        var findings = FastConflictDetector.Detect(snapshot);

        var sb = new StringBuilder();
        sb.Append("Entitlement Certification Service - Reviewer Evidence Pack\n");
        sb.Append($"Campaign quarter: {quarterLabel}\n");
        sb.Append($"Grant log entries replayed: {log.Entries.Count}\n");
        sb.Append($"Active users: {snapshot.ActiveUserIds.Count}\n");
        sb.Append($"Active roles: {snapshot.RoleCatalog.Count}\n");
        sb.Append($"Active user-role grants reviewed: {snapshot.ActiveGrants.Count}\n");
        sb.Append($"Findings: {findings.Count}\n");
        sb.Append("---\n");

        foreach (var f in findings)
        {
            sb.Append(f.FindingId).Append('\t')
              .Append(f.Type).Append('\t')
              .Append(f.UserId).Append('\t')
              .Append(f.SodRuleId ?? "-").Append('\t')
              .Append(f.BusinessUnit?.ToString() ?? "-").Append('\t')
              .Append(f.InstrumentClass?.ToString() ?? "-").Append('\t')
              .Append(string.Join(',', f.GrantIds)).Append('\t')
              .Append(f.Description)
              .Append('\n');
        }

        sb.Append("--- end of evidence pack ---\n");
        return sb.ToString();
    }
}
