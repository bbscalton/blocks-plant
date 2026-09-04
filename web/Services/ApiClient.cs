using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BlocksPlant.Web.Models;
using Microsoft.Extensions.Options;

namespace BlocksPlant.Web.Services;

public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}

public class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly AuthService _auth;

    public ApiClient(HttpClient http, AuthService auth, IOptions<ApiOptions> options)
    {
        _http = http;
        _auth = auth;
        var baseUrl = options.Value.BaseUrl.TrimEnd('/') + "/";
        if (_http.BaseAddress is null || _http.BaseAddress.ToString() != baseUrl)
            _http.BaseAddress = new Uri(baseUrl);
    }

    private void ApplyAuth()
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(_auth.Token)
            ? null
            : new AuthenticationHeaderValue("Bearer", _auth.Token);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { username, password }, JsonOptions);
        return await ReadAsync<LoginResponse>(response);
    }

    public Task<List<ProductDto>> GetProductsAsync() => GetAsync<List<ProductDto>>("api/products");

    public async Task UpdateProductAsync(int id, decimal pricePerBlock, int minStock)
    {
        ApplyAuth();
        var response = await _http.PutAsJsonAsync($"api/products/{id}", new { pricePerBlock, minStock }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public Task<List<StockDto>> GetStockAsync() => GetAsync<List<StockDto>>("api/stock");

    public async Task AdjustStockAsync(int productId, int quantityDelta, string? reason = null)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync("api/stock/adjust", new { productId, quantityDelta, reason }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public Task<List<ProductionDto>> GetProductionAsync(int take = 50) =>
        GetAsync<List<ProductionDto>>($"api/production?take={take}");

    public Task<List<CustomerDto>> GetCustomersAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search)
            ? "api/customers"
            : $"api/customers?search={Uri.EscapeDataString(search)}";
        return GetAsync<List<CustomerDto>>(url);
    }

    public Task<List<CustomerDto>> GetDebtorsAsync() => GetAsync<List<CustomerDto>>("api/customers/debtors");

    public Task<List<SaleDto>> GetSalesAsync(int take = 100) => GetAsync<List<SaleDto>>($"api/sales?take={take}");

    public async Task RecordPaymentAsync(int? customerId, int? saleId, decimal amount, string method = "Cash")
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync("api/payments", new { customerId, saleId, amount, method }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public Task<List<DeliveryDto>> GetDeliveriesAsync(bool includeDelivered = false) =>
        GetAsync<List<DeliveryDto>>($"api/deliveries?includeDelivered={includeDelivered.ToString().ToLowerInvariant()}");

    public async Task UpdateDeliveryStatusAsync(int saleId, string status)
    {
        ApplyAuth();
        var response = await _http.PatchAsync(
            $"api/deliveries/{saleId}",
            new StringContent(JsonSerializer.Serialize(new { status }, JsonOptions), Encoding.UTF8, "application/json"));
        await EnsureSuccessAsync(response);
    }

    public Task<DashboardDto> GetDashboardAsync() => GetAsync<DashboardDto>("api/dashboard");

    public Task<List<RawMaterialDto>> GetMaterialsAsync(bool includeInactive = false) =>
        GetAsync<List<RawMaterialDto>>($"api/materials?includeInactive={includeInactive.ToString().ToLowerInvariant()}");

    public async Task<RawMaterialDto> CreateMaterialAsync(string name, string unit, decimal quantityOnHand, decimal minStock, string? notes)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync("api/materials", new { name, unit, quantityOnHand, minStock, notes }, JsonOptions);
        return await ReadAsync<RawMaterialDto>(response);
    }

    public async Task UpdateMaterialAsync(int id, string name, string unit, decimal minStock, string? notes, bool isActive = true)
    {
        ApplyAuth();
        var response = await _http.PutAsJsonAsync($"api/materials/{id}", new { name, unit, minStock, notes, isActive }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task ReceiveMaterialAsync(int id, decimal quantity, string? notes = null)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync($"api/materials/{id}/receive", new { quantity, notes }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task AdjustMaterialAsync(int id, decimal quantityDelta, string? notes = null)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync($"api/materials/{id}/adjust", new { quantityDelta, notes }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public Task<List<ProductRecipeDto>> GetRecipesAsync() => GetAsync<List<ProductRecipeDto>>("api/recipes");

    public async Task<ProductRecipeDto> SetRecipeAsync(int productId, IEnumerable<object> lines)
    {
        ApplyAuth();
        var response = await _http.PutAsJsonAsync($"api/recipes/by-product/{productId}", new { lines }, JsonOptions);
        return await ReadAsync<ProductRecipeDto>(response);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        ApplyAuth();
        var response = await _http.GetAsync(url);
        return await ReadAsync<T>(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new ApiException(await ReadErrorAsync(response));
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ReadErrorAsync(response));

        var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        if (data is null) throw new ApiException("Empty response from server.");
        return data;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var err = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
            if (!string.IsNullOrWhiteSpace(err?.Message))
                return err.Message!;
        }
        catch
        {
            // fall through
        }

        var raw = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(raw)
            ? $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase})"
            : raw;
    }
}
