using System.Security.Cryptography;
using System.Text;
using Entitlement.Domain.Models;

namespace Entitlement.Domain.Log;

/// <summary>
/// The append-only grant log. This is the single source of truth for the
/// entitlement world; everything else (snapshots, findings, evidence packs) is a
/// pure function of the sequence of entries here.
///
/// The append-only invariant is enforced structurally, not by convention:
/// <see cref="Entries"/> returns a read-only view over an internal list that is
/// only ever grown by <see cref="Append"/>, which assigns the next sequence
/// number itself (callers cannot choose or reuse one), and
/// <see cref="PriorEntriesDigest"/> lets a caller prove, after an append, that
/// every entry that existed before still hashes the same, i.e. the append did
/// not rewrite history. There is no method anywhere on this type that removes,
/// replaces, or reorders an existing entry.
/// </summary>
public sealed class AppendOnlyGrantLog
{
    private readonly List<GrantLogEntry> _entries = new();

    public IReadOnlyList<GrantLogEntry> Entries => _entries.AsReadOnly();

    public GrantLogEntry Append(
        GrantEventType eventType,
        DateTime timestampUtc,
        string? userId = null,
        string? roleId = null,
        IReadOnlyList<Permission>? rolePermissions = null,
        BusinessUnit? businessUnit = null,
        InstrumentClass? instrumentClass = null,
        string? grantId = null)
    {
        var entry = new GrantLogEntry
        {
            SequenceNumber = _entries.Count + 1,
            TimestampUtc = timestampUtc,
            EventType = eventType,
            UserId = userId,
            RoleId = roleId,
            RolePermissions = rolePermissions,
            BusinessUnit = businessUnit,
            InstrumentClass = instrumentClass,
            GrantId = grantId
        };
        _entries.Add(entry);
        return entry;
    }

    /// <summary>
    /// SHA-256 over the first <paramref name="count"/> entries, serialized in a
    /// fixed field order. Used by tests to prove that appending entry N+1 never
    /// changes the hash of entries 1..N, i.e. the log is genuinely append-only
    /// rather than merely "not observed to mutate yet".
    /// </summary>
    public string PriorEntriesDigest(int count)
    {
        if (count < 0 || count > _entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            AppendEntry(sb, _entries[i]);
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }

    internal static void AppendEntry(StringBuilder sb, GrantLogEntry e)
    {
        sb.Append(e.SequenceNumber).Append('|')
          .Append(e.TimestampUtc.Ticks).Append('|')
          .Append(e.EventType).Append('|')
          .Append(e.UserId).Append('|')
          .Append(e.RoleId).Append('|')
          .Append(e.RolePermissions is null ? "" : string.Join(',', e.RolePermissions)).Append('|')
          .Append(e.BusinessUnit).Append('|')
          .Append(e.InstrumentClass).Append('|')
          .Append(e.GrantId).Append('\n');
    }
}
