using System.Net;
using System.Net.Http.Json;
using Entitlement.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Entitlement.Tests.Api;

/// <summary>
/// Exercises the ASP.NET Core API over its real HTTP contract using
/// <see cref="WebApplicationFactory{TEntryPoint}"/>, an in-process test host.
/// No real Kestrel listener or port is ever bound for these tests.
/// </summary>
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCampaignSummary_ReturnsFourThousandGrantsAndTwentyEightFindings()
    {
        var client = _factory.CreateClient();

        var response = await client.GetFromJsonAsync<CampaignSummaryResponse>("/api/campaigns/2026-Q3");

        Assert.NotNull(response);
        Assert.Equal(4000, response!.ActiveGrants);
        Assert.Equal(28, response.FindingCount);
        Assert.Contains(response.Findings, f => f.SodRuleId == "SOD-01");
    }

    [Fact]
    public async Task GetEvidencePack_IsByteIdenticalAcrossTwoRequests()
    {
        var client = _factory.CreateClient();

        var pack1 = await client.GetStringAsync("/api/campaigns/2026-Q3/evidence-pack");
        var pack2 = await client.GetStringAsync("/api/campaigns/2026-Q3/evidence-pack");

        Assert.Equal(pack1, pack2);
    }

    [Fact]
    public async Task PostCertification_BlankReason_ReturnsUnprocessable()
    {
        var client = _factory.CreateClient();
        var summary = await client.GetFromJsonAsync<CampaignSummaryResponse>("/api/campaigns/2026-Q3");
        var findingId = summary!.Findings[0].FindingId;

        var response = await client.PostAsJsonAsync("/api/certifications",
            new CertifyRequest(findingId, Entitlement.Domain.Models.CertificationDecisionType.Approved, "alice", ""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PostCertification_ValidReasonThenRepeat_SecondAttemptRejected()
    {
        var client = _factory.CreateClient();
        var summary = await client.GetFromJsonAsync<CampaignSummaryResponse>("/api/campaigns/2026-Q3");
        var findingId = summary!.Findings[1].FindingId;

        var first = await client.PostAsJsonAsync("/api/certifications",
            new CertifyRequest(findingId, Entitlement.Domain.Models.CertificationDecisionType.Revoked, "alice", "Confirmed conflict, grant revoked."));
        var second = await client.PostAsJsonAsync("/api/certifications",
            new CertifyRequest(findingId, Entitlement.Domain.Models.CertificationDecisionType.Approved, "bob", "changed my mind"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    [Fact]
    public async Task PersistGrantLog_ThenCount_RoundTripsThroughEfCoreSqlite()
    {
        var client = _factory.CreateClient();

        var persistResponse = await client.PostAsync("/api/grant-log/persist", content: null);
        persistResponse.EnsureSuccessStatusCode();
        var persisted = await persistResponse.Content.ReadFromJsonAsync<PersistResult>();

        var countResponse = await client.GetStringAsync("/api/grant-log/count");

        Assert.NotNull(persisted);
        Assert.True(persisted!.EntriesPersisted > 0);
        Assert.Contains(persisted.EntriesPersisted.ToString(), countResponse);
    }
}
