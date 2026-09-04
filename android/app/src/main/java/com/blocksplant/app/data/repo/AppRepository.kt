package com.blocksplant.app.data.repo

import com.blocksplant.app.data.api.ApiClientFactory
import com.blocksplant.app.data.api.CreateCustomerRequest
import com.blocksplant.app.data.api.CreateSaleRequest
import com.blocksplant.app.data.api.LoginRequest
import com.blocksplant.app.data.api.PaymentRequest
import com.blocksplant.app.data.api.ProductionRequest
import com.blocksplant.app.data.api.UpdateDeliveryStatusRequest
import com.blocksplant.app.data.local.PendingProductionDao
import com.blocksplant.app.data.local.PendingProductionEntity
import com.blocksplant.app.data.prefs.SessionStore
import com.blocksplant.app.util.isNetworkFailure
import com.blocksplant.app.util.toUserMessage
import kotlinx.coroutines.flow.Flow
import java.util.UUID

class AppRepository(
    private val apiFactory: ApiClientFactory,
    private val sessionStore: SessionStore,
    private val pendingDao: PendingProductionDao
) {
    private fun api() = apiFactory.api()

    val pendingCount: Flow<Int> = pendingDao.observeCount()
    val pendingItems: Flow<List<PendingProductionEntity>> = pendingDao.observeAll()

    suspend fun login(username: String, password: String) {
        val response = api().login(LoginRequest(username.trim(), password))
        sessionStore.saveSession(
            response.token,
            response.username,
            response.fullName,
            response.role,
            response.userId
        )
    }

    fun logout() = sessionStore.clearSession()

    fun setBaseUrl(url: String) {
        sessionStore.setBaseUrl(url)
        apiFactory.invalidate()
    }

    suspend fun getProducts() = api().getProducts()
    suspend fun getStock() = api().getStock()
    suspend fun getCustomers(search: String? = null) = api().getCustomers(search)
    suspend fun getDebtors() = api().getDebtors()
    suspend fun createCustomer(name: String, phone: String?, address: String?) =
        api().createCustomer(CreateCustomerRequest(name, phone, address))

    suspend fun getSales() = api().getSales()
    suspend fun createSale(request: CreateSaleRequest) = api().createSale(request)
    suspend fun createPayment(request: PaymentRequest) = api().createPayment(request)
    suspend fun getDeliveries(includeDelivered: Boolean = false) =
        api().getDeliveries(includeDelivered)

    suspend fun markDelivered(saleId: Int) =
        api().updateDelivery(saleId, UpdateDeliveryStatusRequest("Delivered"))

    suspend fun getDashboard() = api().getDashboard()
    suspend fun getMaterials() = api().getMaterials()

    /**
     * Offline-first production submit: try API; on network failure queue locally.
     * @return message describing outcome
     */
    suspend fun submitProduction(
        productId: Int,
        productName: String,
        quantity: Int,
        rejects: Int
    ): String {
        val clientId = UUID.randomUUID().toString()
        val request = ProductionRequest(productId, quantity, rejects, clientId)
        return try {
            api().createProduction(request)
            "Production saved."
        } catch (t: Throwable) {
            if (isNetworkFailure(t)) {
                pendingDao.upsert(
                    PendingProductionEntity(
                        clientId = clientId,
                        productId = productId,
                        productName = productName,
                        quantity = quantity,
                        rejects = rejects,
                        createdAtEpochMs = System.currentTimeMillis(),
                        lastError = null
                    )
                )
                "Saved offline — will sync when online."
            } else {
                throw t
            }
        }
    }

    suspend fun syncPending(): SyncResult {
        val items = pendingDao.getAll()
        if (items.isEmpty()) return SyncResult(0, 0, emptyList())

        var synced = 0
        val errors = mutableListOf<String>()
        for (item in items) {
            try {
                api().createProduction(
                    ProductionRequest(
                        productId = item.productId,
                        quantity = item.quantity,
                        rejects = item.rejects,
                        clientId = item.clientId
                    )
                )
                pendingDao.delete(item.clientId)
                synced++
            } catch (t: Throwable) {
                val msg = t.toUserMessage()
                pendingDao.setError(item.clientId, msg)
                if (isNetworkFailure(t)) {
                    errors += "Network still unavailable."
                    break
                }
                errors += "${item.productName} x${item.quantity}: $msg"
            }
        }
        return SyncResult(synced, items.size - synced, errors)
    }
}

data class SyncResult(
    val synced: Int,
    val remaining: Int,
    val errors: List<String>
)
