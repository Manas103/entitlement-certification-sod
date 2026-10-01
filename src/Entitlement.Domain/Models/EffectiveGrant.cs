namespace Entitlement.Domain.Models;

/// <summary>
/// One active (not revoked) user-to-role grant, scoped by business unit and
/// instrument class, as computed by replaying the grant log. This is the "4,000
/// user-role pairs" unit the quarterly campaign reviews.
/// </summary>
public sealed record EffectiveGrant
{
    public required string GrantId { get; init; }
    public required string UserId { get; init; }
    public required string RoleId { get; init; }
    public required BusinessUnit BusinessUnit { get; init; }
    public required InstrumentClass InstrumentClass { get; init; }
    public required DateTime GrantedAtUtc { get; init; }
}

/// <summary>A role as currently defined (or, for a deleted role, as it last was). </summary>
public sealed record RoleDefinition
{
    public required string RoleId { get; init; }
    public required IReadOnlyList<Permission> Permissions { get; init; }
}

/// <summary>
/// The fully replayed state of the entitlement world at a point in the log:
/// which users are active, which roles are currently defined, and which grants
/// are currently active. Orphan detection is a direct comparison of
/// <see cref="ActiveGrants"/> against <see cref="ActiveUserIds"/> and
/// <see cref="RoleCatalog"/>.
/// </summary>
public sealed class EntitlementSnapshot
{
    public required IReadOnlySet<string> ActiveUserIds { get; init; }
    public required IReadOnlySet<string> TerminatedUserIds { get; init; }
    public required IReadOnlyDictionary<string, RoleDefinition> RoleCatalog { get; init; }
    public required IReadOnlySet<string> DeletedRoleIds { get; init; }
    public required IReadOnlyList<EffectiveGrant> ActiveGrants { get; init; }
}
