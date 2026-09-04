namespace BlocksPlant.Web.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int UserId { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SizeInches { get; set; }
    public decimal? PricePerBlock { get; set; }
    public int MinStock { get; set; }
    public int Quantity { get; set; }
}

public class StockDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int SizeInches { get; set; }
    public int Quantity { get; set; }
    public int MinStock { get; set; }
    public bool IsLow { get; set; }
}

public class ProductionDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Rejects { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal Balance { get; set; }
}

public class SaleLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class SaleDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int CashierUserId { get; set; }
    public string CashierName { get; set; } = string.Empty;
    public string FulfillmentType { get; set; } = string.Empty;
    public string? DeliveryAddress { get; set; }
    public string? DeliveryNotes { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ClientId { get; set; }
    public List<SaleLineDto> Lines { get; set; } = new();
}

public class DeliveryDto
{
    public int SaleId { get; set; }
    public string? CustomerName { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryNotes { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DashboardDto
{
    public List<StockDto> Stock { get; set; } = new();
    public int TodayProductionQty { get; set; }
    public decimal TodaySalesTotal { get; set; }
    public int TodaySalesCount { get; set; }
    public decimal TotalUnpaid { get; set; }
    public int DebtorCount { get; set; }
    public List<RawMaterialDto> Materials { get; set; } = new();
}

public class RawMaterialDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal MinStock { get; set; }
    public string? Notes { get; set; }
    public bool IsLow { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RecipeLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int RawMaterialId { get; set; }
    public string RawMaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityPerBlock { get; set; }
}

public class ProductRecipeDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int SizeInches { get; set; }
    public List<RecipeLineDto> Lines { get; set; } = new();
}

public class ApiError
{
    public string? Message { get; set; }
}

public static class Roles
{
    public const string Owner = "Owner";
    public const string Cashier = "Cashier";
    public const string Operator = "Operator";
}
