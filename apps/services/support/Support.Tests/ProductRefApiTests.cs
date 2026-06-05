using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Support.Api.Domain;
using Support.Api.Dtos;

namespace Support.Tests;

public class ProductRefApiTests : IClassFixture<SupportApiFactory>
{
    private readonly SupportApiFactory _factory;

    public ProductRefApiTests(SupportApiFactory factory) => _factory = factory;

    private HttpClient ClientForTenant(string tenantId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return c;
    }

    private async Task<TicketResponse> CreateTicketAsync(HttpClient c, string title = "T")
    {
        var resp = await c.PostAsJsonAsync("/support/api/tickets", new CreateTicketRequest
        {
            Title = title,
            Priority = TicketPriority.Normal,
            Source = TicketSource.Portal,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TicketResponse>())!;
    }

    [Fact]
    public async Task Add_Product_Reference_Succeeds()
    {
        var a = ClientForTenant("tenant-PR-A");
        var t = await CreateTicketAsync(a);

        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs", new CreateProductReferenceRequest
        {
            ProductCode = "LIENS",
            EntityType = "lien",
            EntityId = "lien-123",
            DisplayLabel = "Smith v. Doe",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<ProductReferenceResponse>();
        body!.ProductCode.Should().Be("LIENS");
        body.EntityType.Should().Be("lien");
        body.EntityId.Should().Be("lien-123");
        body.DisplayLabel.Should().Be("Smith v. Doe");
    }

    [Fact]
    public async Task Add_Product_Reference_Normalizes_Product_Code_To_Uppercase()
    {
        var a = ClientForTenant("tenant-PR-B");
        var t = await CreateTicketAsync(a);

        var resp = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs", new CreateProductReferenceRequest
        {
            ProductCode = "fund",
            EntityType = "case",
            EntityId = "c-9",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await resp.Content.ReadFromJsonAsync<ProductReferenceResponse>();
        body!.ProductCode.Should().Be("FUND");
    }

    [Fact]
    public async Task Add_Product_Reference_Rejects_Duplicate()
    {
        var a = ClientForTenant("tenant-PR-C");
        var t = await CreateTicketAsync(a);

        var req = new CreateProductReferenceRequest
        {
            ProductCode = "PLATFORM",
            EntityType = "user",
            EntityId = "u-42",
        };
        var first = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs", req);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs", req);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var dupCaseInsensitive = await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs",
            new CreateProductReferenceRequest { ProductCode = "platform", EntityType = "user", EntityId = "u-42" });
        dupCaseInsensitive.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task List_Product_References_Supports_Product_Code_Filter()
    {
        var a = ClientForTenant("tenant-PR-D");
        var t = await CreateTicketAsync(a);

        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs",
            new CreateProductReferenceRequest { ProductCode = "LIENS", EntityType = "lien", EntityId = "1" });
        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs",
            new CreateProductReferenceRequest { ProductCode = "FUND", EntityType = "case", EntityId = "c1" });
        await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs",
            new CreateProductReferenceRequest { ProductCode = "FUND", EntityType = "case", EntityId = "c2" });

        var list = await (await a.GetAsync($"/support/api/tickets/{t.Id}/product-refs?product_code=fund"))
            .Content.ReadFromJsonAsync<List<ProductReferenceResponse>>();
        list.Should().HaveCount(2);
        list!.Should().OnlyContain(r => r.ProductCode == "FUND");
    }

    [Fact]
    public async Task Delete_Product_Reference_Succeeds()
    {
        var a = ClientForTenant("tenant-PR-E");
        var t = await CreateTicketAsync(a);

        var created = await (await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs",
                new CreateProductReferenceRequest { ProductCode = "DOCUMENTS", EntityType = "doc", EntityId = "d1" }))
            .Content.ReadFromJsonAsync<ProductReferenceResponse>();

        var del = await a.DeleteAsync($"/support/api/tickets/{t.Id}/product-refs/{created!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await (await a.GetAsync($"/support/api/tickets/{t.Id}/product-refs"))
            .Content.ReadFromJsonAsync<List<ProductReferenceResponse>>();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Product_Reference_Is_Tenant_Scoped()
    {
        var a = ClientForTenant("tenant-PR-F1");
        var b = ClientForTenant("tenant-PR-F2");
        var t = await CreateTicketAsync(a);
        var created = await (await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs",
                new CreateProductReferenceRequest { ProductCode = "TASK", EntityType = "task", EntityId = "tk-1" }))
            .Content.ReadFromJsonAsync<ProductReferenceResponse>();

        var del = await b.DeleteAsync($"/support/api/tickets/{t.Id}/product-refs/{created!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // confirm still present for original tenant
        var list = await (await a.GetAsync($"/support/api/tickets/{t.Id}/product-refs"))
            .Content.ReadFromJsonAsync<List<ProductReferenceResponse>>();
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task Product_Reference_Create_And_Delete_Create_Timeline_Events()
    {
        var a = ClientForTenant("tenant-PR-G");
        var t = await CreateTicketAsync(a);

        var created = await (await a.PostAsJsonAsync($"/support/api/tickets/{t.Id}/product-refs",
                new CreateProductReferenceRequest { ProductCode = "CARECONNECT", EntityType = "patient", EntityId = "p-7", DisplayLabel = "Jane Doe" }))
            .Content.ReadFromJsonAsync<ProductReferenceResponse>();

        await a.DeleteAsync($"/support/api/tickets/{t.Id}/product-refs/{created!.Id}");

        var timeline = await (await a.GetAsync($"/support/api/tickets/{t.Id}/timeline"))
            .Content.ReadFromJsonAsync<List<TimelineItem>>();

        var linked = timeline!.SingleOrDefault(i => i.EventType == "product_ref_linked");
        linked.Should().NotBeNull();
        linked!.MetadataJson.Should().Contain("\"product_code\":\"CARECONNECT\"")
            .And.Contain("\"entity_id\":\"p-7\"")
            .And.Contain("\"display_label\":\"Jane Doe\"");

        var removed = timeline.SingleOrDefault(i => i.EventType == "product_ref_removed");
        removed.Should().NotBeNull();
        removed!.MetadataJson.Should().Contain("\"product_code\":\"CARECONNECT\"")
            .And.Contain("\"entity_id\":\"p-7\"");
    }
}
