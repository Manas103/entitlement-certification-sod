using Entitlement.Domain.Log;
using Entitlement.Domain.Models;
using Entitlement.Domain.Rules;

namespace Entitlement.Domain.Synthetic;

public sealed record SeedPlan(int Sod01Count, int Sod02Count, int Sod03Count, int OrphanUserCount, int OrphanRoleCount)
{
    public int TotalDefects => Sod01Count + Sod02Count + Sod03Count + OrphanUserCount + OrphanRoleCount;
}

public sealed record SyntheticPopulationResult
{
    public required AppendOnlyGrantLog Log { get; init; }
    public required int TotalGrantCount { get; init; }
    public required IReadOnlyList<string> SeededFindingIds { get; init; }
    public required SeedPlan Plan { get; init; }
}

/// <summary>
/// Builds the simulated investment-operations application's entitlement world:
/// a population of users and roles over the append-only grant log, with an
/// exact, named count of seeded segregation-of-duties conflicts and orphaned
/// grants, and a target total of active user-role grants. Everything is
/// deterministic (seeded <see cref="Random"/>, fixed synthetic timestamps), so
/// the same call produces the same log on every run.
///
/// The hard part is the noise: the background grants that are not seeded
/// defects must never accidentally create an unintended conflict or orphan of
/// their own, or the "0 false positives" claim would be measuring a generator
/// bug instead of a detector property. <see cref="WouldIntroduceUnintendedConflict"/>
/// is checked before every noise grant is appended, by simulating the merge
/// against that user's running per-scope permission set and rejecting (and
/// retrying with a different random choice) any candidate that would newly
/// satisfy an <see cref="SodRuleCatalog"/> rule outside a scope the plan
/// deliberately seeded.
/// </summary>
public static class SyntheticPopulationGenerator
{
    private static readonly DateTime BaseTime = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    public static SyntheticPopulationResult Generate(
        int seed = 42,
        int totalGrantTarget = 4000,
        int noiseUserCount = 1300,
        SeedPlan? plan = null)
    {
        plan ??= new SeedPlan(Sod01Count: 12, Sod02Count: 5, Sod03Count: 3, OrphanUserCount: 4, OrphanRoleCount: 4);

        var rng = new Random(seed);
        var log = new AppendOnlyGrantLog();
        var seq = 0;
        DateTime NextTime() => BaseTime.AddSeconds(seq++);

        var businessUnits = Enum.GetValues<BusinessUnit>();
        var instrumentClasses = Enum.GetValues<InstrumentClass>();

        // --- Onboard the user population ---
        var noiseUsers = new List<string>();
        for (var i = 0; i < noiseUserCount; i++)
        {
            var userId = $"U-POOL-{i + 1:D5}";
            noiseUsers.Add(userId);
            log.Append(GrantEventType.UserOnboarded, NextTime(), userId: userId);
        }

        var sodConflictCount = plan.Sod01Count + plan.Sod02Count + plan.Sod03Count;
        var sodUsers = new List<string>();
        for (var i = 0; i < sodConflictCount; i++)
        {
            var userId = $"U-SOD-{i + 1:D4}";
            sodUsers.Add(userId);
            log.Append(GrantEventType.UserOnboarded, NextTime(), userId: userId);
        }

        var orphanUserUsers = new List<string>();
        for (var i = 0; i < plan.OrphanUserCount; i++)
        {
            var userId = $"U-ORPHANUSER-{i + 1:D2}";
            orphanUserUsers.Add(userId);
            log.Append(GrantEventType.UserOnboarded, NextTime(), userId: userId);
        }

        // --- Define roles ---
        foreach (var role in RoleCatalogDefinitions.BusinessRoles)
        {
            log.Append(GrantEventType.RoleDefined, NextTime(), roleId: role.RoleId, rolePermissions: role.Permissions);
        }

        var orphanRoleIds = new List<string>();
        for (var i = 0; i < plan.OrphanRoleCount; i++)
        {
            var roleId = $"R-ORPHANROLE-{i + 1:D2}";
            orphanRoleIds.Add(roleId);
            log.Append(GrantEventType.RoleDefined, NextTime(), roleId: roleId,
                rolePermissions: new[] { Permission.ViewPosition });
        }

        var seededFindingIds = new List<string>();
        var grantCount = 0;
        var grantSeq = 0;
        string NextGrantId() => $"G-{++grantSeq:D6}";

        // Per (user, businessUnit, instrumentClass) running permission set, used
        // to reject noise grants that would create an unintended conflict.
        var scopePermissions = new Dictionary<(string, BusinessUnit, InstrumentClass), HashSet<Permission>>();

        void RecordScopePermissions(string userId, BusinessUnit bu, InstrumentClass ic, IEnumerable<Permission> perms)
        {
            var key = (userId, bu, ic);
            if (!scopePermissions.TryGetValue(key, out var set))
            {
                set = new HashSet<Permission>();
                scopePermissions[key] = set;
            }
            foreach (var p in perms) set.Add(p);
        }

        // --- Seed the segregation-of-duties conflicts ---
        var sodAssignments = new List<(SodRule Rule, RoleDefinition Maker, RoleDefinition Checker)>();
        for (var i = 0; i < plan.Sod01Count; i++)
            sodAssignments.Add((SodRuleCatalog.TradeInstructionMakerChecker, RoleCatalogDefinitions.TradeMaker, RoleCatalogDefinitions.TradeApprover));
        for (var i = 0; i < plan.Sod02Count; i++)
            sodAssignments.Add((SodRuleCatalog.WireTransferMakerChecker, RoleCatalogDefinitions.WireMaker, RoleCatalogDefinitions.WireApprover));
        for (var i = 0; i < plan.Sod03Count; i++)
            sodAssignments.Add((SodRuleCatalog.SecurityMasterMakerChecker, RoleCatalogDefinitions.SecurityMasterAnalyst, RoleCatalogDefinitions.SecurityMasterApprover));

        for (var i = 0; i < sodAssignments.Count; i++)
        {
            var (rule, makerRole, checkerRole) = sodAssignments[i];
            var userId = sodUsers[i];
            var bu = businessUnits[rng.Next(businessUnits.Length)];
            var ic = instrumentClasses[rng.Next(instrumentClasses.Length)];

            var makerGrantId = NextGrantId();
            log.Append(GrantEventType.Grant, NextTime(), userId: userId, roleId: makerRole.RoleId,
                businessUnit: bu, instrumentClass: ic, grantId: makerGrantId);
            grantCount++;

            var checkerGrantId = NextGrantId();
            log.Append(GrantEventType.Grant, NextTime(), userId: userId, roleId: checkerRole.RoleId,
                businessUnit: bu, instrumentClass: ic, grantId: checkerGrantId);
            grantCount++;

            RecordScopePermissions(userId, bu, ic, makerRole.Permissions);
            RecordScopePermissions(userId, bu, ic, checkerRole.Permissions);

            seededFindingIds.Add($"SOD|{rule.RuleId}|{userId}|{bu}|{ic}");
        }

        // --- Seed the orphaned grants: terminated user, grant left active ---
        foreach (var userId in orphanUserUsers)
        {
            var bu = businessUnits[rng.Next(businessUnits.Length)];
            var ic = instrumentClasses[rng.Next(instrumentClasses.Length)];
            var grantId = NextGrantId();
            log.Append(GrantEventType.Grant, NextTime(), userId: userId, roleId: RoleCatalogDefinitions.OperationsAnalyst.RoleId,
                businessUnit: bu, instrumentClass: ic, grantId: grantId);
            grantCount++;
            seededFindingIds.Add($"ORPHAN-USER|{grantId}");
        }

        // --- Seed the orphaned grants: deleted role, grant left active ---
        foreach (var roleId in orphanRoleIds)
        {
            var granteeUserId = noiseUsers[rng.Next(noiseUsers.Count)];
            var bu = businessUnits[rng.Next(businessUnits.Length)];
            var ic = instrumentClasses[rng.Next(instrumentClasses.Length)];
            var grantId = NextGrantId();
            log.Append(GrantEventType.Grant, NextTime(), userId: granteeUserId, roleId: roleId,
                businessUnit: bu, instrumentClass: ic, grantId: grantId);
            grantCount++;
            // ViewPosition alone never contributes to any SoD rule, so record
            // it anyway for completeness but it cannot trigger a conflict check.
            RecordScopePermissions(granteeUserId, bu, ic, new[] { Permission.ViewPosition });
            seededFindingIds.Add($"ORPHAN-ROLE|{grantId}");
        }

        // --- Noise: fill the remainder with grants that introduce nothing new ---
        var noiseTarget = totalGrantTarget - grantCount;
        var noiseAdded = 0;
        var grantedTriples = new HashSet<(string, string, BusinessUnit, InstrumentClass)>();
        var guardIterations = 0;
        var maxGuardIterations = noiseTarget * 50 + 1000;

        while (noiseAdded < noiseTarget && guardIterations < maxGuardIterations)
        {
            guardIterations++;
            var userId = noiseUsers[rng.Next(noiseUsers.Count)];
            var role = RoleCatalogDefinitions.BusinessRoles[rng.Next(RoleCatalogDefinitions.BusinessRoles.Count)];
            var bu = businessUnits[rng.Next(businessUnits.Length)];
            var ic = instrumentClasses[rng.Next(instrumentClasses.Length)];

            var triple = (userId, role.RoleId, bu, ic);
            if (grantedTriples.Contains(triple))
            {
                continue;
            }

            if (WouldIntroduceUnintendedConflict(scopePermissions, userId, bu, ic, role.Permissions))
            {
                continue;
            }

            var grantId = NextGrantId();
            log.Append(GrantEventType.Grant, NextTime(), userId: userId, roleId: role.RoleId,
                businessUnit: bu, instrumentClass: ic, grantId: grantId);
            grantCount++;
            noiseAdded++;
            grantedTriples.Add(triple);
            RecordScopePermissions(userId, bu, ic, role.Permissions);
        }

        if (noiseAdded < noiseTarget)
        {
            throw new InvalidOperationException(
                $"Synthetic population generator could not place {noiseTarget - noiseAdded} " +
                "more noise grants without risking an unintended conflict; widen the noise " +
                "population or raise the guard iteration budget.");
        }

        // --- Terminate the orphan-user users and delete the orphan roles,
        //     deliberately with no matching Revoke, so their grants go stale. ---
        foreach (var userId in orphanUserUsers)
        {
            log.Append(GrantEventType.UserTerminated, NextTime(), userId: userId);
        }
        foreach (var roleId in orphanRoleIds)
        {
            log.Append(GrantEventType.RoleDeleted, NextTime(), roleId: roleId);
        }

        return new SyntheticPopulationResult
        {
            Log = log,
            TotalGrantCount = grantCount,
            SeededFindingIds = seededFindingIds.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            Plan = plan
        };
    }

    private static bool WouldIntroduceUnintendedConflict(
        Dictionary<(string, BusinessUnit, InstrumentClass), HashSet<Permission>> scopePermissions,
        string userId, BusinessUnit bu, InstrumentClass ic, IReadOnlyList<Permission> candidatePermissions)
    {
        var key = (userId, bu, ic);
        scopePermissions.TryGetValue(key, out var existing);
        existing ??= new HashSet<Permission>();

        foreach (var rule in SodRuleCatalog.AllRules)
        {
            var hadBoth = existing.Contains(rule.Maker) && existing.Contains(rule.Checker);
            if (hadBoth)
            {
                continue; // already a known (seeded) conflict scope; adding more here is harmless
            }

            var wouldHaveMaker = existing.Contains(rule.Maker) || candidatePermissions.Contains(rule.Maker);
            var wouldHaveChecker = existing.Contains(rule.Checker) || candidatePermissions.Contains(rule.Checker);
            if (wouldHaveMaker && wouldHaveChecker)
            {
                return true;
            }
        }

        return false;
    }
}
