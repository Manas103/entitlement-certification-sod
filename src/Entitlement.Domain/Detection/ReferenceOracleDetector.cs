using Entitlement.Domain.Models;
using Entitlement.Domain.Rules;

namespace Entitlement.Domain.Detection;

/// <summary>
/// A deliberately slow, deliberately naive re-implementation of the same
/// detection logic as <see cref="FastConflictDetector"/>, written with no shared
/// helper code and no indexing, so the two cannot share a bug. For every active
/// user and every (business unit, instrument class) pair it does a fresh linear
/// scan of the entire active-grant list, and every user/role existence check is
/// a brute-force <see cref="Enumerable.Any{T}(IEnumerable{T}, Func{T,bool})"/>
/// scan rather than a dictionary or set lookup. This is O(users * scopes *
/// grants), not O(grants); it exists purely to be diffed exactly against the
/// fast path (see <c>ReferenceOracleAgreementTests</c> and the tooling
/// measurement run), never to run at production scale.
/// </summary>
public static class ReferenceOracleDetector
{
    public static IReadOnlyList<Finding> Detect(EntitlementSnapshot snapshot)
    {
        var allGrants = snapshot.ActiveGrants.ToList();
        var findings = new List<Finding>();

        // --- Segregation-of-duties conflicts, brute force ---
        var distinctUsers = allGrants.Select(g => g.UserId).Distinct().ToList();
        foreach (var userId in distinctUsers)
        {
            var userIsActive = snapshot.ActiveUserIds.Any(u => u == userId);
            if (!userIsActive) continue;

            foreach (BusinessUnit bu in Enum.GetValues<BusinessUnit>())
            {
                foreach (InstrumentClass ic in Enum.GetValues<InstrumentClass>())
                {
                    var scopedGrantIds = new List<string>();
                    var heldPermissions = new List<Permission>();
                    var permissionSource = new Dictionary<Permission, List<string>>();

                    foreach (var grant in allGrants)
                    {
                        if (grant.UserId != userId || grant.BusinessUnit != bu || grant.InstrumentClass != ic)
                        {
                            continue;
                        }

                        RoleDefinition? matchedRole = null;
                        foreach (var kvp in snapshot.RoleCatalog)
                        {
                            if (kvp.Key == grant.RoleId)
                            {
                                matchedRole = kvp.Value;
                                break;
                            }
                        }
                        if (matchedRole is null)
                        {
                            continue;
                        }

                        scopedGrantIds.Add(grant.GrantId);
                        foreach (var perm in matchedRole.Permissions)
                        {
                            heldPermissions.Add(perm);
                            if (!permissionSource.TryGetValue(perm, out var srcList))
                            {
                                srcList = new List<string>();
                                permissionSource[perm] = srcList;
                            }
                            srcList.Add(grant.GrantId);
                        }
                    }

                    foreach (var rule in SodRuleCatalog.AllRules)
                    {
                        var hasMaker = heldPermissions.Any(p => p == rule.Maker);
                        var hasChecker = heldPermissions.Any(p => p == rule.Checker);
                        if (hasMaker && hasChecker)
                        {
                            var grantIds = permissionSource[rule.Maker]
                                .Concat(permissionSource[rule.Checker])
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(g => g, StringComparer.Ordinal)
                                .ToList();

                            findings.Add(new Finding
                            {
                                FindingId = $"SOD|{rule.RuleId}|{userId}|{bu}|{ic}",
                                Type = FindingType.SegregationOfDutiesConflict,
                                UserId = userId,
                                SodRuleId = rule.RuleId,
                                SodRuleName = rule.Name,
                                BusinessUnit = bu,
                                InstrumentClass = ic,
                                GrantIds = grantIds,
                                Description =
                                    $"User {userId} holds both {rule.Maker} and {rule.Checker} " +
                                    $"within {bu}/{ic} scope ({rule.Name})."
                            });
                        }
                    }
                }
            }
        }

        // --- Orphaned grants, brute force ---
        foreach (var grant in allGrants)
        {
            var userStillActive = snapshot.ActiveUserIds.Any(u => u == grant.UserId);
            if (!userStillActive)
            {
                findings.Add(new Finding
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
                });
            }

            var roleStillExists = false;
            foreach (var kvp in snapshot.RoleCatalog)
            {
                if (kvp.Key == grant.RoleId) { roleStillExists = true; break; }
            }
            if (!roleStillExists)
            {
                findings.Add(new Finding
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
                });
            }
        }

        return findings
            .OrderBy(f => f.FindingId, StringComparer.Ordinal)
            .ToList();
    }
}
