package com.blocksplant.app.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "pending_production")
data class PendingProductionEntity(
    @PrimaryKey val clientId: String,
    val productId: Int,
    val productName: String,
    val quantity: Int,
    val rejects: Int,
    val createdAtEpochMs: Long,
    val lastError: String? = null
)
