using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhatsOrder.Application.Auth;
using WhatsOrder.Application.Categories;
using WhatsOrder.Application.Products;
using WhatsOrder.Application.Stores;

namespace WhatsOrder.IntegrationTests;

public abstract class ApiTestBase(TestAppFactory factory)
{
    protected TestAppFactory Factory { get; } = factory;

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected static string UniqueEmail() => $"owner-{Guid.NewGuid():N}@test.om";
    protected static string UniqueSlug() => $"store-{Guid.NewGuid():N}"[..20];

    protected async Task<(HttpClient Client, AuthResponse Auth)> RegisterOwnerAsync()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(UniqueEmail(), "Passw0rd!x", "Test Owner"), Json);
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    protected async Task<(HttpClient Client, AuthResponse Auth)> RegisterBuyerAsync(string displayName = "Test Buyer")
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"buyer-{Guid.NewGuid():N}@test.om", "Passw0rd!x", displayName, "buyer"), Json);
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    protected async Task<StoreDto> CreateStoreAsync(HttpClient client, string? slug = null)
    {
        var response = await client.PostAsJsonAsync("/api/store", new CreateStoreRequest(
            Name: "Test Store",
            NameAr: "متجر تجريبي",
            Slug: slug ?? UniqueSlug(),
            WhatsAppNumber: "91234567",
            InstagramHandle: "test.store",
            Description: "A test store",
            DescriptionAr: null,
            LocationText: "Muscat",
            Governorate: "Muscat",
            Wilayat: "Bawshar"), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StoreDto>(Json))!;
    }

    protected async Task<ProductDto> CreateProductAsync(
        HttpClient client, string name = "Cookies Box", decimal price = 3.000m,
        decimal? discountedPrice = null, int? stock = null, List<VariantDto>? variants = null,
        Guid? categoryId = null, bool isFeatured = false)
    {
        var response = await client.PostAsJsonAsync("/api/products", new SaveProductRequest(
            CategoryId: categoryId,
            Name: name,
            NameAr: null,
            Description: null,
            DescriptionAr: null,
            Price: price,
            DiscountedPrice: discountedPrice,
            StockQuantity: stock,
            IsAvailable: true,
            IsFeatured: isFeatured,
            Variants: variants), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>(Json))!;
    }

    protected async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string name = "Cakes")
    {
        var response = await client.PostAsJsonAsync("/api/categories",
            new SaveCategoryRequest(name, null, 0, true), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>(Json))!;
    }
}
