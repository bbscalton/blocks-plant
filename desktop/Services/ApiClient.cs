using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BlocksPlant.Desktop.Models;

namespace BlocksPlant.Desktop.Services;

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

    public ApiClient(AppConfig config)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.ApiBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void SetToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new { username, password }, JsonOptions);
        return await ReadAsync<LoginResponse>(response);
    }

    public Task<List<ProductDto>> GetProductsAsync(bool includeInactive = false) =>
        GetAsync<List<ProductDto>>($"api/products?includeInactive={includeInactive.ToString().ToLowerInvariant()}");

    public async Task UpdateProductAsync(int id, decimal pricePerBlock, int minStock)
    {
        var response = await _http.PutAsJsonAsync($"api/products/{id}", new { pricePerBlock, minStock }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task<ProductDto> CreateProductAsync(string name, int sizeInches, decimal pricePerBlock, int minStock, int quantity = 0)
    {
        var response = await _http.PostAsJsonAsync("api/products", new { name, sizeInches, pricePerBlock, minStock, quantity }, JsonOptions);
        return await ReadAsync<ProductDto>(response);
    }

    public async Task<DeleteProductResult> DeleteProductAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        return await ReadAsync<DeleteProductResult>(response);
    }

    public async Task ActivateProductAsync(int id)
    {
        var response = await _http.PostAsync($"api/products/{id}/activate", null);
        await EnsureSuccessAsync(response);
    }

    public Task<PlantSettingsDto> GetSettingsAsync() => GetAsync<PlantSettingsDto>("api/settings");

    public async Task<PlantSettingsDto> UpdateSettingsAsync(object body)
    {
        var response = await _http.PutAsJsonAsync("api/settings", body, JsonOptions);
        return await ReadAsync<PlantSettingsDto>(response);
    }

    public async Task<PlantSettingsDto> UploadLogoAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));
        var response = await _http.PostAsync("api/settings/logo", content);
        return await ReadAsync<PlantSettingsDto>(response);
    }

    public async Task ClearLogoAsync()
    {
        var response = await _http.DeleteAsync("api/settings/logo");
        await EnsureSuccessAsync(response);
    }

    public async Task<byte[]?> DownloadLogoBytesAsync()
    {
        var response = await _http.GetAsync("api/settings/logo");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync();
    }

    public Task<List<UserDto>> GetUsersAsync() => GetAsync<List<UserDto>>("api/users");

    public async Task<UserDto> CreateUserAsync(string username, string password, string fullName, string role)
    {
        var response = await _http.PostAsJsonAsync("api/users", new { username, password, fullName, role }, JsonOptions);
        return await ReadAsync<UserDto>(response);
    }

    public async Task UpdateUserAsync(int id, string fullName, string role, bool isActive)
    {
        var response = await _http.PutAsJsonAsync($"api/users/{id}", new { fullName, role, isActive }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task ResetUserPasswordAsync(int id, string newPassword)
    {
        var response = await _http.PostAsJsonAsync($"api/users/{id}/reset-password", new { newPassword }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeactivateUserAsync(int id)
    {
        var response = await _http.PostAsync($"api/users/{id}/deactivate", null);
        await EnsureSuccessAsync(response);
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var response = await _http.PostAsJsonAsync("api/auth/change-password", new { currentPassword, newPassword }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DownloadBackupAsync(string savePath)
    {
        var response = await _http.GetAsync("api/backup");
        await EnsureSuccessAsync(response);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(savePath, bytes);
    }

    public async Task RestoreBackupAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));
        var response = await _http.PostAsync("api/backup/restore", content);
        await EnsureSuccessAsync(response);
    }

    public Task<List<StockDto>> GetStockAsync() => GetAsync<List<StockDto>>("api/stock");

    public async Task AdjustStockAsync(int productId, int quantityDelta, string? reason = null)
    {
        var response = await _http.PostAsJsonAsync("api/stock/adjust", new { productId, quantityDelta, reason }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task<ProductionDto> CreateProductionAsync(int productId, int quantity, int rejects)
    {
        var response = await _http.PostAsJsonAsync("api/production", new { productId, quantity, rejects }, JsonOptions);
        return await ReadAsync<ProductionDto>(response);
    }

    public Task<List<ProductionDto>> GetProductionAsync() => GetAsync<List<ProductionDto>>("api/production");

    public Task<List<CustomerDto>> GetCustomersAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search)
            ? "api/customers"
            : $"api/customers?search={Uri.EscapeDataString(search)}";
        return GetAsync<List<CustomerDto>>(url);
    }

    public Task<List<CustomerDto>> GetDebtorsAsync() => GetAsync<List<CustomerDto>>("api/customers/debtors");

    public async Task<CustomerDto> CreateCustomerAsync(string name, string? phone, string? address)
    {
        var response = await _http.PostAsJsonAsync("api/customers", new { name, phone, address }, JsonOptions);
        return await ReadAsync<CustomerDto>(response);
    }

    public async Task<SaleDto> CreateSaleAsync(object body)
    {
        var response = await _http.PostAsJsonAsync("api/sales", body, JsonOptions);
        return await ReadAsync<SaleDto>(response);
    }

    public Task<List<SaleDto>> GetSalesAsync() => GetAsync<List<SaleDto>>("api/sales");

    public async Task RecordPaymentAsync(int? customerId, int? saleId, decimal amount, string method = "Cash")
    {
        var response = await _http.PostAsJsonAsync("api/payments", new { customerId, saleId, amount, method }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public Task<List<DeliveryDto>> GetDeliveriesAsync() => GetAsync<List<DeliveryDto>>("api/deliveries");

    public async Task UpdateDeliveryStatusAsync(int saleId, string status)
    {
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
        var response = await _http.PostAsJsonAsync("api/materials", new { name, unit, quantityOnHand, minStock, notes }, JsonOptions);
        return await ReadAsync<RawMaterialDto>(response);
    }

    public async Task UpdateMaterialAsync(int id, string name, string unit, decimal minStock, string? notes, bool isActive = true)
    {
        var response = await _http.PutAsJsonAsync($"api/materials/{id}", new { name, unit, minStock, notes, isActive }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task ReceiveMaterialAsync(int id, decimal quantity, string? notes = null)
    {
        var response = await _http.PostAsJsonAsync($"api/materials/{id}/receive", new { quantity, notes }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task AdjustMaterialAsync(int id, decimal quantityDelta, string? notes = null)
    {
        var response = await _http.PostAsJsonAsync($"api/materials/{id}/adjust", new { quantityDelta, notes }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public Task<List<ProductRecipeDto>> GetRecipesAsync() => GetAsync<List<ProductRecipeDto>>("api/recipes");

    public Task<ProductRecipeDto> GetRecipeAsync(int productId) =>
        GetAsync<ProductRecipeDto>($"api/recipes/by-product/{productId}");

    public async Task<ProductRecipeDto> SetRecipeAsync(int productId, IEnumerable<object> lines)
    {
        var response = await _http.PutAsJsonAsync($"api/recipes/by-product/{productId}", new { lines }, JsonOptions);
        return await ReadAsync<ProductRecipeDto>(response);
    }

    private async Task<T> GetAsync<T>(string url)
    {
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

public static class Session
{
    public static LoginResponse? CurrentUser { get; set; }
    public static ApiClient? Api { get; set; }
    public static AppConfig Config { get; set; } = AppConfig.Load();
}
