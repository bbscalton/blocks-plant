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

    public Task<List<ProductDto>> GetProductsAsync(bool includeInactive = false) =>
        GetAsync<List<ProductDto>>($"api/products?includeInactive={includeInactive.ToString().ToLowerInvariant()}");

    public async Task UpdateProductAsync(int id, decimal pricePerBlock, int minStock)
    {
        ApplyAuth();
        var response = await _http.PutAsJsonAsync($"api/products/{id}", new { pricePerBlock, minStock }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task<ProductDto> CreateProductAsync(string name, int sizeInches, decimal pricePerBlock, int minStock, int quantity = 0)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync("api/products", new { name, sizeInches, pricePerBlock, minStock, quantity }, JsonOptions);
        return await ReadAsync<ProductDto>(response);
    }

    public async Task<DeleteProductResult> DeleteProductAsync(int id)
    {
        ApplyAuth();
        var response = await _http.DeleteAsync($"api/products/{id}");
        return await ReadAsync<DeleteProductResult>(response);
    }

    public async Task ActivateProductAsync(int id)
    {
        ApplyAuth();
        var response = await _http.PostAsync($"api/products/{id}/activate", null);
        await EnsureSuccessAsync(response);
    }

    public Task<PlantSettingsDto> GetSettingsAsync() => GetAsync<PlantSettingsDto>("api/settings");

    public async Task<PlantSettingsDto> UpdateSettingsAsync(PlantSettingsDto s)
    {
        ApplyAuth();
        var body = new
        {
            businessName = s.BusinessName,
            address = s.Address,
            phone = s.Phone,
            email = s.Email,
            taxId = s.TaxId,
            receiptFooter = s.ReceiptFooter,
            receiptShowLogo = s.ReceiptShowLogo,
            receiptShowStoreName = s.ReceiptShowStoreName,
            receiptShowAddress = s.ReceiptShowAddress,
            receiptShowPhone = s.ReceiptShowPhone,
            receiptShowCashier = s.ReceiptShowCashier,
            receiptShowThankYou = s.ReceiptShowThankYou
        };
        var response = await _http.PutAsJsonAsync("api/settings", body, JsonOptions);
        return await ReadAsync<PlantSettingsDto>(response);
    }

    public async Task<PlantSettingsDto> UploadLogoAsync(Stream stream, string fileName)
    {
        ApplyAuth();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        var response = await _http.PostAsync("api/settings/logo", content);
        return await ReadAsync<PlantSettingsDto>(response);
    }

    public async Task ClearLogoAsync()
    {
        ApplyAuth();
        var response = await _http.DeleteAsync("api/settings/logo");
        await EnsureSuccessAsync(response);
    }

    public async Task<byte[]?> DownloadLogoBytesAsync()
    {
        ApplyAuth();
        var response = await _http.GetAsync("api/settings/logo");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<string?> GetLogoDataUrlAsync()
    {
        var bytes = await DownloadLogoBytesAsync();
        if (bytes is null || bytes.Length == 0) return null;
        // sniff common types
        var mime = bytes.Length > 3 && bytes[0] == 0x89 && bytes[1] == 0x50 ? "image/png"
            : bytes.Length > 2 && bytes[0] == 0x47 && bytes[1] == 0x49 ? "image/gif"
            : bytes.Length > 3 && bytes[0] == 0x52 && bytes[1] == 0x49 ? "image/webp"
            : "image/jpeg";
        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    public Task<List<UserDto>> GetUsersAsync() => GetAsync<List<UserDto>>("api/users");

    public async Task<UserDto> CreateUserAsync(string username, string password, string fullName, string role)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync("api/users", new { username, password, fullName, role }, JsonOptions);
        return await ReadAsync<UserDto>(response);
    }

    public async Task UpdateUserAsync(int id, string fullName, string role, bool isActive)
    {
        ApplyAuth();
        var response = await _http.PutAsJsonAsync($"api/users/{id}", new { fullName, role, isActive }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task ResetUserPasswordAsync(int id, string newPassword)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync($"api/users/{id}/reset-password", new { newPassword }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task DeactivateUserAsync(int id)
    {
        ApplyAuth();
        var response = await _http.PostAsync($"api/users/{id}/deactivate", null);
        await EnsureSuccessAsync(response);
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        ApplyAuth();
        var response = await _http.PostAsJsonAsync("api/auth/change-password", new { currentPassword, newPassword }, JsonOptions);
        await EnsureSuccessAsync(response);
    }

    public async Task<byte[]> DownloadBackupAsync()
    {
        ApplyAuth();
        var response = await _http.GetAsync("api/backup");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task RestoreBackupAsync(Stream stream, string fileName)
    {
        ApplyAuth();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        var response = await _http.PostAsync("api/backup/restore", content);
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
