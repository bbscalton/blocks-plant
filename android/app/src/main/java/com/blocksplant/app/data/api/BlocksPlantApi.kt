package com.blocksplant.app.data.api

import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.PATCH
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

interface BlocksPlantApi {
    @POST("api/auth/login")
    suspend fun login(@Body body: LoginRequest): LoginResponse

    @GET("api/products")
    suspend fun getProducts(): List<ProductDto>

    @GET("api/stock")
    suspend fun getStock(): List<StockDto>

    @POST("api/production")
    suspend fun createProduction(@Body body: ProductionRequest): ProductionDto

    @GET("api/customers")
    suspend fun getCustomers(@Query("search") search: String? = null): List<CustomerDto>

    @GET("api/customers/debtors")
    suspend fun getDebtors(): List<CustomerDto>

    @POST("api/customers")
    suspend fun createCustomer(@Body body: CreateCustomerRequest): CustomerDto

    @POST("api/sales")
    suspend fun createSale(@Body body: CreateSaleRequest): SaleDto

    @GET("api/sales")
    suspend fun getSales(@Query("take") take: Int = 100): List<SaleDto>

    @POST("api/payments")
    suspend fun createPayment(@Body body: PaymentRequest): PaymentDto

    @GET("api/deliveries")
    suspend fun getDeliveries(
        @Query("includeDelivered") includeDelivered: Boolean = false
    ): List<DeliveryDto>

    @PATCH("api/deliveries/{saleId}")
    suspend fun updateDelivery(
        @Path("saleId") saleId: Int,
        @Body body: UpdateDeliveryStatusRequest
    ): DeliveryDto

    @GET("api/dashboard")
    suspend fun getDashboard(): DashboardDto

    @GET("api/materials")
    suspend fun getMaterials(
        @Query("includeInactive") includeInactive: Boolean = false
    ): List<RawMaterialDto>
}
