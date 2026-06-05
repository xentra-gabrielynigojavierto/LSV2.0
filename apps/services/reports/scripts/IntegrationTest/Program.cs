using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Reports.Api.Endpoints;
using Reports.Api.Middleware;
using Reports.Application;
using Reports.Application.Templates.DTOs;
using Reports.Infrastructure;
using Reports.Worker.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");

builder.Services.AddReportsApplication();
builder.Services.AddReportsInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReportWorkerService>();

var app = builder.Build();
app.UseMiddleware<RequestLoggingMiddleware>();
app.MapHealthEndpoints();
app.MapTemplateEndpoints();

await app.StartAsync();
var addresses = app.Urls;
var baseUrl = addresses.First();
Console.WriteLine($"Test server running at: {baseUrl}");

var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
var jso = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var passed = 0;
var failed = 0;

void Assert(bool condition, string testName)
{
    if (condition) { passed++; Console.WriteLine($"  PASS: {testName}"); }
    else { failed++; Console.WriteLine($"  FAIL: {testName}"); }
}

try
{
    Console.WriteLine("\n=== 1. Health Check ===");
    var healthResp = await client.GetAsync("/api/v1/health");
    Assert(healthResp.IsSuccessStatusCode, "GET /api/v1/health returns 200");
    var healthJson = await healthResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {healthJson}");

    Console.WriteLine("\n=== 2. Readiness Check ===");
    var readyResp = await client.GetAsync("/api/v1/ready");
    var readyJson = await readyResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Status: {(int)readyResp.StatusCode}");
    Console.WriteLine($"  Response: {readyJson}");
    Assert(readyResp.StatusCode == HttpStatusCode.OK || readyResp.StatusCode == HttpStatusCode.ServiceUnavailable, "GET /api/v1/ready returns valid response");

    Console.WriteLine("\n=== 3. Create Template ===");
    var testCode = $"RPT-INT-{DateTime.UtcNow:yyyyMMddHHmmss}";
    var createReq = new { code = testCode, name = "Integration Test Report", description = "Test template", productCode = "SYNQLIEN", organizationType = "LawFirm", isActive = true };
    var createResp = await client.PostAsJsonAsync("/api/v1/templates", createReq);
    Assert(createResp.StatusCode == HttpStatusCode.Created, $"POST /api/v1/templates returns 201 (got {(int)createResp.StatusCode})");
    var createJson = await createResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {createJson}");
    var template = JsonSerializer.Deserialize<TemplateResponse>(createJson, jso)!;
    var templateId = template.Id;
    Console.WriteLine($"  Template ID: {templateId}");
    Assert(template.Code == testCode, "Template code matches");
    Assert(template.OrganizationType == "LawFirm", "OrganizationType matches");
    Assert(template.CurrentVersion == 0, "CurrentVersion is 0 (no versions)");

    Console.WriteLine("\n=== 4. Duplicate Code (409) ===");
    var dupResp = await client.PostAsJsonAsync("/api/v1/templates", createReq);
    Assert(dupResp.StatusCode == HttpStatusCode.Conflict, $"Duplicate code returns 409 (got {(int)dupResp.StatusCode})");

    Console.WriteLine("\n=== 5. Validation Error (400) ===");
    var badReq = new { code = "", name = "", productCode = "", organizationType = "" };
    var badResp = await client.PostAsJsonAsync("/api/v1/templates", badReq);
    Assert(badResp.StatusCode == HttpStatusCode.BadRequest, $"Empty fields returns 400 (got {(int)badResp.StatusCode})");

    Console.WriteLine("\n=== 6. Update Template ===");
    var updateReq = new { name = "Updated Report", description = "Updated description", productCode = "SYNQLIEN", organizationType = "MedicalProvider", isActive = true };
    var updateResp = await client.PutAsJsonAsync($"/api/v1/templates/{templateId}", updateReq);
    Assert(updateResp.IsSuccessStatusCode, $"PUT /api/v1/templates/{templateId} returns 200 (got {(int)updateResp.StatusCode})");
    var updateJson = await updateResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {updateJson}");
    var updated = JsonSerializer.Deserialize<TemplateResponse>(updateJson, jso)!;
    Assert(updated.Name == "Updated Report", "Name updated");
    Assert(updated.OrganizationType == "MedicalProvider", "OrganizationType updated");

    Console.WriteLine("\n=== 7. Get Template By ID ===");
    var getResp = await client.GetAsync($"/api/v1/templates/{templateId}");
    Assert(getResp.IsSuccessStatusCode, $"GET /api/v1/templates/{templateId} returns 200");
    var getJson = await getResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {getJson}");

    Console.WriteLine("\n=== 8. Get Template Not Found (404) ===");
    var nfResp = await client.GetAsync($"/api/v1/templates/{Guid.Empty}");
    Assert(nfResp.StatusCode == HttpStatusCode.NotFound, $"Non-existent template returns 404 (got {(int)nfResp.StatusCode})");

    Console.WriteLine("\n=== 9. List Templates ===");
    var listResp = await client.GetAsync("/api/v1/templates?productCode=SYNQLIEN&organizationType=MedicalProvider");
    Assert(listResp.IsSuccessStatusCode, "GET /api/v1/templates returns 200");
    var listJson = await listResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {listJson}");

    Console.WriteLine("\n=== 10. Create Version 1 ===");
    var v1Req = new { templateBody = "<h1>Report v1</h1>", outputFormat = "PDF", changeNotes = "Initial version", isActive = true, createdByUserId = "user-001" };
    var v1Resp = await client.PostAsJsonAsync($"/api/v1/templates/{templateId}/versions", v1Req);
    Assert(v1Resp.StatusCode == HttpStatusCode.Created, $"Create version returns 201 (got {(int)v1Resp.StatusCode})");
    var v1Json = await v1Resp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {v1Json}");
    var v1 = JsonSerializer.Deserialize<TemplateVersionResponse>(v1Json, jso)!;
    Assert(v1.VersionNumber == 1, $"Version number is 1 (got {v1.VersionNumber})");
    Assert(!v1.IsPublished, "Version 1 not published on creation");

    Console.WriteLine("\n=== 11. Create Version 2 ===");
    var v2Req = new { templateBody = "<h1>Report v2</h1>", outputFormat = "PDF", changeNotes = "Second version", isActive = true, createdByUserId = "user-001" };
    var v2Resp = await client.PostAsJsonAsync($"/api/v1/templates/{templateId}/versions", v2Req);
    Assert(v2Resp.StatusCode == HttpStatusCode.Created, $"Create version 2 returns 201 (got {(int)v2Resp.StatusCode})");
    var v2Json = await v2Resp.Content.ReadAsStringAsync();
    var v2 = JsonSerializer.Deserialize<TemplateVersionResponse>(v2Json, jso)!;
    Assert(v2.VersionNumber == 2, $"Version number is 2 (got {v2.VersionNumber})");

    Console.WriteLine("\n=== 12. Get Latest Version ===");
    var latestResp = await client.GetAsync($"/api/v1/templates/{templateId}/versions/latest");
    Assert(latestResp.IsSuccessStatusCode, "GET latest version returns 200");
    var latestJson = await latestResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {latestJson}");
    var latest = JsonSerializer.Deserialize<TemplateVersionResponse>(latestJson, jso)!;
    Assert(latest.VersionNumber == 2, $"Latest version is 2 (got {latest.VersionNumber})");

    Console.WriteLine("\n=== 13. Get Published Version (before publish) ===");
    var pubBefore = await client.GetAsync($"/api/v1/templates/{templateId}/versions/published");
    Assert(pubBefore.StatusCode == HttpStatusCode.NotFound, $"No published version returns 404 (got {(int)pubBefore.StatusCode})");

    Console.WriteLine("\n=== 14. Publish Version 1 ===");
    var pubReq = new { publishedByUserId = "admin-001" };
    var pub1Resp = await client.PostAsJsonAsync($"/api/v1/templates/{templateId}/versions/1/publish", pubReq);
    Assert(pub1Resp.IsSuccessStatusCode, $"Publish v1 returns 200 (got {(int)pub1Resp.StatusCode})");
    var pub1Json = await pub1Resp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {pub1Json}");
    var pub1 = JsonSerializer.Deserialize<TemplateVersionResponse>(pub1Json, jso)!;
    Assert(pub1.IsPublished, "Version 1 is published");

    Console.WriteLine("\n=== 15. Get Published Version (after publish v1) ===");
    var pubAfter1 = await client.GetAsync($"/api/v1/templates/{templateId}/versions/published");
    Assert(pubAfter1.IsSuccessStatusCode, "Get published version returns 200");
    var pubAfter1Data = JsonSerializer.Deserialize<TemplateVersionResponse>(await pubAfter1.Content.ReadAsStringAsync(), jso)!;
    Assert(pubAfter1Data.VersionNumber == 1, $"Published version is 1 (got {pubAfter1Data.VersionNumber})");

    Console.WriteLine("\n=== 16. Publish Version 2 (switch) ===");
    var pub2Resp = await client.PostAsJsonAsync($"/api/v1/templates/{templateId}/versions/2/publish", pubReq);
    Assert(pub2Resp.IsSuccessStatusCode, $"Publish v2 returns 200 (got {(int)pub2Resp.StatusCode})");
    var pub2Data = JsonSerializer.Deserialize<TemplateVersionResponse>(await pub2Resp.Content.ReadAsStringAsync(), jso)!;
    Assert(pub2Data.IsPublished, "Version 2 is now published");
    Assert(pub2Data.VersionNumber == 2, $"Published version switched to 2 (got {pub2Data.VersionNumber})");

    Console.WriteLine("\n=== 17. Confirm v1 is no longer published ===");
    var pubCheck = await client.GetAsync($"/api/v1/templates/{templateId}/versions/published");
    var pubCheckData = JsonSerializer.Deserialize<TemplateVersionResponse>(await pubCheck.Content.ReadAsStringAsync(), jso)!;
    Assert(pubCheckData.VersionNumber == 2, $"Only version 2 is published (got {pubCheckData.VersionNumber})");

    Console.WriteLine("\n=== 18. Idempotent Publish ===");
    var pub3Resp = await client.PostAsJsonAsync($"/api/v1/templates/{templateId}/versions/2/publish", pubReq);
    Assert(pub3Resp.IsSuccessStatusCode, $"Idempotent publish returns 200 (got {(int)pub3Resp.StatusCode})");

    Console.WriteLine("\n=== 19. List All Versions ===");
    var versionsResp = await client.GetAsync($"/api/v1/templates/{templateId}/versions");
    Assert(versionsResp.IsSuccessStatusCode, "GET versions returns 200");
    var versionsJson = await versionsResp.Content.ReadAsStringAsync();
    Console.WriteLine($"  Response: {versionsJson}");
    var versions = JsonSerializer.Deserialize<List<TemplateVersionResponse>>(versionsJson, jso)!;
    Assert(versions.Count == 2, $"Two versions exist (got {versions.Count})");
    var publishedCount = versions.Count(v => v.IsPublished);
    Assert(publishedCount == 1, $"Exactly one published version (got {publishedCount})");

    Console.WriteLine("\n=== 20. Concurrent Version Creation ===");
    var tasks = new[]
    {
        client.PostAsJsonAsync($"/api/v1/templates/{templateId}/versions",
            new { templateBody = "v3 concurrent", outputFormat = "PDF", changeNotes = "Concurrent A", isActive = true, createdByUserId = "user-001" }),
        client.PostAsJsonAsync($"/api/v1/templates/{templateId}/versions",
            new { templateBody = "v4 concurrent", outputFormat = "PDF", changeNotes = "Concurrent B", isActive = true, createdByUserId = "user-001" })
    };
    var results = await Task.WhenAll(tasks);
    var createdVersions = new List<int>();
    var allCreated = true;
    foreach (var r in results)
    {
        if (r.StatusCode == HttpStatusCode.Created)
        {
            var vd = JsonSerializer.Deserialize<TemplateVersionResponse>(await r.Content.ReadAsStringAsync(), jso)!;
            createdVersions.Add(vd.VersionNumber);
            Console.WriteLine($"  Created version {vd.VersionNumber}");
        }
        else
        {
            allCreated = false;
            Console.WriteLine($"  Response: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        }
    }
    Assert(allCreated, $"Both concurrent creates returned 201 ({createdVersions.Count}/2 succeeded)");
    Assert(createdVersions.Count == 2, $"Exactly 2 versions created concurrently (got {createdVersions.Count})");
    var distinctVersions = createdVersions.Distinct().Count();
    Assert(distinctVersions == createdVersions.Count, $"No duplicate version numbers (versions: {string.Join(", ", createdVersions)})");

    Console.WriteLine("\n=== 21. Final Version List (sequential check) ===");
    var finalVersions = await client.GetAsync($"/api/v1/templates/{templateId}/versions");
    var finalVersionsData = JsonSerializer.Deserialize<List<TemplateVersionResponse>>(await finalVersions.Content.ReadAsStringAsync(), jso)!;
    Assert(finalVersionsData.Count == 4, $"Total 4 versions after concurrent creates (got {finalVersionsData.Count})");
    var versionNumbers = finalVersionsData.Select(v => v.VersionNumber).OrderBy(n => n).ToList();
    Console.WriteLine($"  Version numbers: {string.Join(", ", versionNumbers)}");
    var isSequential = true;
    for (int i = 0; i < versionNumbers.Count; i++)
        if (versionNumbers[i] != i + 1) isSequential = false;
    Assert(isSequential, $"Version numbers are sequential (1..{versionNumbers.Count})");

    Console.WriteLine("\n=== 22. Check template CurrentVersion updated ===");
    var finalTemplate = JsonSerializer.Deserialize<TemplateResponse>(await (await client.GetAsync($"/api/v1/templates/{templateId}")).Content.ReadAsStringAsync(), jso)!;
    Assert(finalTemplate.CurrentVersion == versionNumbers.Max(), $"Template CurrentVersion matches latest ({finalTemplate.CurrentVersion} == {versionNumbers.Max()})");
}
catch (Exception ex)
{
    Console.WriteLine($"\nEXCEPTION: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    failed++;
}

Console.WriteLine($"\n========================================");
Console.WriteLine($"Results: {passed} passed, {failed} failed");
Console.WriteLine($"========================================");

await app.StopAsync();

return failed > 0 ? 1 : 0;
