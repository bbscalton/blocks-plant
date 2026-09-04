package com.blocksplant.app.data.api

data class LoginRequest(
    val username: String,
    val password: String
)

data class LoginResponse(
    val token: String,
    val username: String,
    val fullName: String,
    val role: String,
    val userId: Int
)

data class ProductDto(
    val id: Int,
    val name: String,
    val sizeInches: Int,
    val pricePerBlock: Double?,
    val minStock: Int,
    val quantity: Int
)

data class StockDto(
    val productId: Int,
    val productName: String,
    val sizeInches: Int,
    val quantity: Int,
    val minStock: Int,
    val isLow: Boolean
)

data class ProductionRequest(
    val productId: Int,
    val quantity: Int,
    val rejects: Int = 0,
    val clientId: String? = null
)

data class ProductionDto(
    val id: Int,
    val productId: Int,
    val productName: String,
    val quantity: Int,
    val rejects: Int,
    val createdAt: String,
    val createdBy: String,
    val clientId: String? = null
)

data class CustomerDto(
    val id: Int,
    val name: String,
    val phone: String?,
    val address: String?,
    val balance: Double
)

data class CreateCustomerRequest(
    val name: String,
    val phone: String? = null,
    val address: String? = null
)

data class SaleLineRequest(
    val productId: Int,
    val quantity: Int
)

data class CreateSaleRequest(
    val clientId: String,
    val customerId: Int?,
    val fulfillmentType: String,
    val deliveryAddress: String?,
    val deliveryNotes: String?,
    val amountPaid: Double,
    val lines: List<SaleLineRequest>
)

data class SaleLineDto(
    val id: Int,
    val productId: Int,
    val productName: String,
    val quantity: Int,
    val unitPrice: Double,
    val lineTotal: Double
)

data class SaleDto(
    val id: Int,
    val customerId: Int?,
    val customerName: String?,
    val cashierUserId: Int,
    val cashierName: String,
    val fulfillmentType: String,
    val deliveryAddress: String?,
    val deliveryNotes: String?,
    val deliveryStatus: String,
    val subtotal: Double,
    val amountPaid: Double,
    val balanceDue: Double,
    val createdAt: String,
    val clientId: String,
    val lines: List<SaleLineDto>
)

data class PaymentRequest(
    val customerId: Int?,
    val saleId: Int?,
    val amount: Double,
    val method: String = "Cash"
)

data class PaymentDto(
    val id: Int,
    val customerId: Int?,
    val saleId: Int?,
    val amount: Double,
    val method: String,
    val createdAt: String,
    val createdBy: String
)

data class DeliveryDto(
    val saleId: Int,
    val customerName: String?,
    val deliveryAddress: String?,
    val deliveryNotes: String?,
    val deliveryStatus: String,
    val subtotal: Double,
    val createdAt: String
)

data class UpdateDeliveryStatusRequest(
    val status: String
)

data class DashboardDto(
    val stock: List<StockDto>,
    val todayProductionQty: Int,
    val todaySalesTotal: Double,
    val todaySalesCount: Int,
    val totalUnpaid: Double,
    val debtorCount: Int,
    val materials: List<RawMaterialDto> = emptyList()
)

data class RawMaterialDto(
    val id: Int,
    val name: String,
    val unit: String,
    val quantityOnHand: Double,
    val minStock: Double,
    val notes: String? = null,
    val isLow: Boolean,
    val isActive: Boolean = true
)

data class ApiErrorBody(
    val message: String?
)
