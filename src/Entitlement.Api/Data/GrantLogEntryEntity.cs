namespace Entitlement.Api.Data;

/// <summary>
/// The persisted row shape for one append-only grant log entry. This is the EF
/// Core side of the same log the Domain's in-memory <c>AppendOnlyGrantLog</c>
/// models; the API persists entries here to demonstrate the real EF Core
/// provider path described in the README, separately from the in-process
/// detection and evidence-pack logic, which never touches the database.
/// </summary>
public sealed class GrantLogEntryEntity
{
    public long SequenceNumber { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string EventType { get; set; } = "";
    public string? UserId { get; set; }
    public string? RoleId { get; set; }
    public string? RolePermissionsCsv { get; set; }
    public string? BusinessUnit { get; set; }
    public string? InstrumentClass { get; set; }
    public string? GrantId { get; set; }
}
