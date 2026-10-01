using Entitlement.Domain.Log;
using Entitlement.Domain.Models;

namespace Entitlement.Domain.Engine;

/// <summary>
/// Replays an <see cref="AppendOnlyGrantLog"/> into the current entitlement
/// state. This is the only place event order matters; everything downstream
/// (detection, campaigns, evidence packs) consumes the resulting
/// <see cref="EntitlementSnapshot"/>, never the raw log.
///
/// Deliberately a dumb, linear fold: one pass over the entries, in sequence
/// order, mutating a small amount of state. There is nothing here a fast path
/// could get wrong by taking a shortcut, which is the point: the interesting
/// correctness question in this repository is detection (see
/// <see cref="Entitlement.Domain.Detection"/>), not replay.
/// </summary>
public static class EntitlementSnapshotBuilder
{
    public static EntitlementSnapshot Build(AppendOnlyGrantLog log)
    {
        var activeUsers = new HashSet<string>(StringComparer.Ordinal);
        var terminatedUsers = new HashSet<string>(StringComparer.Ordinal);
        var roleCatalog = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal);
        var deletedRoles = new HashSet<string>(StringComparer.Ordinal);
        var activeGrants = new Dictionary<string, EffectiveGrant>(StringComparer.Ordinal);

        foreach (var e in log.Entries.OrderBy(x => x.SequenceNumber))
        {
            switch (e.EventType)
            {
                case GrantEventType.UserOnboarded:
                    activeUsers.Add(e.UserId!);
                    terminatedUsers.Remove(e.UserId!);
                    break;

                case GrantEventType.UserTerminated:
                    activeUsers.Remove(e.UserId!);
                    terminatedUsers.Add(e.UserId!);
                    break;

                case GrantEventType.RoleDefined:
                    roleCatalog[e.RoleId!] = new RoleDefinition
                    {
                        RoleId = e.RoleId!,
                        Permissions = e.RolePermissions ?? Array.Empty<Permission>()
                    };
                    deletedRoles.Remove(e.RoleId!);
                    break;

                case GrantEventType.RoleDeleted:
                    roleCatalog.Remove(e.RoleId!);
                    deletedRoles.Add(e.RoleId!);
                    break;

                case GrantEventType.Grant:
                    activeGrants[e.GrantId!] = new EffectiveGrant
                    {
                        GrantId = e.GrantId!,
                        UserId = e.UserId!,
                        RoleId = e.RoleId!,
                        BusinessUnit = e.BusinessUnit!.Value,
                        InstrumentClass = e.InstrumentClass!.Value,
                        GrantedAtUtc = e.TimestampUtc
                    };
                    break;

                case GrantEventType.Revoke:
                    activeGrants.Remove(e.GrantId!);
                    break;
            }
        }

        return new EntitlementSnapshot
        {
            ActiveUserIds = activeUsers,
            TerminatedUserIds = terminatedUsers,
            RoleCatalog = roleCatalog,
            DeletedRoleIds = deletedRoles,
            ActiveGrants = activeGrants.Values
                .OrderBy(g => g.GrantId, StringComparer.Ordinal)
                .ToList()
        };
    }
}
