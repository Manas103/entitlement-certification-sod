using Entitlement.Domain.Models;
using Entitlement.Domain.Rules;

namespace Entitlement.Domain.Detection;

/// <summary>
/// The indexed implementation: builds a single dictionary keyed by
/// (UserId, BusinessUnit, InstrumentClass) in one pass over the active grants,
/// then checks each of the three catalog rules against each scope group's
/// accumulated permission set. Orphan detection is a second, independent linear
/// pass. Overall cost is O(grants + roles) rather than the O(grants^2) the
/// reference oracle in this same namespace uses; <see cref="ReferenceOracleDetector"/>
/// exists specifically so this type's output can be diffed against something
/// that cannot share a bug with it.
/// </summary>
public static class FastConflictDetector
{
    public static IReadOnlyList<Finding> Detect(EntitlementSnapshot snapshot)
    {
        var findings = new List<Finding>();
        findings.AddRange(DetectSodConflicts(snapshot));
        findings.AddRange(DetectOrphanedGrants(snapshot));
        return findings
            .OrderBy(f => f.FindingId, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<Finding> DetectSodConflicts(EntitlementSnapshot snapshot)
    {
        var groups = new Dictionary<(string UserId, BusinessUnit Bu, InstrumentClass Ic), Dictionary<Permission, List<string>>>();

        foreach (var grant in snapshot.ActiveGrants)
        {
            // A grant whose role no longer validly exists contributes no
            // permissions to its scope group; it is handled entirely as an
            // orphan below, not as a source of SoD permissions.
            if (!snapshot.RoleCatalog.TryGetValue(grant.RoleId, out var role))
            {
                continue;
            }

            // A grant held by a terminated user is an orphan, not a live
            // entitlement; it cannot contribute to a live SoD conflict either.
            if (!snapshot.ActiveUserIds.Contains(grant.UserId))
            {
                continue;
            }

            var key = (grant.UserId, grant.BusinessUnit, grant.InstrumentClass);
            if (!groups.TryGetValue(key, out var permMap))
            {
                permMap = new Dictionary<Permission, List<string>>();
                groups[key] = permMap;
            }

            foreach (var perm in role.Permissions)
            {
                if (!permMap.TryGetValue(perm, out var list))
                {
                    list = new List<string>();
                    permMap[perm] = list;
                }
                list.Add(grant.GrantId);
            }
        }

        foreach (var (key, permMap) in groups)
        {
            foreach (var rule in SodRuleCatalog.AllRules)
            {
                if (permMap.TryGetValue(rule.Maker, out var makerGrants) &&
                    permMap.TryGetValue(rule.Checker, out var checkerGrants))
                {
                    var grantIds = makerGrants.Concat(checkerGrants)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(g => g, StringComparer.Ordinal)
                        .ToList();

                    yield return new Finding
                    {
                        FindingId = $"SOD|{rule.RuleId}|{key.UserId}|{key.Bu}|{key.Ic}",
                        Type = FindingType.SegregationOfDutiesConflict,
                        UserId = key.UserId,
                        SodRuleId = rule.RuleId,
                        SodRuleName = rule.Name,
                        BusinessUnit = key.Bu,
                        InstrumentClass = key.Ic,
                        GrantIds = grantIds,
                        Description =
                            $"User {key.UserId} holds both {rule.Maker} and {rule.Checker} " +
                            $"within {key.Bu}/{key.Ic} scope ({rule.Name})."
                    };
                }
            }
        }
    }

    private static IEnumerable<Finding> DetectOrphanedGrants(EntitlementSnapshot snapshot)
    {
        foreach (var grant in snapshot.ActiveGrants)
        {
            if (!snapshot.ActiveUserIds.Contains(grant.UserId))
            {
                yield return new Finding
                {
                    FindingId = $"ORPHAN-USER|{grant.GrantId}",
                    Type = FindingType.OrphanedGrantTerminatedUser,
                    UserId = grant.UserId,
                    BusinessUnit = grant.BusinessUnit,
                    InstrumentClass = grant.InstrumentClass,
                    GrantIds = new[] { grant.GrantId },
                    Description =
                        $"Grant {grant.GrantId} is active for user {grant.UserId}, " +
                        "who has a UserTerminated event with no matching Revoke."
                };
            }

            if (!snapshot.RoleCatalog.ContainsKey(grant.RoleId))
            {
                yield return new Finding
                {
                    FindingId = $"ORPHAN-ROLE|{grant.GrantId}",
                    Type = FindingType.OrphanedGrantDeletedRole,
                    UserId = grant.UserId,
                    BusinessUnit = grant.BusinessUnit,
                    InstrumentClass = grant.InstrumentClass,
                    GrantIds = new[] { grant.GrantId },
                    Description =
                        $"Grant {grant.GrantId} points at role {grant.RoleId}, " +
                        "which has a RoleDeleted event with no matching Revoke."
                };
            }
        }
    }
}
