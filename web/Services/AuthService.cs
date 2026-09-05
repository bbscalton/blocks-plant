using BlocksPlant.Web.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace BlocksPlant.Web.Services;

public class AuthService
{
    private const string StorageKey = "blocksplant.auth";
    private readonly ProtectedSessionStorage _storage;
    private LoginResponse? _user;

    public AuthService(ProtectedSessionStorage storage) => _storage = storage;

    public LoginResponse? User => _user;
    public bool IsAuthenticated => _user is not null && !string.IsNullOrWhiteSpace(_user.Token);
    public string? Token => _user?.Token;
    public string Role => _user?.Role ?? string.Empty;
    public bool IsOwner => string.Equals(Role, Roles.Owner, StringComparison.OrdinalIgnoreCase);
    public bool IsCashier => string.Equals(Role, Roles.Cashier, StringComparison.OrdinalIgnoreCase);
    public bool IsOperator => string.Equals(Role, Roles.Operator, StringComparison.OrdinalIgnoreCase);
    public bool CanSeeMoney => IsOwner || IsCashier;
    public bool CanManageStock => IsOwner;
    public bool CanEditProducts => IsOwner;
    public bool CanSeeDashboard => IsOwner;
    public bool CanSeeProductionHistory => IsOwner;
    public bool CanManageMaterials => IsOwner;
    public bool CanEditRecipes => IsOwner;
    public bool CanViewMaterials => IsAuthenticated;
    public bool CanManageUsers => IsOwner;
    public bool CanManageSettings => IsOwner;
    public bool CanBackup => IsOwner;

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        try
        {
            var result = await _storage.GetAsync<LoginResponse>(StorageKey);
            if (result.Success && result.Value is not null && !string.IsNullOrWhiteSpace(result.Value.Token))
                _user = result.Value;
        }
        catch
        {
            _user = null;
        }
    }

    public async Task SetUserAsync(LoginResponse user)
    {
        _user = user;
        await _storage.SetAsync(StorageKey, user);
        Changed?.Invoke();
    }

    public async Task LogoutAsync()
    {
        _user = null;
        try
        {
            await _storage.DeleteAsync(StorageKey);
        }
        catch
        {
            // ignore storage errors on logout
        }

        Changed?.Invoke();
    }

    public bool HasAnyRole(params string[] roles) =>
        roles.Any(r => string.Equals(Role, r, StringComparison.OrdinalIgnoreCase));
}
