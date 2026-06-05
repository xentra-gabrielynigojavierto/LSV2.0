using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Support.Tests;

/// <summary>SUP-INT-08 upload-endpoint coverage.</summary>
public class FileUploadTests : IClassFixture<FileUploadApiFactory>
{
    private readonly FileUploadApiFactory _factory;

    public FileUploadTests(FileUploadApiFactory factory)
    {
        _factory = factory;
        _factory.Recorder.ThrowNotConfigured = false;
        _factory.Recorder.ThrowRemote = false;
    }

    private HttpClient ClientForTenant(string tenantId, string sub = "test-user")
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        c.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        return c;
    }

    private static async Task<TicketResponse> CreateTicketAsync(HttpClient c)
    {
        var resp = await c.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = "T",
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TicketResponse>())!;
    }

    private static MultipartFormDataContent BuildUpload(
        byte[] bytes, string fileName, string contentType,
        string? displayName = null, string? uploadedByUserId = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        if (displayName is not null)
            content.Add(new StringContent(displayName), "display_name");
        if (uploadedByUserId is not null)
            content.Add(new StringContent(uploadedByUserId), "uploaded_by_user_id");
        return content;
    }

    // ------------------------------------------------------------------
    //  Happy path
    // ------------------------------------------------------------------
    [Fact]
    public async Task Upload_Succeeds_And_Returns_Attachment_Reference()
    {
        var c = ClientForTenant("tenant-UP-1", sub: "real-user-1");
        var t = await CreateTicketAsync(c);

        _factory.Recorder.DocumentIdOverride = "doc-upload-success";

        var bytes = Encoding.UTF8.GetBytes("hello world pdf bytes");
        // Intentionally pass an `uploaded_by_user_id` form value that does NOT
        // match the JWT subject; the server must ignore the form value and use
        // the JWT subject for attribution (see SUP-INT-08 security review).
        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(bytes, "report.pdf", "application/pdf",
                uploadedByUserId: "spoofed-user-x"));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<TicketAttachmentResponse>();
        body!.DocumentId.Should().Be("doc-upload-success");
        body.FileName.Should().Be("report.pdf");
        body.TicketId.Should().Be(t.Id);
        body.FileSizeBytes.Should().Be(bytes.Length);
        body.UploadedByUserId.Should().Be("real-user-1",
            "uploader identity must come from the JWT subject, not the form field");

        _factory.Recorder.DocumentIdOverride = null;
    }

    [Fact]
    public async Task Upload_Form_Field_Cannot_Spoof_Uploader_Identity()
    {
        // Defense-in-depth: even if every other check passes, a caller cannot
        // forge `uploaded_by_user_id` to attribute the upload to someone else.
        var c = ClientForTenant("tenant-UP-spoof", sub: "honest-actor");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("hi"), "h.txt", "text/plain",
                uploadedByUserId: "admin-impersonation-attempt"));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<TicketAttachmentResponse>();
        body!.UploadedByUserId.Should().Be("honest-actor");

        // Also: the recorded provider call must reflect the JWT subject, so
        // any downstream Documents Service call cannot be misattributed.
        var call = _factory.Recorder.Calls.Last(x => x.TicketId == t.Id);
        call.UploadedByUserId.Should().Be("honest-actor");
    }

    [Fact]
    public async Task Upload_Calls_Configured_Storage_Provider()
    {
        var c = ClientForTenant("tenant-UP-2");
        var t = await CreateTicketAsync(c);
        var before = _factory.Recorder.Calls.Count;

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("a"), "a.txt", "text/plain"));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.Recorder.Calls.Should().HaveCountGreaterThan(before);
        var call = _factory.Recorder.Calls.Last(x => x.TicketId == t.Id);
        call.TenantId.Should().Be("tenant-UP-2");
        call.FileName.Should().Be("a.txt");
        call.ContentType.Should().Be("text/plain");
    }

    [Fact]
    public async Task Upload_Persists_Attachment_Row_Without_File_Bytes()
    {
        var c = ClientForTenant("tenant-UP-3");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("x"), "x.txt", "text/plain"));
        resp.EnsureSuccessStatusCode();

        // Inspect the DB directly: the attachment row exists, but the schema
        // has no column for file bytes — so by definition we cannot have stored
        // any. Verifying the row is present and correctly references the
        // provider-issued document id is the strongest contractual check.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var row = await db.TicketAttachments.AsNoTracking()
            .SingleAsync(a => a.TicketId == t.Id && a.TenantId == "tenant-UP-3");

        row.DocumentId.Should().NotBeNullOrEmpty();
        row.FileName.Should().Be("x.txt");

        var props = typeof(SupportTicketAttachment)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();
        props.Should().NotContain("FileBytes");
        props.Should().NotContain("Content");
        props.Should().NotContain("Data");
    }

    [Fact]
    public async Task Upload_Creates_Timeline_Event_AttachmentAdded()
    {
        var c = ClientForTenant("tenant-UP-4");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("d"), "d.txt", "text/plain"));
        resp.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var events = await db.TicketEvents.AsNoTracking()
            .Where(e => e.TicketId == t.Id && e.TenantId == "tenant-UP-4")
            .ToListAsync();
        events.Should().Contain(e => e.EventType == "attachment_added");
    }

    [Fact]
    public async Task Upload_Honors_DisplayName_Override()
    {
        var c = ClientForTenant("tenant-UP-5");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("hi"), "raw.pdf", "application/pdf",
                displayName: "Pretty Name.pdf"));
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<TicketAttachmentResponse>();
        body!.FileName.Should().Be("Pretty Name.pdf");
    }

    // ------------------------------------------------------------------
    //  Validation
    // ------------------------------------------------------------------
    [Fact]
    public async Task Upload_Rejects_Missing_File_Part()
    {
        var c = ClientForTenant("tenant-UP-6");
        var t = await CreateTicketAsync(c);

        var content = new MultipartFormDataContent();
        content.Add(new StringContent("nope"), "display_name");

        var resp = await c.PostAsync($"/support/api/tickets/{t.Id}/attachments/upload", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Rejects_Zero_Byte_File()
    {
        var c = ClientForTenant("tenant-UP-7");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Array.Empty<byte>(), "empty.pdf", "application/pdf"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Rejects_Disallowed_Content_Type()
    {
        var c = ClientForTenant("tenant-UP-8");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("malware"), "evil.exe",
                "application/x-msdownload"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Rejects_File_Exceeding_Max_Size()
    {
        var c = ClientForTenant("tenant-UP-9");
        var t = await CreateTicketAsync(c);

        // Factory configures MaxFileSizeMb=1 → 1.5 MB exceeds.
        var bytes = new byte[(int)(1.5 * 1024 * 1024)];
        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(bytes, "big.pdf", "application/pdf"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Rejects_Unsafe_Display_Name()
    {
        var c = ClientForTenant("tenant-UP-10");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("x"), "ok.pdf", "application/pdf",
                displayName: "../../etc/passwd"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    //  Tenant isolation
    // ------------------------------------------------------------------
    [Fact]
    public async Task Upload_Returns_404_For_Wrong_Tenant()
    {
        var owner = ClientForTenant("tenant-UP-OWN");
        var t = await CreateTicketAsync(owner);

        var stranger = ClientForTenant("tenant-UP-STR");
        var resp = await stranger.PostAsync(
            $"/support/api/tickets/{t.Id}/attachments/upload",
            BuildUpload(Encoding.UTF8.GetBytes("x"), "x.pdf", "application/pdf"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------
    //  Storage-provider failure modes
    // ------------------------------------------------------------------
    [Fact]
    public async Task Upload_Returns_502_When_Storage_Provider_Fails_Remotely()
    {
        var c = ClientForTenant("tenant-UP-11");
        var t = await CreateTicketAsync(c);

        _factory.Recorder.ThrowRemote = true;
        try
        {
            var resp = await c.PostAsync(
                $"/support/api/tickets/{t.Id}/attachments/upload",
                BuildUpload(Encoding.UTF8.GetBytes("x"), "x.pdf", "application/pdf"));
            resp.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        }
        finally { _factory.Recorder.ThrowRemote = false; }

        // No attachment row should exist for this ticket.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
        var any = await db.TicketAttachments.AnyAsync(a => a.TicketId == t.Id);
        any.Should().BeFalse();
    }

    [Fact]
    public async Task Upload_Returns_503_When_Storage_Provider_Not_Configured()
    {
        var c = ClientForTenant("tenant-UP-12");
        var t = await CreateTicketAsync(c);

        _factory.Recorder.ThrowNotConfigured = true;
        try
        {
            var resp = await c.PostAsync(
                $"/support/api/tickets/{t.Id}/attachments/upload",
                BuildUpload(Encoding.UTF8.GetBytes("x"), "x.pdf", "application/pdf"));
            resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }
        finally { _factory.Recorder.ThrowNotConfigured = false; }
    }

    // ------------------------------------------------------------------
    //  Existing link endpoint still works
    // ------------------------------------------------------------------
    [Fact]
    public async Task Existing_Link_Endpoint_Still_Works_Alongside_Upload()
    {
        var c = ClientForTenant("tenant-UP-13");
        var t = await CreateTicketAsync(c);

        var resp = await c.PostAsJsonAsync($"/support/api/tickets/{t.Id}/attachments",
            new CreateTicketAttachmentRequest
            {
                DocumentId = "doc_link_only",
                FileName = "linked.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 100,
            });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

/// <summary>End-to-end test of the real Local provider against a temp dir.</summary>
public class LocalFileProviderTests : IClassFixture<LocalProviderApiFactory>
{
    private readonly LocalProviderApiFactory _factory;

    public LocalFileProviderTests(LocalProviderApiFactory factory) => _factory = factory;

    private HttpClient ClientForTenant(string tenantId, string sub = "test-user")
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        c.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        return c;
    }

    [Fact]
    public async Task LocalProvider_Writes_File_To_Disk_And_Persists_Reference()
    {
        var c = ClientForTenant("tenant-LOCAL-1");
        var ticketResp = await c.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = "Local upload",
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
        });
        ticketResp.EnsureSuccessStatusCode();
        var ticket = (await ticketResp.Content.ReadFromJsonAsync<TicketResponse>())!;

        var bytes = Encoding.UTF8.GetBytes("PDF\n%%EOF\n");
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "report.pdf");

        var resp = await c.PostAsync(
            $"/support/api/tickets/{ticket.Id}/attachments/upload", content);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await resp.Content.ReadFromJsonAsync<TicketAttachmentResponse>())!;

        body.DocumentId.Should().StartWith("local-");
        body.FileSizeBytes.Should().Be(bytes.Length);
        body.FileName.Should().Be("report.pdf");

        // File is on disk under {root}/{tenant}/{ticket}/{docId}/{name}
        var expectedDir = Path.Combine(
            _factory.LocalRoot,
            "tenant-LOCAL-1",
            ticket.Id.ToString("N"),
            body.DocumentId);
        Directory.Exists(expectedDir).Should().BeTrue();
        File.Exists(Path.Combine(expectedDir, "report.pdf")).Should().BeTrue();
        new FileInfo(Path.Combine(expectedDir, "report.pdf")).Length.Should().Be(bytes.Length);

        // The API response MUST NOT leak the local filesystem path.
        var rawJson = await resp.Content.ReadAsStringAsync();
        rawJson.Should().NotContain(_factory.LocalRoot);
    }
}
