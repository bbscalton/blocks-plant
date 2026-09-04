namespace BlocksPlant.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SizeInches { get; set; }
    public decimal PricePerBlock { get; set; }
    public int MinStock { get; set; }
    public int Quantity { get; set; }
}

public class ProductionEntry
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public int Rejects { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    /// <summary>Optional client-generated id for offline sync idempotency.</summary>
    public Guid? ClientId { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal Balance { get; set; }
}

public class Sale
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int CashierUserId { get; set; }
    public User? CashierUser { get; set; }
    public FulfillmentType FulfillmentType { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryNotes { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
    public decimal Subtotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid ClientId { get; set; }
    public List<SaleLine> Lines { get; set; } = new();
}

public class SaleLine
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}

public class RawMaterial
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal MinStock { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RecipeLine
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    /// <summary>Amount of material consumed per good block produced.</summary>
    public decimal QuantityPerBlock { get; set; }
}

public class MaterialTransaction
{
    public int Id { get; set; }
    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }
    public MaterialTxnType Type { get; set; }
    public decimal QuantityDelta { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? Notes { get; set; }
    public int? ProductionEntryId { get; set; }
    public ProductionEntry? ProductionEntry { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
