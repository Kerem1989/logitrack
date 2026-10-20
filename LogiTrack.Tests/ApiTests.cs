using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LogiTrack.Tests;

public class ApiTests : IClassFixture<LogiTrackFactory>
{
    private const string Password = "Long-Enough-Password-1!";
    private readonly LogiTrackFactory _factory;

    public ApiTests(LogiTrackFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_IsAnonymousAndHealthy()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/Inventory")]
    [InlineData("/api/orders")]
    public async Task Endpoints_RequireToken(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_GrantsUserRoleOnly()
    {
        var (client, _) = await NewUserClientAsync();

        var delete = await client.DeleteAsync("/api/Inventory/1");
        var promote = await client.PostAsync("/api/Auth/promote/admin", null);

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, promote.StatusCode);
    }

    [Fact]
    public async Task Register_EnforcesPasswordLength()
    {
        var name = UniqueName();
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/register",
            new { UserName = name, Email = $"{name}@test.local", Password = "Short-1!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at least 12 characters", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        var name = UniqueName();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/Auth/register", new { UserName = name, Email = $"{name}@test.local", Password });

        var duplicate = await client.PostAsJsonAsync("/api/Auth/register",
            new { UserName = name + "2", Email = $"{name}@test.local", Password });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Login_LocksAccountAfterFiveFailures()
    {
        var (_, name) = await NewUserClientAsync();
        var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/Auth/login", new { UserName = name, Password = "wrong" });
        }

        var correct = await client.PostAsJsonAsync("/api/Auth/login", new { UserName = name, Password });

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task PostInventory_RejectsNonPositiveQuantity(int quantity)
    {
        var (client, _) = await NewUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/Inventory",
            new { Name = "Widget", Quantity = quantity, Location = "A" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostInventory_IgnoresClientSuppliedIds()
    {
        var (client, _) = await NewUserClientAsync();
        var first = await PostItemAsync(client, "First");

        // Before DTOs this was inserted with the given key and failed with a 500
        var response = await client.PostAsJsonAsync("/api/Inventory",
            new { ItemId = first.GetProperty("itemId").GetInt32(), Name = "Second", Quantity = 1, Location = "A" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(first.GetProperty("itemId").GetInt32(), second.GetProperty("itemId").GetInt32());
    }

    [Fact]
    public async Task GetInventory_ReflectsWritesDespiteCaching()
    {
        var admin = await AdminClientAsync();
        var before = await TotalCountAsync(admin, "/api/Inventory");

        var item = await PostItemAsync(admin, "Cached");
        var afterPost = await TotalCountAsync(admin, "/api/Inventory");
        await admin.DeleteAsync($"/api/Inventory/{item.GetProperty("itemId").GetInt32()}");
        var afterDelete = await TotalCountAsync(admin, "/api/Inventory");

        Assert.Equal(before + 1, afterPost);
        Assert.Equal(before, afterDelete);
    }

    [Fact]
    public async Task GetInventory_PagesResults()
    {
        var (client, _) = await NewUserClientAsync();
        for (var i = 0; i < 3; i++)
        {
            await PostItemAsync(client, $"Paged{i}");
        }

        var page = await client.GetAsync("/api/Inventory?page=1&pageSize=2");
        var tooLarge = await client.GetAsync("/api/Inventory?pageSize=101");

        Assert.Equal(2, (await page.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());
        Assert.True(int.Parse(page.Headers.GetValues("X-Total-Count").Single()) >= 3);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }

    [Fact]
    public async Task Orders_AreVisibleOnlyToOwnerAndAdmins()
    {
        var (owner, ownerName) = await NewUserClientAsync();
        var (other, _) = await NewUserClientAsync();
        var admin = await AdminClientAsync();

        var created = await (await owner.PostAsJsonAsync("/api/orders",
            new[] { new { Name = "OrderItem", Quantity = 2, Location = "B" } })).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("orderId").GetInt32();

        Assert.Equal(ownerName, created.GetProperty("customerName").GetString());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/orders/{id}")).StatusCode);
        Assert.Equal(0, await TotalCountAsync(other, "/api/orders"));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/orders/{id}")).StatusCode);
    }

    [Fact]
    public async Task PostOrder_RejectsEmptyOrder()
    {
        var (client, _) = await NewUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/orders", Array.Empty<object>());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_RemovesItsItemsFromCachedInventory()
    {
        var admin = await AdminClientAsync();
        var before = await TotalCountAsync(admin, "/api/Inventory");
        var created = await (await admin.PostAsJsonAsync("/api/orders",
            new[] { new { Name = "A", Quantity = 1, Location = "X" }, new { Name = "B", Quantity = 1, Location = "X" } }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(before + 2, await TotalCountAsync(admin, "/api/Inventory"));

        await admin.DeleteAsync($"/api/orders/{created.GetProperty("orderId").GetInt32()}");

        Assert.Equal(before, await TotalCountAsync(admin, "/api/Inventory"));
    }

    private static string UniqueName() => "user" + Guid.NewGuid().ToString("N")[..12];

    private async Task<(HttpClient Client, string UserName)> NewUserClientAsync()
    {
        var name = UniqueName();
        var register = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/register",
            new { UserName = name, Email = $"{name}@test.local", Password });
        register.EnsureSuccessStatusCode();
        return (await LoginAsync(name, Password), name);
    }

    private Task<HttpClient> AdminClientAsync() =>
        LoginAsync(LogiTrackFactory.AdminUserName, LogiTrackFactory.AdminPassword);

    private async Task<HttpClient> LoginAsync(string userName, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { UserName = userName, Password = password });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> PostItemAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/Inventory", new { Name = name, Quantity = 1, Location = "A" });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<int> TotalCountAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return int.Parse(response.Headers.GetValues("X-Total-Count").Single());
    }
}
