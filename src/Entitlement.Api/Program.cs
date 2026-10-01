using Entitlement.Api.Contracts;
using Entitlement.Api.Data;
using Entitlement.Api.Services;
using Entitlement.Domain.Certification;
using Entitlement.Domain.Engine;
using Entitlement.Domain.Detection;
using Entitlement.Domain.Reporting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<GrantLogAppState>();

// Provider switch: SQLite by default (what every measurement in this
// repository actually exercised), SQL Server if Database:Provider=SqlServer
// is set against a reachable instance. See README "Honest framing".
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (provider == "SqlServer")
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServerConnection"));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection")
            ?? "Data Source=entitlement_certification.db");
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure the configured provider's schema exists; harmless no-op if already created.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/api/campaigns/{quarter}", (string quarter, GrantLogAppState state) =>
{
    var snapshot = EntitlementSnapshotBuilder.Build(state.Log);
    var findings = FastConflictDetector.Detect(snapshot);
    var response = new CampaignSummaryResponse(
        quarter,
        snapshot.ActiveUserIds.Count,
        snapshot.RoleCatalog.Count,
        snapshot.ActiveGrants.Count,
        findings.Count,
        findings.Select(f => new FindingResponse(
            f.FindingId, f.Type.ToString(), f.UserId, f.SodRuleId,
            f.BusinessUnit?.ToString(), f.InstrumentClass?.ToString(), f.GrantIds, f.Description))
            .ToList());
    return Results.Ok(response);
})
.WithName("GetCampaignSummary");

app.MapGet("/api/campaigns/{quarter}/evidence-pack", (string quarter, GrantLogAppState state) =>
{
    var pack = EvidencePackGenerator.Generate(state.Log, quarter);
    return Results.Text(pack, "text/plain");
})
.WithName("GetEvidencePack");

app.MapPost("/api/certifications", (CertifyRequest request, GrantLogAppState state) =>
{
    var result = state.Certifications.Certify(
        request.FindingId, request.Decision, request.Reviewer, request.Reason, DateTime.UtcNow);
    var response = new CertifyResponse(result.Success, result.Error);
    return result.Success ? Results.Ok(response) : Results.UnprocessableEntity(response);
})
.WithName("CertifyFinding");

app.MapPost("/api/grant-log/persist", async (GrantLogAppState state, AppDbContext db) =>
{
    db.GrantLogEntries.RemoveRange(db.GrantLogEntries);
    foreach (var e in state.Log.Entries)
    {
        db.GrantLogEntries.Add(new GrantLogEntryEntity
        {
            SequenceNumber = e.SequenceNumber,
            TimestampUtc = e.TimestampUtc,
            EventType = e.EventType.ToString(),
            UserId = e.UserId,
            RoleId = e.RoleId,
            RolePermissionsCsv = e.RolePermissions is null ? null : string.Join(',', e.RolePermissions),
            BusinessUnit = e.BusinessUnit?.ToString(),
            InstrumentClass = e.InstrumentClass?.ToString(),
            GrantId = e.GrantId
        });
    }
    await db.SaveChangesAsync();
    return Results.Ok(new PersistResult(state.Log.Entries.Count, provider));
})
.WithName("PersistGrantLog");

app.MapGet("/api/grant-log/count", async (AppDbContext db) =>
    Results.Ok(new { PersistedEntries = await db.GrantLogEntries.CountAsync() }))
.WithName("GetPersistedGrantLogCount");

app.Run();

public partial class Program { }
