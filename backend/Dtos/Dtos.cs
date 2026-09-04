using System.ComponentModel.DataAnnotations;
using BlocksPlant.Api.Models;

namespace BlocksPlant.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, string Username, string FullName, string Role, int UserId);

public record ProductDto(int Id, string Name, int SizeInches, decimal? PricePerBlock, int MinStock, int Quantity);

public record UpdateProductRequest(decimal PricePerBlock, int MinStock);

public record StockDto(int ProductId, string ProductName, int SizeInches, int Quantity, int MinStock, bool IsLow);

public record StockAdjustRequest(int ProductId, int QuantityDelta, string? Reason);

public record ProductionRequest(int ProductId, int Quantity, int Rejects = 0, Guid? ClientId = null);

public record ProductionDto(
    int Id,
    int ProductId,
    string ProductName,
    int Quantity,
    int Rejects,
    DateTime CreatedAt,
    string CreatedBy,
    Guid? ClientId = null);

public record CustomerDto(int Id, string Name, string? Phone, string? Address, decimal Balance);

public record CreateCustomerRequest(
    [Required] string Name,
    string? Phone,
    string? Address);

public record SaleLineRequest(int ProductId, int Quantity);

public record CreateSaleRequest(
    Guid ClientId,
    int? CustomerId,
    FulfillmentType FulfillmentType,
    string? DeliveryAddress,
    string? DeliveryNotes,
    decimal AmountPaid,
    List<SaleLineRequest> Lines);

public record SaleLineDto(
    int Id,
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record SaleDto(
    int Id,
    int? CustomerId,
    string? CustomerName,
    int CashierUserId,
    string CashierName,
    FulfillmentType FulfillmentType,
    string? DeliveryAddress,
    string? DeliveryNotes,
    DeliveryStatus DeliveryStatus,
    decimal Subtotal,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTime CreatedAt,
    Guid ClientId,
    List<SaleLineDto> Lines);

public record PaymentRequest(
    int? CustomerId,
    int? SaleId,
    [Range(0.01, double.MaxValue)] decimal Amount,
    PaymentMethod Method = PaymentMethod.Cash);

public record PaymentDto(
    int Id,
    int? CustomerId,
    int? SaleId,
    decimal Amount,
    PaymentMethod Method,
    DateTime CreatedAt,
    string CreatedBy);

public record DeliveryDto(
    int SaleId,
    string? CustomerName,
    string? DeliveryAddress,
    string? DeliveryNotes,
    DeliveryStatus DeliveryStatus,
    decimal Subtotal,
    DateTime CreatedAt);

public record UpdateDeliveryStatusRequest(DeliveryStatus Status);

public record RawMaterialDto(
    int Id,
    string Name,
    string Unit,
    decimal QuantityOnHand,
    decimal MinStock,
    string? Notes,
    bool IsLow,
    bool IsActive);

public record CreateRawMaterialRequest(
    [Required] string Name,
    [Required] string Unit,
    [Range(0, double.MaxValue)] decimal QuantityOnHand,
    [Range(0, double.MaxValue)] decimal MinStock,
    string? Notes);

public record UpdateRawMaterialRequest(
    [Required] string Name,
    [Required] string Unit,
    [Range(0, double.MaxValue)] decimal MinStock,
    string? Notes,
    bool IsActive = true);

public record MaterialReceiveRequest(
    [Range(0.0001, double.MaxValue)] decimal Quantity,
    string? Notes);

public record MaterialAdjustRequest(
    decimal QuantityDelta,
    string? Notes);

public record RecipeLineDto(
    int Id,
    int ProductId,
    int RawMaterialId,
    string RawMaterialName,
    string Unit,
    decimal QuantityPerBlock);

public record ProductRecipeDto(
    int ProductId,
    string ProductName,
    int SizeInches,
    List<RecipeLineDto> Lines);

public record UpsertRecipeLineRequest(
    int RawMaterialId,
    [Range(0, double.MaxValue)] decimal QuantityPerBlock);

public record SetProductRecipeRequest(List<UpsertRecipeLineRequest> Lines);

public record DashboardDto(
    List<StockDto> Stock,
    int TodayProductionQty,
    decimal TodaySalesTotal,
    int TodaySalesCount,
    decimal TotalUnpaid,
    int DebtorCount,
    List<RawMaterialDto> Materials);
