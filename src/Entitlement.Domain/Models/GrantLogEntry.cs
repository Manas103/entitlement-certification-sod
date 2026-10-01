namespace Entitlement.Domain.Models;

/// <summary>
/// One immutable fact in the append-only grant log. <see cref="SequenceNumber"/> is
/// assigned by <see cref="Log.AppendOnlyGrantLog"/> at append time and is never
/// reassigned; replaying entries 1..N in sequence order is the only way the
/// current entitlement state is ever computed (see
/// <see cref="Engine.EntitlementSnapshotBuilder"/>). Nothing in this type is
/// mutable after construction.
/// </summary>
public sealed record GrantLogEntry
{
    public required long SequenceNumber { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required GrantEventType EventType { get; init; }

    /// <summary>User the event concerns. Always set for Grant/Revoke/UserOnboarded/UserTerminated.</summary>
    public string? UserId { get; init; }

    /// <summary>Role the event concerns. Always set for Grant/Revoke/RoleDefined/RoleDeleted.</summary>
    public string? RoleId { get; init; }

    /// <summary>Permissions a RoleDefined event attaches to RoleId. Null for every other event type.</summary>
    public IReadOnlyList<Permission>? RolePermissions { get; init; }

    /// <summary>Attribute scope of a Grant/Revoke. Null for user/role lifecycle events.</summary>
    public BusinessUnit? BusinessUnit { get; init; }
    public InstrumentClass? InstrumentClass { get; init; }

    /// <summary>Stable identifier of the specific grant a Grant/Revoke pair refers to.</summary>
    public string? GrantId { get; init; }
}
