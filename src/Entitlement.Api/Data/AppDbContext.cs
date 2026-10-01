using Microsoft.EntityFrameworkCore;

namespace Entitlement.Api.Data;

/// <summary>
/// EF Core context for the grant log's durable store. SQL Server is the
/// intended production provider (<c>Database:Provider = SqlServer</c>,
/// configured against the connection string in appsettings.json); SQLite is
/// what every measurement in this repository's README actually exercised,
/// because no SQL Server instance was reachable on the build machine. See the
/// README's Honest framing section for the full disclosure. The DbSet and its
/// mapping are identical either way; only the underlying provider changes.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<GrantLogEntryEntity> GrantLogEntries => Set<GrantLogEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GrantLogEntryEntity>(e =>
        {
            e.HasKey(x => x.SequenceNumber);
            e.Property(x => x.EventType).IsRequired();
        });
    }
}
