using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GhostWatch.Tests;

public class HostTests
{
    [Fact]
    public async Task Health_reports_product_identity_and_unknown_api_is_not_an_html_page()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
        Assert.Contains("Ghost Watch Management Dashboard", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/missing")).StatusCode);
    }
}
